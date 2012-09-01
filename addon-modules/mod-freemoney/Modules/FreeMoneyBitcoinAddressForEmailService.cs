using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FreeMoney 
{
    public class BitcoinAddressForEmailService
    {

        private Dictionary<string, string> m_config;

        public BitcoinAddressForEmailService(Dictionary<string, string> config) {
            m_config = config;
        }

        public string BTCAddressForEmail(string email_address) {

            if (email_address == "") {
                return "";
            }

            string url = m_config["bitcoin_address_for_email_service_1_url"];

            if (url == "") {
                return "";
            }

            
            string payer_name = m_config["bitcoin_address_for_email_payer_name"];
            string message_text = m_config["bitcoin_address_for_email_message_text"];

            string post_data = "to=" + HttpUtility.UrlEncode(email_address)
                + "&from=" + HttpUtility.UrlEncode(payer_name)
                + "&message=" + HttpUtility.UrlEncode(message_text);

            HttpWebRequest httpWebRequest=(HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Method = "POST";

            ASCIIEncoding encoding = new ASCIIEncoding ();
            byte[] byte1 = encoding.GetBytes (post_data);

            Console.WriteLine("sending post data"+post_data);

            httpWebRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            httpWebRequest.ContentLength = byte1.Length;

            Console.WriteLine("post length:"+httpWebRequest.ContentLength.ToString());

            Stream newStream = httpWebRequest.GetRequestStream ();
            newStream.Write (byte1, 0, byte1.Length);
            newStream.Close();

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            string response;
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }

            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                Console.WriteLine("address service response code was ng");
                return "";
            }

            Console.WriteLine(response);

            string pattern = "\\\"bitcoin\\:(.*?)\\\"";
            Match m = Regex.Match(response, pattern, RegexOptions.Multiline);
            if (m.Success) {

                GroupCollection gc = m.Groups;
                if (gc.Count < 2) {
                    Console.WriteLine("not found");
                    return "";
                }
                string new_address = (string)gc[1].Value;
                Console.WriteLine("Created new address "+new_address);
                return new_address;

            } else {
                Console.WriteLine("not found");
            }

            return "";

        }

    } 
}

