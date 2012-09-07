using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;


// For JSON Decoding 
//using OpenMetaverse;
//using OpenMetaverse.StructuredData;




namespace FreeMoney 
{
    public class BitcoinNotificationService
    {

        private Dictionary<string, string> m_config;
        private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);

        private string m_agent_id = "";

        private string m_btc_address = "";
        private int m_num_confirmations_received = 0;
        private decimal m_amount_received = 0.0m;
        private string m_signature = "";

        public BitcoinNotificationService(Dictionary<string, string> config) {
            m_config = config;
        }

        public string BtcAddress() {
            return m_btc_address;
        }

        public int NumConfirmationsReceived() {
            return m_num_confirmations_received; 
        }

        public decimal AmountReceived() {
            return m_amount_received;
        }

        private string AgentName(int num_confirmations_required) {

            string agent_name = m_config["bitcoin_ping_service_1_agent_name"];
            if (agent_name == "") {
                return "";
            }

            return agent_name + "_" + num_confirmations_required.ToString();
             
        }

        private string SubscriptionURL(int num_confirmations_required) 
        {

            if (m_agent_id == "") {
                //print "Could not make subscription URL: no agent_id";
                return "";
            }

            string url = m_config["bitcoin_ping_service_1_base_url"]+"/"+m_agent_id+"/address/";

            return url;

        }

        public bool Subscribe(string address, int num_confirmations_required, string pingback_url) 
        {

            if (address == "") {
                //print "Could not subscribe, address not set.";
                return false;
            }

            if (!InitializeAgent(num_confirmations_required, pingback_url)) {
                //print "Could not initialize agent";
                return false;
            }

            string url = SubscriptionURL(num_confirmations_required);
            if (url == "") {
                //print "Could not make URL";
                return false;
            }

            string data = "address="+address;

            m_log.Info("[FreeMoney] Planning to hit remote service to set up a notification for the address "+address);
            //Console.WriteLine(url);
            //Console.WriteLine(data);
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

        private bool SetAgentCallback(int num_confirmations_required, string pingback_url) {

            m_log.Info("[FreeMoney] Telling the notification service to use the URL "+pingback_url);

            string data = "req_confirmations="+num_confirmations_required.ToString()
                + "&url="+pingback_url;
//m_config["bitcoin_external_url"];

            //string useragent = "Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin";

            string url = m_config["bitcoin_ping_service_1_base_url"]+"/"+m_agent_id+"/"+"notification"+"/"+"url"+"/";
            //Console.WriteLine(url);
            //Console.WriteLine(data);

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
            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }
            //Console.WriteLine(response);

            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                m_log.Warn("[FreeMoney] Got a bad status code from the notification service when trying to set a callback URL.");
                return false;
            }

            try {

                JObject agent = (JObject)JObject.Parse(response);
                if ( (int)agent["id"] != 0 ) {
                    // This is the URL id, but seeing "id" should mean we're ok
                    //m_agent_id = agent["id"].ToString();
                    return true;
                }
 
            } catch (Exception e) {

                m_log.Warn("[FreeMoney] Could not understand the response when trying to set a callback URL.");
                //m_log.Warn(Console.WriteLine(e.ToString());
                //Console.WriteLine(response);

            }

            return false;

        }
        

        private bool CreateAgent(string agent_name) {

            m_log.Info("[FreeMoney] Creating an agent with the notification service with the name "+agent_name);

            string data = "name="+agent_name
                + "&watch_type=2";

            //string useragent = "Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin";

            string url = m_config["bitcoin_ping_service_1_base_url"]+"/";
            Console.WriteLine(url);


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
            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }
            Console.WriteLine(response);

            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                m_log.Warn("[FreeMoney] Got a bad status code from the notification service when trying to set up a notification agent.");
                return false;
            }

            try {

                JArray agent = (JArray)JArray.Parse(response);
                if ( (string)agent["id"] != "" ) {
                    m_agent_id = agent["id"].ToString();
                    m_log.Info("[FreeMoney] Using the agent ID "+m_agent_id+". Set this in the config to make transactions faster.");
                    return true;
                }
 
            } catch (Exception e) {

                m_log.Warn("[FreeMoney] Could not parse response when trying to set up a notification agent.");
                //Console.WriteLine("Parsing of creation attempt failed.");
                //Console.WriteLine(e.ToString());
                //Console.WriteLine(response);

            }

            return false;

        }

        private bool InitializeAgent(int num_confirmations_required, string pingback_url) {

            m_log.Info("[FreeMoney] Initializing notification agent.");

            if (m_config["bitcoin_ping_service_1_agent_id"] != "") {
                m_agent_id = m_config["bitcoin_ping_service_1_agent_id"];
            }

            string agent_name = AgentName(num_confirmations_required);
            //Console.WriteLine("Made agent with name" + agent_name);

            string url = m_config["bitcoin_ping_service_1_base_url"];
            if (url == "") {
                //print "Could not make URL";
                return false;
            }

            HttpWebRequest httpWebRequest=(HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Headers.Add("Authorization: "+ m_config["bitcoin_ping_service_1_accesskey"]);
            //httpWebRequest.Method = "POST";
            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();

            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                return false;
            }

            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }

            try {

                //Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(post_body);
                //JObject jo = JObject.Parse(response);
                JArray agents = JArray.Parse(response);
                //JArray agents = (JArray)jo["signed_data"];
    
                foreach (JToken agent in agents )
                {
                    if ((string)agent["name"] == agent_name) {
                        m_agent_id = agent["id"].ToString();
                        m_log.Info("[FreeMoney] Got agent "+agent_name+", set agent id to "+m_agent_id);
                        //"urlnotification_set"
                        JArray notifications = (JArray)agent["urlnotification_set"];
                        if (notifications.Count == 0) {
                            m_log.Info("[FreeMoney] No URLs set, creating for "+agent_name);
                            return SetAgentCallback(num_confirmations_required, pingback_url);
                        }
                        m_log.Info("[FreeMoney] URL set OK, InitializeAgent completed.");
                        return true;
                    }

                } 
 
            } catch (Exception e) {
                //Console.WriteLine("Parsing failed in agent initialization");
                m_log.Info("[FreeMoney] Could not understand response when trying to initialize agent.");
                //Console.WriteLine(e.ToString());
                //Console.WriteLine(response);
                return false;
            }

            return ( CreateAgent(agent_name) && SetAgentCallback(num_confirmations_required, pingback_url) );

        }

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
                m_amount_received = Decimal.Parse((string)signed_data["amount_btc"]);
                m_num_confirmations_received = (int)signed_data["confirmations"];
                //m_txhash = (string)signed_data["txhash"];
                
                //m_signvalues.TryGetValue("signature", out sig);

 
            } catch (Exception) {
                m_log.Error("[FreeMoney] Got a notification about a completed transaction, but could not understand it.");
                return false;
            }

            return true;

        }

        public bool IsValid() {
            return true;
        }

    } 
}

