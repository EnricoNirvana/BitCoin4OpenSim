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


namespace FreeMoney 
{
    public class BitcoinExchangeRateService
    {

        private Dictionary<string, string> m_config;

        public BitcoinExchangeRateService(Dictionary<string, string> config) {
            m_config = config;
        }

        public float LookupRate(string currency_code) {

            //string useragent = "Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin";
            string url = m_config["bitcoin_exchange_rate_service_1_url"];
            HttpWebRequest httpWebRequest=(HttpWebRequest)WebRequest.Create(url);

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }

            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                return 0.0F;
            }

            JObject jo = JObject.Parse(response);
            JObject currency_rates = (JObject)jo[currency_code];
            string currency_rate = (string)currency_rates["24h"];
            return (float)Convert.ToDouble(currency_rate);

        }

    } 
}

