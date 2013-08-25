using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using log4net;

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

        private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);

        public BitcoinAddress(string dbConnectionString, Dictionary<string, string> config) {

            m_connectionString = dbConnectionString;
            m_config = config;

        }

        public int CountAddressesForAvatar(string user_identifier, bool include_already_assigned) {

            string query = "";
            if (include_already_assigned) {
                query = "select count(*) as cnt from opensim_btc_addresses where user_identifier=?user_identifier";
            } else {
                query = "select count(*) as cnt from opensim_btc_addresses a left outer join opensim_btc_transactions t on a.btc_address=t.btc_address where a.user_identifier=?user_identifier AND t.confirmation_sent_ts > 0 OR t.id IS NULL;";
            }

            int num = 0;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {

                dbcon.Open();

                MySqlCommand cmd = new MySqlCommand( query, dbcon);
                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?user_identifier", user_identifier);

                        using (MySqlDataReader dbReader = cmd.ExecuteReader())
                        {
                            if (dbReader.Read())
                            {
                                num = dbReader.GetInt32(0);
                            }
                        }

                        cmd.Dispose();

                    }
                }
                catch (Exception e)
                {
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}")
                    m_log.Error("[FreeMoney] Error counting addresses for avatar: "+e.ToString());
                }

            }

            return num;

        }

        public string AddressForAvatar(string user_identifier, string user_email) {

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
                                m_btc_address = (string)dbReader["btc_address"];
                            }
                        }

                        cmd.Dispose();

                        return m_btc_address;

                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[FreeMoney] Error fetching addresses for avatar: "+e.ToString());
                    //m_log.ErrorFormat("[ASSET DB]: MySQL failure creating asset {0} with name \"{1}\". Error: {2}",
                    //return "";
                }

            }

            if (user_email == "") {
                return "";
            }

            // No address yet - try to create one with an email service.
            BitcoinAddressForEmailService serv = new BitcoinAddressForEmailService(m_config);
            string new_btc_address = serv.BTCAddressForEmail(user_email);

            if (new_btc_address != "") {
                // create a new object - we may want to make this whole method static.
                BitcoinAddress addr = new BitcoinAddress(m_connectionString, m_config);
                if (addr.Create(user_identifier, new_btc_address)) {
                    return new_btc_address;
                }
            }
        
            return "";

        }

        public bool CreateFromLine(string user_identifier, string btc_address_line) {

            // Create as is if we can
            if (IsValidAddress(btc_address_line)) {
                return Create(user_identifier, btc_address_line);
            }

            // CSV export, as exported by the standard clieng:
            // "ed20110831","1L5yiXZCjUrvfPr5LPFFqnJdW9fcecBojS"
            string pattern = "^\".*?\",\"([a-zA-Z1-9]{27,35})\"$";
            Match m = Regex.Match(btc_address_line, pattern);
            if (m.Success) {
                return Create(user_identifier, m.Groups[1].Value);
            }

            // Anything the right length in quotes
            string pattern2 = "\"([a-zA-Z1-9]{27,35})\"";
            Match m2 = Regex.Match(btc_address_line, pattern2);
            if (m2.Success) {
                return Create(user_identifier, m2.Groups[1].Value);
            }

            return false;

        }

        public bool Create(string user_identifier, string btc_address) {
            m_user_identifier = user_identifier;
            m_btc_address = btc_address;
            if (!IsValidAddress(m_btc_address)) {
                return false;
            }
            return Insert();
        }

        private bool Insert() 
        {

            string query = "";

            query += "INSERT INTO opensim_btc.opensim_btc_addresses (";
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

        public static bool IsValidAddress(string address) {

            string pattern = "^[a-zA-Z1-9]{27,35}$";
            Match m = Regex.Match(address, pattern);
            return (m.Success); 

        }

    }

}
