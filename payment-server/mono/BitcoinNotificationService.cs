using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;


namespace OpenSim.FreeMoney
{
    public class BitcoinNotificationService
    {

        private string m_base_url;
        private Dictionary<string, string> m_config;

        private string m_agent_id = "";

        public BitcoinNotificationService(Dictionary<string, string> config, string base_url) {
            m_config = config;
            m_base_url = base_url;
        }

        private string SubscriptionURL() 
        {

            if (m_agent_id == "") {
                //print "Could not make subscription URL: no agent_id";
                return "";
            }

            if (m_base_url == "") {
                //print "Could not find base_url";
                return "";
            }

            string url = m_config["bitcoin_ping_service_1_base_url"]+"/"+m_agent_id+"/address/";

            return url;

        }

        public bool Subscribe(string address, int num_confirmations_required) 
        {

Console.WriteLine("subscribing");
            if (address == "") {
Console.WriteLine("no address");
                //print "Could not subscribe, address not set.";
                return false;
            }

            if (!InitializeAgent()) {
Console.WriteLine("no init agent");
                //print "Could not initialize agent";
                return false;
            }

            string url = SubscriptionURL();
            if (url == "") {
Console.WriteLine("url");
                //print "Could not make URL";
                return false;
            }

            string data = "address="+address;

            //string useragent = "Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin";
Console.WriteLine("making request to url "+url);

            HttpWebRequest myHttpWebRequest=(HttpWebRequest)WebRequest.Create(url);
            myHttpWebRequest.Method = "POST";

            myHttpWebRequest.Headers.Add("Authorization: " + m_config["bitcoin_ping_service_1_accesskey"]);
            Console.WriteLine("\nThe HttpHeaders are \n\n\tName\t\tValue\n{0}",myHttpWebRequest.Headers);

            // Assign the response object of 'HttpWebRequest' to a 'HttpWebResponse' variable.
            HttpWebResponse myHttpWebResponse=(HttpWebResponse)myHttpWebRequest.GetResponse();



            StreamWriter streamWriter = new StreamWriter (myHttpWebRequest.GetRequestStream ());
            streamWriter.Write (data);
            streamWriter.Close ();

            // Print the HTML contents of the page to the console. 
            Stream streamResponse=myHttpWebResponse.GetResponseStream();
            StreamReader streamRead = new StreamReader( streamResponse );
            Char[] readBuff = new Char[256];
            int count = streamRead.Read( readBuff, 0, 256 );
            Console.WriteLine("\nThe HTML contents of page the are  : \n\n ");  
            while (count > 0) 
            {
                String outputData = new String(readBuff, 0, count);
                Console.Write(outputData);
                count = streamRead.Read(readBuff, 0, 256);
            }
            // Close the Stream object.
            streamResponse.Close();
            streamRead.Close();
            // Release the HttpWebResponse Resource.

            return (myHttpWebResponse.StatusCode == HttpStatusCode.OK);

        }

        private bool InitializeAgent() {

            //string agent_name = m_config["bitcoin_ping_service_1_agent_name"];

            // TODO: Instead of hard-coding this, fetch an angent with that name from service using the agent name.
            // ...or if it doesn't exist, create it.
            m_agent_id = "142";

            return true;

        }


        /*
            config.Add("bitcoin_ping_service_1_agent_name", "opensim_bitcoin_dev_agent");
            config.Add("bitcoin_ping_service_1_base_url", "http://www.bitcoinmonitor.net/api/v1/agent");
            config.Add("bitcoin_ping_service_1_accesskey", "8c673a5239d05f137e2fac4e3e3d0600be870afd");
            config.Add("bitcoin_ping_service_1_verificationkey", "cd65311804492d69cdaf896052e98fe442c0bac5");

            config.Add("bitcoin_exchange_rate_service_1_url", "http://bitcoincharts.com/t/weighted_prices.json");

            config.Add("bitcoin_address_for_email_service_1_url", "http://coinapult.com/payload/send/");
        */



    } 
}

