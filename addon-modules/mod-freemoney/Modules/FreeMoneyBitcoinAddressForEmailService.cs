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
using log4net;

namespace FreeMoney 
{
    public class BitcoinAddressForEmailService
    {

        private Dictionary<string, string> m_config;
        private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);

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

            m_log.Info("[FreeMoney] Sending post data"+post_data);

            httpWebRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            httpWebRequest.ContentLength = byte1.Length;

            m_log.Info("[FreeMoney] Post length:"+httpWebRequest.ContentLength.ToString());

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
                m_log.Warn("[FreeMoney] address service response code was ng");
                return "";
            }

            //Console.WriteLine(response);

            string pattern = "\\\"bitcoin\\:(.*?)\\\"";
            Match m = Regex.Match(response, pattern, RegexOptions.Multiline);
            if (m.Success) {

                GroupCollection gc = m.Groups;
                if (gc.Count < 2) {
                    m_log.Warn("[FreeMoney] Could not find an address in the response from the email service.");
                    return "";
                }
                string new_address = (string)gc[1].Value;
                if (!BitcoinAddress.IsValidAddress(new_address)) {
                    m_log.Warn("[FreeMoney] Created new address "+new_address+", but it did not look like a valid address.");
                    return "";
                }
                
                m_log.Info("[FreeMoney] Created new address "+new_address);
                return new_address;

            } else {
                m_log.Warn("[FreeMoney] Could not find an address in the response from the email service.");
            }

            return "";

        }

    } 
}

