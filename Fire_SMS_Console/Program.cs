﻿using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using System.Data;
using System.Collections;
using Fire_SMS_Console.App_Code;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace Fire_SMS_Console
{
    class Program
    {
        //you'll need to modify the connection string to get the exact tables
        static string ConnectionString = "Server=DB IP ADDRESS Port=5432;User ID=DB USER;Password=DB PASSWORD;Database=fire_sms_registration";

        static int Main(string[] args)
        {
            //Connect to PostGres Database.  Get Unique List of Districts with Fires.
            ProcessFiresForUsers(GetAffectedUsers());

            //Now, assemble the SMS message based on the fires in each district, and send messgaes to each user.
            Console.WriteLine("Press Enter to Finish");
            Console.ReadLine();
            return 1;

        }

        public static void ProcessFiresForUsers(ArrayList affectedUsers)
        {
            //AffectedUsers is a list of users who subscribe to districts where there are currently fires.  Unique based on E-mail
            List<string> regions = new List<string>();
            foreach (PhoneUser p in affectedUsers)
            {
                //Get all fires tied to regions subscribed to by this user's email
                ArrayList fires = GetFiresForUser(p.email);

                //Create SMS Message and send
                ProcessSMS(p, fires);

                //Create Email Message and send
                ProcessEmail(p, fires);
            }
        }


        public static ArrayList GetAffectedUsers()
        {

            ArrayList list = new ArrayList();

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            try
            {
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "SELECT DISTINCT ON (email) * FROM user_phones WHERE regionid IN (SELECT DISTINCT ON (regionid) regionid)";
                cmd.Connection = conn;

                NpgsqlDataReader thisReader = cmd.ExecuteReader();
                while (thisReader.Read())
                {
                    PhoneUser pu = new PhoneUser();
                    pu.ID = int.Parse(thisReader["id"].ToString());
                    pu.email = thisReader["email"].ToString();
                    pu.phonenumber = thisReader["phonenumber"].ToString();
                    pu.regionid = int.Parse(thisReader["regionid"].ToString());
                    pu.regionname = thisReader["regionname"].ToString();
                    DateTime dt;
                    DateTime.TryParse(thisReader["lastsmssent"].ToString(), out dt);

                    if(dt != null){
                        pu.lastsmssent = dt;
                    }

                    list.Add(pu);
                    Console.WriteLine("\t{0}", thisReader["email"]);
                }
                thisReader.Close();
                conn.Close();

                return list;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
            finally
            {
                conn.Close();
            }
        }

        public static ArrayList GetFiresForUser(string userEmail)
        {

            ArrayList list = new ArrayList();

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            try
            {
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.CommandType = CommandType.Text;
                //obviously you'll have to modify the SQl to reflect what you actually want in the message
                cmd.CommandText = "SELECT fire_bump_output.*, user_phones.regionname FROM fire_bump_output, user_phones WHERE fire_bump_output.regionid = user_phones.regionid AND user_phones.email = '" + userEmail + "'";
                cmd.Connection = conn;

                NpgsqlDataReader thisReader = cmd.ExecuteReader();
                while (thisReader.Read())
                {
                    Fire f = new Fire();
                    f.id = thisReader["id"].ToString();
                    f.x = thisReader["x"].ToString();
                    f.y = thisReader["y"].ToString();
                    f.regionid = thisReader["regionid"].ToString();
                    f.regionname = thisReader["regionname"].ToString();

                    list.Add(f);
                    Console.WriteLine("Adding Fires to List");
                }
                thisReader.Close();
                conn.Close();

                return list;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
            finally
            {
                conn.Close();
            }
        }

        public static ArrayList GetUniqueRegionIDs()
        {

            ArrayList list = new ArrayList();

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            try
            {
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "SELECT DISTINCT ON (regionid) regionid FROM user_phones WHERE id IN (SELECT DISTINCT ON (regionid) regionid)";
                cmd.Connection = conn;

                NpgsqlDataReader thisReader = cmd.ExecuteReader();
                while (thisReader.Read())
                {
                    list.Add(thisReader["regionid"]);
                    Console.WriteLine("\t{0}", thisReader["regionid"]);
                }
                thisReader.Close();
                conn.Close();

                return list;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
            finally
            {
                conn.Close();
            }
        }

        public static void ProcessSMS(PhoneUser pu,ArrayList fires)
        {
            string smsmessage = CreateSMSMessage(pu, fires);

            //Cue it up to be sent.
            SendSMS(pu.phonenumber, smsmessage);
        }

        public static void ProcessEmail(PhoneUser pu,ArrayList fires)
        {
            string emailmessage = CreateEmailMessage(pu, fires);

            //Cue it up to be sent.
            SendMail(pu.email, null, emailmessage);
        }

        public static string CreateSMSMessage(PhoneUser pu, ArrayList fires)
        {
            string smsmessage = "There are " + fires.Count + " fires in " + pu.regionname + " as of " + DateTime.UtcNow.ToString().Replace("/", "-");
            foreach (Fire fire in fires)
            {
                smsmessage += " ID: " + fire.id + " Lat:" + fire.y + " Lng:" + fire.x;
            }
            return smsmessage;
        }

        public static string CreateEmailMessage(PhoneUser pu, ArrayList fires)
        {

            string message = "This message has been auto generated by NASA Fire Alerter, Please do not reply<br/><br/>";
            message += "There are " + fires.Count + " fires in your area(s) of interest as of " + DateTime.UtcNow + " UTC";
            foreach (Fire fire in fires)
            {
                message += "<br/>ID: " + fire.id + ",   Lat:" + fire.y + ",    Lng:" + fire.x + ",    Region: " + fire.regionname;
            }
            return message;
        }

        public static bool SendMail(string to, string cc, string message) {
        
            try
            {

                var fromAddress = new MailAddress("noreply.nasafire@gmail.com", "NASA Fire Advisor");
                var toAddress = new MailAddress(to);
                const string fromPassword = "EMAIL PASSWORD";
                const string subject = "Fire!";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,

                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                if (to.Length > 0)
                {
                    using (var mmessage = new MailMessage(fromAddress, toAddress)
                    {
                        Subject = subject,
                        Body = message,
                        IsBodyHtml = true
                    })
                    {
                        smtp.Send(mmessage);
                    }
                }
                if (cc.Length > 0)
                {
                    var ccAddress = new MailAddress(cc);
                    using (var message2 = new MailMessage(fromAddress, ccAddress)
                    {
                        Subject = subject,
                        Body = message,
                        IsBodyHtml = true
                    })
                    {
                        smtp.Send(message2);
                    }
                }
                Console.WriteLine("email sent to " + toAddress);
                return true;

            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static bool SendSMS(string to, string message)
        {
            try
            {
                //modify the name of the laptop/or computer that will be sending the actual message. 
                string destinationURL = "http://NAME OF FRONELINE SMS HOST SYSTEM :8011/send/sms/" + HttpUtility.HtmlEncode(to) + "/" + HttpUtility.HtmlEncode(message);
                HttpHelper http = new HttpHelper();
                string html = http.HttpStringGet(destinationURL);
                Console.WriteLine(html);
                return true;
            }   
            catch (Exception e)
            {
                throw e;
            }
        }

    }
}
