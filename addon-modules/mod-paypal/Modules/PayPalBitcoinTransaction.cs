using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;

// For unix timestamps
using OpenMetaverse;



namespace PayPal
{
    public class BitcoinTransaction
    {

        private string m_connectionString;
        private Dictionary<string, string> m_config;


        // The payee. In the PayPal world this would be "business", which is an email address. We will have to do some kind of lookup to get a Bitcoin address for this person.
        private string m_payee = ""; 

        // The name of the object to be transferred, for humans.
        private string m_item_name = ""; 

        // The transaction ID. In the Paypay world it's called "item_number".
        private string m_transaction_code = ""; 

        // The original amount requested in the original currency. If quoted in a non-BTC currency, we may convert it. In PayPal, "amount".
        private float m_original_amount = 0.0F; 

        // The currency originally requested. In PayPal, "currency_code".
        private string m_original_currency_code = ""; 

        // The BTC equivalent at the time when the payment address was given to the user.
        private float m_btc_amount = 0.0F; 

        // The URL we  should notify when the payment is confirmed.
        private string m_notify_url = ""; 

        // The address we gave the user to pay.
        private string m_btc_address = ""; 

        // The number of confirmations we are expected to wait for before considering the payment paid.
        private int m_num_confirmations_required = 0; 

        // A unix timestamp for the time we first heard about the transaction.
        private int m_created_ts = 0; 

        // A timestamp for the first time we saw the payment on the network. 
        private int m_payment_detected_ts = 0; 

        // The number of confirmations received, last time we checked.
        private int m_num_confirmations_received = 0; 

        // A timestamp for the time we notified the server that payment was complete, causing object delivery etc.
        private int m_confirmation_sent_ts = 0; 


        private string m_base_url = "";

        public BitcoinTransaction(string dbConnectionString, Dictionary<string, string> config, string base_url) {

            m_connectionString = dbConnectionString;
            m_config = config;
            m_base_url = base_url;

        }

        public bool Initialize(Dictionary<string,string> transaction_params) {


            m_transaction_code = transaction_params["item_number"];
            m_payee = transaction_params["business"];
            m_item_name = transaction_params["item_name"];
            m_original_amount =  (float) Convert.ToDouble(transaction_params["amount"]);
            m_original_currency_code = transaction_params["currency_code"];
            m_notify_url = transaction_params["notify_url"];
            m_num_confirmations_required = 0; // TODO: We may get this from the server

            /*
            if (!$this->_mysqli) {
                print_simple_and_exit( "No mysqli" );
                return false;
            }
            */

            if (m_transaction_code == "") {
                return false;
            }
    
            if ( !(Populate("transaction_code") || Create()) ) {
                Console.WriteLine("could not create");
                return false;
            }
            Console.WriteLine("trying notification service");

            BitcoinNotificationService notification_service = new BitcoinNotificationService(m_config, m_base_url);
            return (notification_service.Subscribe(m_btc_address, m_num_confirmations_required));

        }

        public bool PopulateByBtcAddress(string btc_address) {
            m_btc_address = btc_address;
            return Populate("btc_address");
        }

        public bool PopulateByTransactionCode(string transaction_code) {
            m_transaction_code = transaction_code;
            return Populate("transaction_code");
        }
    
        private bool Populate(string by) 
        {

            string query = "SELECT payee, item_name, transaction_code, original_amount, btc_amount, notify_url, btc_address, num_confirmations_required, created_ts, payment_detected_ts, num_confirmations_received, confirmation_sent_ts FROM opensim_btc_transactions";

            if (by == "transaction_code") {
                query += " WHERE transaction_code=?transaction_code";
            } else if (by == "btc_address") {
                query += " WHERE btc_address=?btc_address";
            } else {
                // not supported
                return false;
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                bool found = false;

                try
                {
                    using (cmd)
                    {
                        if (by == "transaction_code") {
                            cmd.Parameters.AddWithValue("?transaction_code", m_transaction_code);
                        } else {
                            cmd.Parameters.AddWithValue("?btc_address", m_btc_address);
                        }

                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                m_payee = (string)dbReader["payee"];
                                m_item_name = (string)dbReader["item_name"];
                                m_transaction_code = (string)dbReader["transaction_code"];
                                m_original_amount = (float)dbReader["original_amount"];
                                m_btc_amount = (float)dbReader["btc_amount"];
                                m_notify_url = (string)dbReader["notify_url"];
                                m_btc_address = (string)dbReader["btc_address"];
                                m_num_confirmations_required = (int)dbReader["num_confirmations_required"];
                                m_created_ts = (int)dbReader["created_ts"];
                                m_payment_detected_ts = (int)dbReader["payment_detected_ts"];
                                m_num_confirmations_received = (int)dbReader["num_confirmations_received"];
                                m_num_confirmations_received = (int)dbReader["num_confirmations_received"];
                                m_confirmation_sent_ts = (int)dbReader["confirmation_sent_ts"];

                                found = true;
                            }
                        }

                        cmd.Dispose();

                        return found;

                    }
                }
                catch (Exception)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                }

            }
        
            return false;

        }

        private float ToBTC(float original_amount, string currency_code) 
        {
            // TODO: Do this properly...
            return (float)(0.001 * original_amount);
        }

        private bool Create() 
        {

            //m_created_ts = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
            m_created_ts = 0;

            if (m_original_currency_code == "BTC") {
                m_btc_amount = m_original_amount;
            } else {
                m_btc_amount = ToBTC(m_original_amount, m_original_currency_code); 
            }

            /*
            // TODO: Get address from BitcoinAddress object
            if (!m_btc_address = BitcoinAddress.AddressForAvatar(m_payee)) {
                return false;
            }
            */
            m_btc_address = "15S5AqChfugJRaUZSAe2tkvhjqMkn3qo7y";

            string query = "";
            query += "INSERT INTO opensim_btc_transactions (";
            query += "payee, ";
            query += "item_name, ";
            query += "transaction_code, ";
            query += "original_amount, ";
            query += "original_currency_code, ";
            query += "btc_amount, ";
            query += "notify_url, ";
            query += "btc_address, ";
            query += "num_confirmations_required, ";
            query += "created_ts";
            query += ") values(";
            query += "?payee, ";
            query += "?item_name, ";
            query += "?transaction_code, ";
            query += "?original_amount, ";
            query += "?original_currency_code, ";
            query += "?btc_amount, ";
            query += "?notify_url, ";
            query += "?btc_address, ";
            query += "?num_confirmations_required, ";
            query += "?created_ts);";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?payee", m_payee);
                        cmd.Parameters.AddWithValue("?item_name", m_item_name);
                        cmd.Parameters.AddWithValue("?transaction_code", m_transaction_code);
                        cmd.Parameters.AddWithValue("?original_amount", m_original_amount);
                        cmd.Parameters.AddWithValue("?original_currency_code", m_original_currency_code);
                        cmd.Parameters.AddWithValue("?btc_amount", m_btc_amount);
                        cmd.Parameters.AddWithValue("?notify_url", m_notify_url);
                        cmd.Parameters.AddWithValue("?btc_address", m_btc_address);
                        cmd.Parameters.AddWithValue("?num_confirmations_required", m_num_confirmations_required);
                        cmd.Parameters.AddWithValue("?created_ts", m_created_ts);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();

                        return true;

                    }
                }
                catch (Exception)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                }

            }

            return false;

        }

        float ToBTC(float amount, float currency_code) {

            //float btc = BitcoinExchangeRateService.ToBTC(amount, currency_code);
            // TODO
            float btc = amount;
            return btc;

        }

        public bool MarkNotified() {

            if (m_transaction_code == "") {
                return false;
            }

            string query = "update opensim_btc_transactions set confirmation_sent_ts=?confirmation_sent_ts where confirmation_sent_ts = 0 and transaction_code=?transaction_code";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?confirmation_sent_ts",(int)Utils.DateTimeToUnixTime(DateTime.UtcNow));
                        cmd.Parameters.AddWithValue("?transaction_code", m_transaction_code);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();

                    }
                }
                catch (Exception)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                    return false;
                }

            }

            return true;

        }

	    public bool MarkConfirmed(int num_confirmations_received) {

            if (m_transaction_code == "") {
                Console.WriteLine("Could not mark confirmed - transaction code not set");
                return false;
            }

            string query = "update opensim_btc_transactions set num_confirmations_received=?num_confirmations_received, payment_detected_ts=?payment_detected_ts where num_confirmations_received<?num_confirmations_received and transaction_code=?transaction_code";
            //string query = "update opensim_btc_transactions set num_confirmations_received=?num_confirmations_received, payment_detected_ts=?payment_detected_ts where transaction_code=?transaction_code";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?num_confirmations_received", m_num_confirmations_received);
                        //cmd.Parameters.AddWithValue("?num_confirmations_received2", m_num_confirmations_received);
                        cmd.Parameters.AddWithValue("?payment_detected_ts",(int)Utils.DateTimeToUnixTime(DateTime.UtcNow));
                        cmd.Parameters.AddWithValue("?transaction_code", m_transaction_code);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();

                    }
                }
                catch (Exception ex)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                    Console.WriteLine("Marking confirmed failed for transaction with id "+m_transaction_code);
                  Console.WriteLine("Error: {0}",  ex.ToString());
                    return false;
                }

            }

            return true;

        }

        public bool IsEnoughConfirmations(int num_confirmations_received) {
            Console.WriteLine("Need "+Convert.ToInt32(m_num_confirmations_required)+" confirmations");
            return num_confirmations_received >= m_num_confirmations_required;
        }

        public bool IsConfirmationSent() {
            return (m_confirmation_sent_ts > 0);
        }

        public float GetBTCAmount() {
            return m_btc_amount;
        }

        public string GetBTCAddress() {
            return m_btc_address;
        }

        public string GetTransactionID() {
            return m_transaction_code;
        }

    } 
}

