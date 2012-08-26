using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;

namespace OpenSim.FreeMoney
{
    public class FreeMoneyTest
    {
        public static void Main(string[] args)
        {

            Dictionary<string,string> config = new Dictionary<string, string>();
            config.Add("bitcoin_db_host", "localhost");
            config.Add("bitcoin_db_name", "opensim_btc");
            config.Add("bitcoin_db_user", "opensim_btc_user");
            config.Add("bitcoin_db_password", "owiejfsd");
            config.Add("bitcoin_external_url", "110-133-28-96.rev.home.ne.jp");
            config.Add("bitcoin_address_for_email_payer_name", "OpenSim Bitcoin Processor");
            config.Add("bitcoin_address_for_email_message_text", "To make payments go directly to your own wallet in future instead of via email, please login and register some Bitcoin addresses.");

            config.Add("bitcoin_ping_service_1_agent_name", "opensim_bitcoin_dev_agent");
            config.Add("bitcoin_ping_service_1_base_url", "http://www.bitcoinmonitor.net/api/v1/agent");
            config.Add("bitcoin_ping_service_1_accesskey", "8c673a5239d05f137e2fac4e3e3d0600be870afd");
            config.Add("bitcoin_ping_service_1_verificationkey", "cd65311804492d69cdaf896052e98fe442c0bac5");

            config.Add("bitcoin_exchange_rate_service_1_url", "http://bitcoincharts.com/t/weighted_prices.json");

            config.Add("bitcoin_address_for_email_service_1_url", "http://coinapult.com/payload/send/");


            FreeMoneyModule fmm = new FreeMoneyModule(config);

            Dictionary<string, string> testparams = new Dictionary<string, string>();
            testparams.Add("payee", "buyer@edochan.com");
            testparams.Add("business", "seller@edochan.com");
            testparams.Add("item_name", "some object");
            testparams.Add("item_number", "TRANSACTION_CODE_GOES_HERE");
            testparams.Add("amount", "0.1");
            testparams.Add("currency_code", "USD");
            testparams.Add("notify_url", "http://beech/notify_me");

            if (fmm.InitializeTransaction(testparams)) {
                Console.WriteLine("Initialized OK");
            } else {
                Console.WriteLine("Initialization failed");
            }

            //Dictionary<string, string> dic = new Dictionary<string, string> ();
            //ShowPaymentWebPage();
            //Console.WriteLine(fmm.sayHello());
        }
    } 
}
