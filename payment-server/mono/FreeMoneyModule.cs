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
    public class FreeMoneyModule
    {

        Dictionary<string,string> m_config;
        string m_connectionString = "";

        public FreeMoneyModule(Dictionary<string,string> config) 
        {
            m_config = config; 
            m_connectionString = "" + 
                "Server="  + m_config["bitcoin_db_host"]+";" + 
                "Database="+ m_config["bitcoin_db_name"]+";" + 
                "User ID=" + m_config["bitcoin_db_user"]+";" + 
                "Password="+ m_config["bitcoin_db_password"]+";" + 
                "Pooling=false";
        }

        public bool InitializeTransaction(Dictionary<string,string> transaction_params) {

            string base_url = "http://beech/TODO";

            BitcoinTransaction btc_trans = new BitcoinTransaction(m_connectionString, m_config, base_url);
            return (btc_trans.Initialize(transaction_params));

        }
    } 
}
