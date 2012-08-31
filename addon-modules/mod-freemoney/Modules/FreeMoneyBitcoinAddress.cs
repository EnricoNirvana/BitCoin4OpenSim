using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;

namespace FreeMoney 
{
    public class BitcoinAddress
    {

        private string m_connectionString;
        private Dictionary<string, string> m_config;

        // The address we gave the user to pay.
        private string m_btc_address = ""; 

        // The number of confirmations we are expected to wait for before considering the payment paid.
        private string m_user_identifier = ""; 


        public BitcoinAddress(string dbConnectionString, Dictionary<string, string> config) {

            m_connectionString = dbConnectionString;
            m_config = config;

        }


        public string AddressForAvatar(string user_identifier) {

            string query = "select a.btc_address as btc_address from opensim_btc_addresses a left outer join opensim_btc_transactions t on a.btc_address=t.btc_address where a.user_identifier=?user_identifier AND t.confirmation_sent_ts > 0 OR t.id IS NULL limit 1;";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?user_identifier", m_user_identifier);

                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                m_btc_address = (string)dbReader["address"];
                            }
                        }

                        cmd.Dispose();

                        return m_btc_address;

                    }
                }
                catch (Exception)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                    //return "";
                }

            }

            /*
            // No free addresses found.
            // Try to create one with an address->email service.
            if (!$btc_address = BitcoinAddressForEmailService::BTCAddressForEmail($av)) {
                return false;
            }

            $address = new BitcoinAddress($mysqli);
            $address->btc_address = $btc_address;
            $address->user_identifier = $av;

            if ($address->insert()) {
                return $address->btc_address;
            }

            */
        
            return "";

        }

        public bool Create(string user_identifier, string btc_address) {
            m_user_identifier = user_identifier;
            m_btc_address = btc_address;
            return Insert();
        }

        private bool Insert() 
        {

            string query = "";

            query += "INSERT INTO opensim_btc_addresses (";
            query += "btc_address, ";
            query += "user_identifier";
            query += ") values(";
            query += "?btc_address, ";
            query += "?user_identifier";
            query += ");";

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);

                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?btc_address", m_btc_address);
                        cmd.Parameters.AddWithValue("?user_identifier", m_user_identifier);
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
    } 
}
