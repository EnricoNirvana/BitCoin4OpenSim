using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


// For JSON Decoding 
//using OpenMetaverse;
//using OpenMetaverse.StructuredData;




namespace PayPal
{
    public class BitcoinNotificationService
    {

        private string m_base_url;
        private Dictionary<string, string> m_config;

        private string m_agent_id = "";

        private string m_btc_address = "";
        private int m_num_confirmations_received = 0;
        private float m_amount_received = 0.0F;
        private string m_signature = "";

        public BitcoinNotificationService(Dictionary<string, string> config, string base_url) {
            m_config = config;
            m_base_url = base_url;
        }

        public string BtcAddress() {
            return m_btc_address;
        }

        public int NumConfirmationsReceived() {
            return m_num_confirmations_received; 
        }

        public float AmountReceived() {
            return m_amount_received;
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

            if (address == "") {
                //print "Could not subscribe, address not set.";
                return false;
            }

            if (!InitializeAgent()) {
                //print "Could not initialize agent";
                return false;
            }

            string url = SubscriptionURL();
            if (url == "") {
                //print "Could not make URL";
                return false;
            }

            string data = "address="+address;

            //string useragent = "Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin";

            HttpWebRequest httpWebRequest=(HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Headers.Add("Authorization: "+ m_config["bitcoin_ping_service_1_accesskey"]);
            httpWebRequest.Method = "POST";

            ASCIIEncoding encoding = new ASCIIEncoding ();
            byte[] byte1 = encoding.GetBytes (data);

            //httpWebRequest.ContentType = "application/x-www-form-urlencoded";
            httpWebRequest.ContentLength = byte1.Length;

            Stream newStream = httpWebRequest.GetRequestStream ();
            newStream.Write (byte1, 0, byte1.Length);
            newStream.Close();

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            /*
            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }
            */

            return (httpWebResponse.StatusCode == HttpStatusCode.OK);

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

        public bool ParseRequestBody(string post_body) {

            //JObject o = JObject.Parse(post_body);
            
            // Populate m_num_confirmations_received, m_btc_address, m_amount
            // TODO: May later want txhash

            try {

                //Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(post_body);
                JObject jo = JObject.Parse(post_body);
                JObject signed_data = (JObject)jo["signed_data"];
    
                m_signature = (string)jo["signature"];
                m_btc_address = (string)signed_data["address"];
                m_amount_received = (float) Convert.ToDouble((string)signed_data["amount_btc"]);
                m_num_confirmations_received = (int)signed_data["confirmations"];
                //m_txhash = (string)signed_data["txhash"];
                
                //values.TryGetValue("signature", out sig);

 
            } catch (Exception) {
                Console.WriteLine("Parsing failed.");
                return false;
            }

            return true;

        }

        public bool IsValid() {
            return true;
        }

    } 
}

