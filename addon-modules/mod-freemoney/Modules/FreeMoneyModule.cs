/*
 * Copyright (c) 2009 Adam Frisby (adam@deepthink.com.au), Snoopy Pfeffer (snoopy.pfeffer@yahoo.com)
 *
 * Copyright (c) 2010 BlueWall Information Technologies, LLC
 * James Hughes (jamesh@bluewallgroup.com)
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using System.Security.Cryptography;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Servers;
using OpenSim.Server.Base;
using OpenSim.Region.CoreModules.World.Land;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using Nwc.XmlRpc;
using System.Text.RegularExpressions;

using Mono.Addins;



[assembly: Addin("FreeMoney", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace FreeMoney
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class FreeMoneyModule : ISharedRegionModule, IMoneyModule
    {
        // Change to www.sandbox.paypal.com for testing.
        private string m_ppurl = "www.paypal.com";
        private string m_ppprotocol = "https";
        private string m_pprequesturi = "/cgi-bin/webscr";

        private string m_btcurl = "www.openmicropayments.net";
        private string m_btcprotocol = "https";
        private string m_btcrequesturi = "/ipn.php";

        // External URL - useful for me developing behind a firewall, 
        // probably not useful for anyone else...
        private string m_externalBaseURL = "";
        private string m_connectionString;

        private bool m_active;
        private bool m_enabled;

        private bool m_allowPayPal = false;
        private bool m_allowBitcoin = false;

        private int m_btcNumberOfConfirmationsRequired = 0;

        private string m_gridCurrencyCode = "USD";
        private string m_gridCurrencyText = "US$";
        private string m_gridCurrencySmallDenominationText = "US$ cents";
        private decimal m_gridCurrencySmallDenominationFraction = 100m;
        
        private readonly object m_setupLock = new object ();
        private bool m_setup;

        private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);
        private readonly Dictionary<UUID, string> m_usersemail = new Dictionary<UUID, string> ();

        // Database details and web service info that needs to be passed to Bitcoin objects
        private readonly Dictionary<string, string> m_btcconfig = new Dictionary<string, string> ();

	// We'll keep our own session key for each user we encounter.
	// This will be used in the URL when we show people the Bitcoin transaction pages...
	// ...allowing us to verify their email address when they register new Bitcoin addresses.
	// In future we may also create an option for a separate client process to talk to us
	// ...to find out about payment requests directly, without passing information in a URL.
        private readonly Dictionary<UUID, UUID> m_usersessionkey= new Dictionary<UUID, UUID> ();
	// Keep a dictionary with the opposite mapping for quick lookups
        private readonly Dictionary<UUID, UUID> m_sessionkeyuser= new Dictionary<UUID, UUID> ();

        private IConfigSource m_config;

        private readonly List<Scene> m_scenes = new List<Scene> ();

        private const int m_maxBalance = 100000;

        private readonly Dictionary<UUID, FreeMoneyTransaction> m_transactionsInProgress =
            new Dictionary<UUID, FreeMoneyTransaction> ();

        private bool m_allowGridEmails = false;
        private bool m_allowGroups = false;
        private bool m_balanceOnEntry = true;
        private string m_messageOnEntry = "PayPal Money System:  OS$ 100 = US$ 1.00";

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene> ();

        // private int m_stipend = 1000;
        private float EnergyEfficiency = 0f;
        private int ObjectCount = 0;
        private int PriceEnergyUnit = 0;
        private int PriceGroupCreate = 0;
        private int PriceObjectClaim = 0;
        private float PriceObjectRent = 0f;
        private float PriceObjectScaleFactor = 0f;
        private int PriceParcelClaim = 0;
        private float PriceParcelClaimFactor = 0f;
        private int PriceParcelRent = 0;
        private int PricePublicObjectDecay = 0;
        private int PricePublicObjectDelete = 0;
        private int PriceRentLight = 0;
        private int PriceUpload = 0;
        private int TeleportMinPrice = 0;
        private float TeleportPriceExponent = 0f;

        private bool m_directToBitcoin = false;

        #region Currency - FreeMoney

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Thanks to Melanie for reminding me about 
        /// EventManager.OnMoneyTransfer being the critical function,
        /// and not ApplyCharge.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnMoneyTransfer (object sender, EventManager.MoneyTransferArgs e)
        {
            if (!m_active)
                return;
            
            IClientAPI user = null;
            Scene scene = null;
            
            // Find the user's controlling client.
            lock (m_scenes) {
                foreach (Scene sc in m_scenes) {
                    ScenePresence av = sc.GetScenePresence (e.sender);
                    
                    if ((av != null) && (av.IsChildAgent == false)) {
                        // Found the client,
                        // and their root scene.
                        user = av.ControllingClient;
                        scene = sc;
                    }
                }
            }
            
            if (scene == null || user == null) {
                m_log.Warn ("[FreeMoney] Unable to find scene or user! Aborting transaction.");
                return;
            }
            
            FreeMoneyTransaction txn;
            
            if (e.transactiontype == 5008) {
                // Object was paid, find it.
                SceneObjectPart sop = scene.GetSceneObjectPart (e.receiver);
                if (sop == null) {
                    m_log.Warn ("[FreeMoney] Unable to find SceneObjectPart that was paid. Aborting transaction.");
                    return;
                }
                
                string email;
                
                if (sop.OwnerID == sop.GroupID) {
                    if (m_allowGroups) {
                        if (!GetEmail (scene.RegionInfo.ScopeID, sop.OwnerID, out email)) {
                            m_log.Warn ("[FreeMoney] Unknown email address of group " + sop.OwnerID);
                            return;
                        }
                    } else {
                        m_log.Warn ("[FreeMoney] Payments to group owned objects is disabled.");
                        return;
                    }
                } else {
                    if (!GetEmail (scene.RegionInfo.ScopeID, sop.OwnerID, out email)) {
                        m_log.Warn ("[FreeMoney] Unknown email address of user " + sop.OwnerID);
                        return;
                    }
                }
                
                m_log.Info ("[FreeMoney] Start: " + e.sender + " wants to pay object " + e.receiver + " owned by " +
                            sop.OwnerID + " with email " + email + " " + m_gridCurrencySmallDenominationText + " " + e.amount);
                
                txn = new FreeMoneyTransaction (e.sender, sop.OwnerID, email, e.amount, scene, e.receiver,
                                             e.description + " T:" + e.transactiontype,
                                             FreeMoneyTransaction.InternalTransactionType.Payment);
            } else {
                // Payment to a user.
                string email;
                if (!GetEmail (scene.RegionInfo.ScopeID, e.receiver, out email)) {
                    m_log.Warn ("[FreeMoney] Unknown email address of user " + e.receiver);
                    return;
                }
                
                m_log.Info ("[FreeMoney] Start: " + e.sender + " wants to pay user " + e.receiver + " with email " +
                            email + " " + m_gridCurrencySmallDenominationText + " " + e.amount);
                
                txn = new FreeMoneyTransaction (e.sender, e.receiver, email, e.amount, scene, e.description + " T:" +
                                             e.transactiontype, FreeMoneyTransaction.InternalTransactionType.Payment);
            }
            
            // Add transaction to queue
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Add (txn.TxID, txn);
            
            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;
            
            user.SendLoadURL ("FreeMoney", txn.ObjectID, txn.To, false, "Confirm payment?", m_btcprotocol + "://" + m_btcurl + m_btcrequesturi + "?txn=" + txn.TxID);
        }

        void TransferSuccess (FreeMoneyTransaction transaction)
        {
            if (transaction.InternalType == FreeMoneyTransaction.InternalTransactionType.Payment) {
                if (transaction.ObjectID == UUID.Zero) {
                    // User 2 User Transaction
                    m_log.Info ("[FreeMoney] Success: " + transaction.From + " did pay user " +
                                transaction.To + " " + m_gridCurrencySmallDenominationText + " " + transaction.Amount);
                    
                    IUserAccountService userAccountService = m_scenes[0].UserAccountService;
                    UserAccount ua;
                    
                    // Notify receiver
                    ua = userAccountService.GetUserAccount (transaction.From, "", "");
                    SendInstantMessage (transaction.To, ua.FirstName + " " + ua.LastName +
                                        " did pay you " + m_gridCurrencySmallDenominationText + transaction.Amount);
                    
                    // Notify sender
                    ua = userAccountService.GetUserAccount (transaction.To, "", "");
                    SendInstantMessage (transaction.From, "You did pay " + ua.FirstName + " " +
                                        ua.LastName + " "+m_gridCurrencySmallDenominationText+" " + transaction.Amount);
                } else {
                    if (OnObjectPaid != null) {
                        m_log.Info ("[FreeMoney] Success: " + transaction.From + " did pay object " +
                                    transaction.ObjectID + " owned by " + transaction.To +
                                    " " + m_gridCurrencySmallDenominationText + " " + transaction.Amount);
                        
                        OnObjectPaid (transaction.ObjectID, transaction.From, transaction.Amount);
                    }
                }
            } else if (transaction.InternalType == FreeMoneyTransaction.InternalTransactionType.Purchase) {
                if (transaction.ObjectID == UUID.Zero) {
                    m_log.Error ("[FreeMoney] Unable to find Object bought! UUID Zero.");
                } else {
                    Scene s = LocateSceneClientIn (transaction.From);
                    SceneObjectPart part = s.GetSceneObjectPart (transaction.ObjectID);
                    if (part == null) {
                        m_log.Error ("[FreeMoney] Unable to find Object bought! UUID = " + transaction.ObjectID);
                        return;
                    }
                    
                    m_log.Info ("[FreeMoney] Success: " + transaction.From + " did buy object " +
                                transaction.ObjectID + " from " + transaction.To + " paying " + m_gridCurrencySmallDenominationText + " " +
                                transaction.Amount);
                    
                    IBuySellModule module = s.RequestModuleInterface<IBuySellModule> ();
                    if (module == null) {
                        m_log.Error ("[FreeMoney] Missing BuySellModule! Transaction failed.");
                    } else {
                        ScenePresence sp = s.GetScenePresence(transaction.From);
                        if (sp != null)
                            module.BuyObject (sp.ControllingClient,
                                          transaction.InternalPurchaseFolderID, part.LocalId,
                                          transaction.InternalPurchaseType, transaction.Amount);
                    }
                }
            } else if (transaction.InternalType == FreeMoneyTransaction.InternalTransactionType.Land) {
                // User 2 Land Transaction
                EventManager.LandBuyArgs e = transaction.E;
                
                lock (e) {
                    e.economyValidated = true;
                }
                
                Scene s = LocateSceneClientIn (transaction.From);
                ILandObject land = s.LandChannel.GetLandObject ((int)e.parcelLocalID);
                
                if (land == null) {
                    m_log.Error ("[FreeMoney] Unable to find Land bought! UUID = " + e.parcelLocalID);
                    return;
                }
                
                m_log.Info ("[FreeMoney] Success: " + e.agentId + " did buy land from " + e.parcelOwnerID +
                            " paying " + m_gridCurrencySmallDenominationText + " " + e.parcelPrice);
                
                land.UpdateLandSold (e.agentId, e.groupId, e.groupOwned, (uint)e.transactionID,
                                     e.parcelPrice, e.parcelArea);
            } else {
                m_log.Error ("[FreeMoney] Unknown Internal Transaction Type.");
                return;
            }
            // Cleanup.
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Remove (transaction.TxID);
        }

        // Currently hard coded to $0.01 = OS$1
        private decimal ConvertAmountToCurrency (int amount)
        {
            return amount / m_gridCurrencySmallDenominationFraction;
        }


        static internal void debugStringDict (Dictionary<string, object> strs)
        {
            foreach (KeyValuePair<string, object> str in strs) {
                m_log.Debug ("[FreeMoney] '" + str.Key + "' = '" + (string)str.Value + "'");
            }
        }

        /*
        This is a call to allow the address-handling service to confirm that information that we passed to it
        ...really came from us.
        Specifically, it may want to confirm:
            Payee's Avatar UUID + grid URL for someone identified by email. Having this will allow other grids on hypergrid to pay with just UUID@grid.
            Payer's Avatar UUID + grid URL for someone identified by transaction ID. If the payer signs up, having this will allow any grid, including us, to pay with just UUID@grid, even if they use a different email. (We won't provide the payer's email - the payer can supply that themselves if they want to.)
        NB It would be more efficient to do this by validating the data we send with a shared secret.
            ...although this means we have to manage the API secret. 
        */
        public Hashtable BitcoinConfirmAvatarInfo (Hashtable request) 
        {
            Dictionary<string, object> postvals = ServerUtils.ParseQueryString ((string)request["body"]);
            m_log.Debug ("[FreeMoney] BitcoinConfirmAvatarInfo handling query string:"+(string)request["body"]);

            Hashtable reply = new Hashtable ();
            reply["content_type"] = "text/html";

            if (!postvals.ContainsKey("txnid")) {
               reply["int_response_code"] = 403;
               reply["str_response_string"] = "Forbidden due to missing transaction ID";
                m_log.Debug ("[FreeMoney] Responding with:"+(string)reply["str_response_string"]);
               return reply;
            }

            UUID txnID = new UUID ((string)postvals["txnid"]);
            if (!m_transactionsInProgress.ContainsKey (txnID)) {
                Hashtable ereply = new Hashtable ();
                
                ereply["int_response_code"] = 404;
                ereply["str_response_string"] = "Transaction Not Found";
                m_log.Debug ("[FreeMoney] Responding with:"+(string)reply["str_response_string"]);
                ereply["content_type"] = "text/html";
                
                return reply;
            }

            FreeMoneyTransaction txn = m_transactionsInProgress[txnID];
            UUID payee = txn.From;
            UUID payer = txn.To;

            /*
            We should already have passed whatever information we want to give the money service to it.
            It should therefore always be correct unless something weird is going on.
            */ 
            bool isDodgy = false;
            if (!isDodgy && postvals.ContainsKey("payer_avatar_uuid")) {
                if ((string)postvals["payer_avatar_uuid"] != payer.ToString()) {
                    isDodgy = true;
                }
            }
            if (!isDodgy && postvals.ContainsKey("payee_avatar_uuid")) {
                if ((string)postvals["payee_avatar_uuid"] != payee.ToString()) {
                    isDodgy = true;
                }
            }
            
            // Scene is used in GEtEmil 
            Scene scene;
            

            if (!isDodgy && postvals.ContainsKey("payee_avatar_email")) {
                string payee_email = "";

                if (m_scenes[0] != null) {
                    GetEmail (m_scenes[0].RegionInfo.ScopeID, payee, out payee_email);
                }

                if (postvals["payer_avatar_email"] != payee_email) {
                    isDodgy = true;
                }
                
            }

            reply["int_response_code"] = 200;
            if (isDodgy) {
                reply["str_response_string"] = "NG";
            } else {
                reply["str_response_string"] = "VERIFIED";
            }

            m_log.Debug ("[FreeMoney] Responding with:"+(string)reply["str_response_string"]);
            return reply;

        }

        public Hashtable HandleBitcoinConfirmationPing(Hashtable request_hash) {

                m_log.Error("[FreeMoney] Got request for confirmation");
            //string base_url = "http://beech/TODO";
            string base_url = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;
            /*
            TODO: Recreate the ability to handle multiple services.
            if (!get_params.ContainsKey("service")) {
                //print_simple_and_exit("Error: Service name not set.");
                return false;
            }
            */

            Hashtable error_response = new Hashtable();
            error_response.Add("int_response_code", 400);

            //Dictionary<string, object> postvals = ServerUtils.ParseQueryString ((string)request["body"]);
            string post_data = (string)request_hash["body"];
            m_log.Error("[FreeMoney] Confirmation body:"+post_data);

            BitcoinNotificationService service = new BitcoinNotificationService(m_btcconfig);

            if (!service.ParseRequestBody(post_data)) {
                m_log.Error("[FreeMoney] Could not parse post params");
                return error_response;
            }

            if (!service.IsValid()) {
                return error_response;
            }

            string address = service.BtcAddress();
            int num_confirmations_received = service.NumConfirmationsReceived();

            BitcoinTransaction btc_trans = new BitcoinTransaction(m_connectionString, m_btcconfig, base_url);
            
            if (!btc_trans.PopulateByBtcAddress(address)) {
                m_log.Error("[FreeMoney] Could not find btc_trans");
                return error_response;
            }

            // Always mark the latest number of confirmations, assuming it's more than we had last time.
            if (!btc_trans.MarkConfirmed(num_confirmations_received)) {
                m_log.Error("[FreeMoney] Could not mark confirmed in database"); 
                //print_simple_and_exit("Could not mark btc_trans confirmed despite receiving ".htmlentities($num_confirmations_received)." confirmations.");
                return error_response;
            }

            // If it's less than we need, there should be nothing more to do.
            //if (num_confirmations_received < btc_trans.num_confirmations_required) 
            if (!btc_trans.IsEnoughConfirmations(num_confirmations_received)) {
                m_log.Info("[FreeMoney] Got "+ num_confirmations_received.ToString()+" confirmations for address " + address + ", but that is not enough to complete the transaction."); 
                return error_response;
                //print_simple_and_exit("Not enough confirmations, ignoring.", 200);
            }

            // If we've already notified the client about this btc_trans, no need to do anything else.
            if (btc_trans.IsConfirmationSent()) {
                //print_simple_and_exit("Already confirmed, nothing more to do.", 200);
            }

            UUID txnID = new UUID (btc_trans.GetTransactionID());
            if (!m_transactionsInProgress.ContainsKey (txnID)) {
                Hashtable ereply = new Hashtable ();
                
                ereply["int_response_code"] = 404;
                // 200 OK
                ereply["str_response_string"] = "Invalid Transaction";
                ereply["content_type"] = "text/html";
                
                return ereply;
            }
            
            FreeMoneyTransaction txn = m_transactionsInProgress[txnID];

            if (!btc_trans.MarkNotified()) {
                //print_simple_and_exit("Notified sim, but unable to record the fact that we did.");
                m_log.Error("[FreeMoney] Could not mark notified"); 
                return error_response;
            }

            Util.FireAndForget (delegate { TransferSuccess (txn); });

            Hashtable ok_response = new Hashtable();
            ok_response.Add("int_response_code", 200);
            ok_response.Add("str_response_string", "OK, thanks for letting us know.");
            ok_response.Add("content_type", "text/html");

            return ok_response;

        }

        public Hashtable BitcoinIPN (Hashtable request) {

            return IPN(request, m_btcprotocol+"://" + m_btcurl + m_btcrequesturi);

        }

        public Hashtable PayPalIPN (Hashtable request) {

            return IPN(request, m_ppprotocol+"://" + m_ppurl + m_pprequesturi);

        }

        public Hashtable IPN (Hashtable request, string verifyURL)
        {
            Hashtable reply = new Hashtable ();
            
            // Does not matter what we send back to PP here.
            reply["int_response_code"] = 200;
            // 200 OK
            reply["str_response_string"] = "IPN Processed - Have a nice day.";
            reply["content_type"] = "text/html";
            
            if (!m_active) {
                m_log.Error ("[FreeMoney] Recieved IPN request, but module is disabled. Aborting.");
                reply["str_response_string"] = "IPN Not processed. Module is not enabled.";
                return reply;
            }
            
            Dictionary<string, object> postvals = ServerUtils.ParseQueryString ((string)request["body"]);
            string originalPost = (string)request["body"];
            
            string modifiedPost = originalPost + "&cmd=_notify-validate";
            
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create (verifyURL);
            httpWebRequest.Method = "POST";
            
            httpWebRequest.ContentLength = modifiedPost.Length;
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";

            StreamWriter streamWriter = new StreamWriter (httpWebRequest.GetRequestStream ());
            streamWriter.Write (modifiedPost);
            streamWriter.Close ();
            
            string response;
            
            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }
            
            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                m_log.Error ("[FreeMoney] IPN Status code != 200. Aborting.");
                debugStringDict (postvals);
                return reply;
            }
            
            if (!response.Contains ("VERIFIED")) {
                m_log.Error ("[FreeMoney] IPN was NOT verified. Aborting.");
                debugStringDict (postvals);
                return reply;
            }
            
            // Handle IPN Components
            try {
                if ((string)postvals["payment_status"] != "Completed") {
                    m_log.Warn ("[FreeMoney] Transaction not confirmed. Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }
                
                if (((string)postvals["mc_currency"]).ToUpper () != m_gridCurrencyCode) {
                    m_log.Error ("[FreeMoney] Payment was made in an incorrect currency (" +
                                 postvals["mc_currency"] + "). Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }
                
                // Check we have a transaction with the listed ID.
                UUID txnID = new UUID ((string)postvals["item_number"]);
                FreeMoneyTransaction txn;
                
                lock (m_transactionsInProgress) {
                    if (!m_transactionsInProgress.ContainsKey (txnID)) {
                        m_log.Error ("[FreeMoney] Recieved IPN request for Payment that is not in progress. Aborting.");
                        debugStringDict (postvals);
                        return reply;
                    }
                    
                    txn = m_transactionsInProgress[txnID];
                }
                
                // Check user paid correctly...
                if (((string)postvals["business"]).ToLower () != txn.SellersEmail.ToLower ()) {
                    m_log.Error ("[FreeMoney] Expected payment to " + txn.SellersEmail +
                                 " but receiver was " + (string)postvals["business"] + " instead. Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }

                Decimal amountPaid = Decimal.Parse ((string)postvals["mc_gross"]);
                if (System.Math.Abs (ConvertAmountToCurrency (txn.Amount) - amountPaid) > (Decimal)0.001) {
                    m_log.Error ("[FreeMoney] Expected payment was " + ConvertAmountToCurrency (txn.Amount) +
                                 " but received " + amountPaid + " " + postvals["mc_currency"] + " instead. Aborting.");
                    debugStringDict (postvals);
                    return reply;
                }
                
                // At this point, the user has paid, paid a correct amount, in the correct currency.
                // Time to deliver their items. Do it in a seperate thread, so we can return "OK" to PP.
                Util.FireAndForget (delegate { TransferSuccess (txn); });
            } catch (KeyNotFoundException) {
                m_log.Error ("[FreeMoney] Received badly formatted IPN notice. Aborting.");
                debugStringDict (postvals);
                return reply;
            }
            // Wheeeee
            
            return reply;
        }

        #endregion


        #region Implementation of IRegionModuleBase

        public string Name {
            get { return "FreeMoneyModule"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public void Initialise (IConfigSource source)
        {
            m_log.Info ("[FreeMoney] Initialising.");
            m_config = source;

            IConfig config = m_config.Configs["FreeMoney"];
            
            if (null == config) {
                m_log.Warn ("[FreeMoney] No configuration specified. Skipping.");
               return;
            }
            
            if (!config.GetBoolean ("Enabled", false))
            {
                m_log.Info ("[FreeMoney] Not enabled. (to enable set \"Enabled = true\" in [FreeMoney])");
                return;
            }

            m_allowPayPal = config.GetBoolean("AllowPayPal", false);
            m_allowBitcoin = config.GetBoolean("AllowBitcoin", true);

            if (!m_allowBitcoin && !m_allowPayPal) {
                m_log.Warn ("[FreeMoney] No payment methods permitted - PayPal and Bitcoin both disabled. Skipping.");
                return;
            }

            // If we use PayPal, we need a page allowing the user to start a PayPal transaction or choose a Bitcoin one.
            // If we only do Bitcoin, we can skip that page and take them straight to a Bitcoin transaction.
            m_directToBitcoin = ( m_allowBitcoin && !m_allowPayPal );

            m_ppurl = config.GetString ("PayPalURL", m_ppurl);
            m_ppprotocol = config.GetString ("PayPalProtocol", m_ppprotocol);
            m_pprequesturi = config.GetString ("PayPalRequestURI", m_pprequesturi);

            m_btcurl = config.GetString ("BitcoinURL", m_btcurl);
            m_btcprotocol = config.GetString ("BitcoinProtocol", m_btcprotocol);
            m_btcrequesturi = config.GetString ("BitcoinRequestURI", m_btcrequesturi);

            m_allowGridEmails = config.GetBoolean ("AllowGridEmails", false);
            m_allowGroups = config.GetBoolean ("AllowGroups", false);
            m_balanceOnEntry = config.GetBoolean ("BalanceOnEntry", true);
            m_messageOnEntry = config.GetString ("MessageOnEntry", m_messageOnEntry);
            
            IConfig startupConfig = m_config.Configs["Startup"];


            if (startupConfig != null)
            {
                m_enabled = (startupConfig.GetString("economymodule", "FreeMoneyModule") == "FreeMoneyModule");

                if (!m_enabled) {
                    m_log.Info ("[FreeMoney] Not enabled. (to enable set \"economymodule = FreeMoneyMoneyModule\" in [Startup])");
                    return;
                }
            }

            IConfig economyConfig = m_config.Configs["Economy"];

            if (economyConfig != null)
            {
                PriceEnergyUnit = economyConfig.GetInt("PriceEnergyUnit", 100);
                PriceObjectClaim = economyConfig.GetInt("PriceObjectClaim", 10);
                PricePublicObjectDecay = economyConfig.GetInt("PricePublicObjectDecay", 4);
                PricePublicObjectDelete = economyConfig.GetInt("PricePublicObjectDelete", 4);
                PriceParcelClaim = economyConfig.GetInt("PriceParcelClaim", 1);
                PriceParcelClaimFactor = economyConfig.GetFloat("PriceParcelClaimFactor", 1f);
                PriceUpload = economyConfig.GetInt("PriceUpload", 0);
                PriceRentLight = economyConfig.GetInt("PriceRentLight", 5);
                TeleportMinPrice = economyConfig.GetInt("TeleportMinPrice", 2);
                TeleportPriceExponent = economyConfig.GetFloat("TeleportPriceExponent", 2f);
                EnergyEfficiency = economyConfig.GetFloat("EnergyEfficiency", 1);
                PriceObjectRent = economyConfig.GetFloat("PriceObjectRent", 1);
                PriceObjectScaleFactor = economyConfig.GetFloat("PriceObjectScaleFactor", 10);
                PriceParcelRent = economyConfig.GetInt("PriceParcelRent", 1);
                PriceGroupCreate = economyConfig.GetInt("PriceGroupCreate", -1);
            }

            m_log.Info ("[FreeMoney] Getting btc config.");

            m_gridCurrencyCode = config.GetString("GridCurrencyCode", "USD");
            m_gridCurrencyText = config.GetString("GridCurrencyText", "US$");

            m_gridCurrencySmallDenominationText  = config.GetString("GridCurrencySmallDenominationText", "US$ cents");
            m_gridCurrencySmallDenominationFraction = Convert.ToDecimal(config.GetFloat("GridCurrencySmallDenominationFraction", 100.0f));

            m_btcNumberOfConfirmationsRequired = config.GetInt("BitcoinNumberOfConfirmations", 0);

            m_btcconfig.Add("bitcoin_external_url", config.GetString ("BitcoinServerExternalURL", ""));

            //string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;
            m_log.Info ("[FreeMoney] Got btc config.");

            m_log.Info ("[FreeMoney] Loaded.");
            
            m_enabled = true;
        }

        public void PostInitialise ()
        {

        }

        public void Close ()
        {
            m_active = false;
        }

        public void AddRegion (Scene scene)
        {
            lock (m_scenes)
                m_scenes.Add (scene);
            
            if (m_enabled) {
                m_log.Info ("[FreeMoney] Found Scene.");

                scene.RegisterModuleInterface<IMoneyModule> (this);
                IHttpServer httpServer = MainServer.Instance;
                
                lock (m_scenel)
                {
                    if (m_scenel.Count == 0)
                    {
                        // XMLRPCHandler = scene;
                        
                        // To use the following you need to add:
                        // -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
                        // to the command line parameters you use to start up your client
                        // This commonly looks like -helperuri http://127.0.0.1:9000/
                        
                        // Local Server..  enables functionality only.
                        httpServer.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                        httpServer.AddXmlRPCHandler("buyCurrency", buy_func);
                        httpServer.AddXmlRPCHandler ("preflightBuyLandPrep", preflightBuyLandPrep_func);
                        httpServer.AddXmlRPCHandler ("buyLandPrep", landBuy_func);
                    }
                    
                    if (m_scenel.ContainsKey (scene.RegionInfo.RegionHandle))
                    {
                        m_scenel[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_scenel.Add (scene.RegionInfo.RegionHandle, scene);
                    }
                }
                
                scene.EventManager.OnNewClient += OnNewClient;
                //scene.EventManager.OnMakeRootAgent += MakeRootAgent;
                scene.EventManager.OnMoneyTransfer += OnMoneyTransfer;
                scene.EventManager.OnValidateLandBuy += ValidateLandBuy;
                scene.EventManager.OnLandBuy += processLandBuy;
            }
        }

        #region Basic Plumbing of Currency Events
        public void EconomyDataRequestHandler(IClientAPI user)
        {
            Scene s = (Scene)user.Scene;

            user.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                 PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                 PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                 TeleportMinPrice, TeleportPriceExponent);
        }
        private void OnNewClient(IClientAPI client)
        {
            // Subscribe to Money messages
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
            client.OnRequestPayPrice += requestPayPrice;
            client.OnObjectBuy += ObjectBuy;
        }

        /*private void EconomyDataRequestHandler(IClientAPI client)
        {
            throw new NotImplementedException();
        }*/

        /// <summary>
        /// Event Handler for when a root agent becomes a root agent
        /// </summary>
        /// <param name="avatar"></param>
       private void MakeRootAgent(ScenePresence avatar)
        {
            IClientAPI client = avatar.ControllingClient;

            if (m_balanceOnEntry)
            {
                client.SendMoneyBalance(UUID.Random(), true, new byte[0], m_maxBalance);

                if (m_messageOnEntry != "")
                    SendEntryMessage(client);
            }
        }

        /// <summary>
        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }
            return null;
        }

        internal Scene LocateSceneClientIn (UUID agentID)
        {
            ScenePresence avatar = null;
            
            foreach (Scene scene in m_scenes)
            {
                if (scene.TryGetScenePresence (agentID, out avatar))
                {
                    if (!avatar.IsChildAgent)
                    {
                        return avatar.Scene;
                    }
                }
            }
            
            return null;
        }

        public void ObjectBuy (IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID groupID,
                               UUID categoryID, uint localID, byte saleType, int salePrice)
        {
            if (!m_active)
                return;
            
            IClientAPI user = null;
            Scene scene = null;
            
            // Find the user's controlling client.
            lock (m_scenes) {
                foreach (Scene sc in m_scenes) {
                    ScenePresence av = sc.GetScenePresence (agentID);
                    
                    if ((av != null) && (av.IsChildAgent == false)) {
                        // Found the client,
                        // and their root scene.
                        user = av.ControllingClient;
                        scene = sc;
                    }
                }
            }
            
            if (scene == null || user == null) {
                m_log.Warn ("[FreeMoney] Unable to find scene or user! Aborting transaction.");
                return;
            }
            
            if (salePrice == 0) {
                IBuySellModule module = scene.RequestModuleInterface<IBuySellModule> ();
                if (module == null) {
                    m_log.Error ("[FreeMoney] Missing BuySellModule! Transaction failed.");
                    return;
                }
                module.BuyObject (remoteClient, categoryID, localID, saleType, salePrice);
                return;
            }
            
            SceneObjectPart sop = scene.GetSceneObjectPart (localID);
            if (sop == null) {
                m_log.Error ("[FreeMoney] Unable to find SceneObjectPart that was paid. Aborting transaction.");
                return;
            }
            
            string email = "";
            
            if (m_allowPayPal) {
                if (sop.OwnerID == sop.GroupID) {
                    if (m_allowGroups) {
                        if (!GetEmail (scene.RegionInfo.ScopeID, sop.OwnerID, out email)) {
                            m_log.Warn ("[FreeMoney] Unknown email address of group " + sop.OwnerID);
                            if (!m_allowBitcoin) {
                                return;
                            }
                        }
                    } else {
                        m_log.Warn ("[FreeMoney] Purchase of group owned objects is disabled.");
                        if (!m_allowBitcoin) {
                            return;
                        }
                    }
                } else {
                    if (!GetEmail (scene.RegionInfo.ScopeID, sop.OwnerID, out email)) {
                        m_log.Warn ("[FreeMoney] Unknown email address of user " + sop.OwnerID);
                        if (!m_allowBitcoin) {
                            return;
                        }
                    }
                }
            }
            
            m_log.Info ("[FreeMoney] Start: " + agentID + " wants to buy object " + sop.UUID + " from " + sop.OwnerID +
                        " with email " + email + " costing " + m_gridCurrencySmallDenominationText + " " + salePrice);
            
            FreeMoneyTransaction txn = new FreeMoneyTransaction (agentID, sop.OwnerID, email, salePrice, scene, sop.UUID,
                                                           "Item Purchase - " + sop.Name + " (" + saleType + ")",
                                                           FreeMoneyTransaction.InternalTransactionType.Purchase,
                                                           categoryID, saleType);
            
            // Add transaction to queue
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Add (txn.TxID, txn);
            
            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            // Talk to the address-handling server to initialize the transaction
            if (!CommunicateBitcoinTransaction(txn, baseUrl)) {
                m_log.Warn ("[FreeMoney] Attempt to set up transaction failed.");
                return;
            }

            user.SendLoadURL ("FreeMoney", txn.ObjectID, txn.To, false, "Confirm payment?", m_btcprotocol + "://" + m_btcurl + m_btcrequesturi + "?txn=" + txn.TxID);
        }

        // This will send the transaction to the payment handling server
        // Traditional PayPal flow would pass this information as parameters
        // Doing it this way allows us to do the transaction without revealing the email address of the seller to the buyer
        private bool CommunicateBitcoinTransaction(FreeMoneyTransaction txn, string baseUrl) {

            // Hard-coding this for now - may end up building everything into mono, in which case it will go away.
            string bitcoin_server =  m_btcprotocol + "://" + m_btcurl + m_btcrequesturi;


            IUserAccountService userAccountService = m_scenes[0].UserAccountService;
            UserAccount ua;
            
            // Notify receiver
            ua = userAccountService.GetUserAccount (txn.From, "", "");
            string first = "";
            if (ua != null) {
                first = ua.FirstName;
            }
            //string first = ua.FirstName;
 

            string post_data = ""
            + "cmd=initialize_transaction"
            + "&payee"           +HttpUtility.HtmlEncode (txn.To.ToString())
            + "&business="       +HttpUtility.HtmlEncode (txn.SellersEmail)
            + "&item_name="     +HttpUtility.HtmlEncode (txn.Description)
            + "&item_number="   +HttpUtility.HtmlEncode (txn.TxID.ToString ())
            + "&amount="        +HttpUtility.HtmlEncode (String.Format ("{0:0.00}", ConvertAmountToCurrency (txn.Amount)))
            + "&notify_url="    +HttpUtility.HtmlEncode ("http://" + baseUrl + "/btcipn/") // TODO: Deal with passing the PayPal details
            + "&currency_code=" +HttpUtility.HtmlEncode (m_gridCurrencyCode)
            + "&sim_base_url="  +HttpUtility.HtmlEncode ("http://" + baseUrl)
            + "&payee_avatar_uuid="+HttpUtility.HtmlEncode ( txn.To.ToString() )
            + "&payer_avatar_uuid="+HttpUtility.HtmlEncode ( txn.From.ToString() )
            + "&payee_avatar_grid="+HttpUtility.HtmlEncode ( baseUrl ) // TODO: These probably need to change for hypergrid
            + "&payer_avatar_grid="+HttpUtility.HtmlEncode ( baseUrl ) // TODO: These probably need to change for hypergrid
            + "&payer_avatar_first="+HttpUtility.HtmlEncode ( first ) // TODO: These probably need to change for hypergrid
            + "&confirm_url="    +HttpUtility.HtmlEncode ("http://" + baseUrl + "/btcconfirm/") 
            + "";
           

/*
            + "&payee_avatar_first="+HttpUtility.HtmlEncode ( toUA.FirstName ) // TODO: These probably need to change for hypergrid
            + "&payer_avatar_first="+HttpUtility.HtmlEncode ( fromUA.FirstName ) // TODO: These probably need to change for hypergrid
            + "&payee_avatar_last="+HttpUtility.HtmlEncode ( toUA.LastName ) // TODO: These probably need to change for hypergrid
            + "&payer_avatar_last="+HttpUtility.HtmlEncode ( fromUA.LastName ) // TODO: These probably need to change for hypergrid
*/
                        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create (bitcoin_server);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = post_data.Length;

            StreamWriter streamWriter = new StreamWriter (httpWebRequest.GetRequestStream (), System.Text.Encoding.ASCII);
            streamWriter.Write (post_data);
            streamWriter.Close ();
            
            string response;
            
            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse ();
            using (StreamReader streamReader = new StreamReader (httpWebResponse.GetResponseStream ())) {
                response = streamReader.ReadToEnd ();
                streamReader.Close ();
            }
            
                m_log.Error ("response: "+response);
            if (httpWebResponse.StatusCode != HttpStatusCode.OK) {
                m_log.Error ("[FreeMoney] Bitcoin transaction initialzation != 200. Aborting.");
                return false;
            }

            return true;

        }

        public void requestPayPrice (IClientAPI client, UUID objectID)
        {
            Scene scene = LocateSceneClientIn (client.AgentId);
            if (scene == null)
                return;
            
            SceneObjectPart task = scene.GetSceneObjectPart (objectID);
            if (task == null)
                return;
            SceneObjectGroup @group = task.ParentGroup;
            SceneObjectPart root = @group.RootPart;
            
            client.SendPayPrice (objectID, root.PayPrice);
        }

        /// <summary>
        /// Event called Economy Data Request handler.
        /// </summary>
        /// <param name="agentId"></param>
        public void EconomyDataRequestHandler(UUID agentId)
        {
            IClientAPI user = LocateClientObject(agentId);

            if (user != null)
            {
                Scene s = LocateSceneClientIn(user.AgentId);

                user.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                     PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                     PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                     TeleportMinPrice, TeleportPriceExponent);
            }
        }

        void OnMoneyBalanceRequest (IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            if (client.AgentId == agentID && client.SessionId == SessionID && (client == LocateClientObject(agentID)))
            {
                client.SendMoneyBalance(TransactionID, true, new byte[0], m_maxBalance, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
            
                //client.SendMoneyBalance (TransactionID, true, new byte[0], m_maxBalance);
            }
        }

        private void ValidateLandBuy (Object osender, EventManager.LandBuyArgs e)
        {
            // confirm purchase of land for free
            if (e.parcelPrice == 0) {
                lock (e) {
                    e.economyValidated = true;
                }
            }
        }


       
        private void processLandBuy (Object osender, EventManager.LandBuyArgs e)
        {
            if (!m_active)
                return;
            
            if (e.parcelPrice == 0)
                return;
            
            IClientAPI user = null;
            Scene scene = null;
            
            // Find the user's controlling client.
            lock (m_scenes) {
                foreach (Scene sc in m_scenes) {
                    ScenePresence av = sc.GetScenePresence (e.agentId);
                    
                    if ((av != null) && (av.IsChildAgent == false)) {
                        // Found the client,
                        // and their root scene.
                        user = av.ControllingClient;
                        scene = sc;
                    }
                }
            }
            
            if (scene == null || user == null) {
                m_log.Error ("[FreeMoney] Unable to find scene or user! Aborting transaction.");
                return;
            }
            
            string email;
            
            if ((e.parcelOwnerID == e.groupId) || e.groupOwned) {
                if (m_allowGroups) {
                    if (!GetEmail (scene.RegionInfo.ScopeID, e.parcelOwnerID, out email)) {
                        m_log.Warn ("[FreeMoney] Unknown email address of group " + e.parcelOwnerID);
                        return;
                    }
                } else {
                    m_log.Warn ("[FreeMoney] Purchases of group owned land is disabled.");
                    return;
                }
            } else {
                if (!GetEmail (scene.RegionInfo.ScopeID, e.parcelOwnerID, out email)) {
                    m_log.Warn ("[FreeMoney] Unknown email address of user " + e.parcelOwnerID);
                    return;
                }
            }
            
            m_log.Info ("[FreeMoney] Start: " + e.agentId + " wants to buy land from " + e.parcelOwnerID +
                        " with email " + email + " costing " + m_gridCurrencySmallDenominationText +" " + e.parcelPrice);
            
            FreeMoneyTransaction txn;
            txn = new FreeMoneyTransaction (e.agentId, e.parcelOwnerID, email, e.parcelPrice, scene,
                                         "Buy Land", FreeMoneyTransaction.InternalTransactionType.Land, e);
            
            // Add transaction to queue
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Add (txn.TxID, txn);
            
            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            // Talk to the address-handling server to initialize the transaction
            if (!CommunicateBitcoinTransaction(txn, baseUrl)) {
                m_log.Warn ("[FreeMoney] Attempt to set up transaction failed.");
                return;
            }
            
            user.SendLoadURL ("FreeMoney", txn.ObjectID, txn.To, false, "Confirm payment?", m_btcprotocol + "://" + m_btcurl + m_btcrequesturi + "?txn=" + txn.TxID);

        }

        public XmlRpcResponse preflightBuyLandPrep_func (XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse ret = new XmlRpcResponse ();
            Hashtable retparam = new Hashtable ();
            Hashtable membershiplevels = new Hashtable ();
            ArrayList levels = new ArrayList ();
            Hashtable level = new Hashtable ();
            level.Add ("id", "00000000-0000-0000-0000-000000000000");
            level.Add ("description", "some level");
            levels.Add (level);
            //membershiplevels.Add("levels",levels);
            
            Hashtable landuse = new Hashtable ();
            landuse.Add ("upgrade", false);
            landuse.Add ("action", "http://invaliddomaininvalid.com/");
            
            Hashtable currency = new Hashtable ();
            currency.Add ("estimatedCost", 0);
            
            Hashtable membership = new Hashtable ();
            membershiplevels.Add ("upgrade", false);
            membershiplevels.Add ("action", "http://invaliddomaininvalid.com/");
            membershiplevels.Add ("levels", membershiplevels);
            
            retparam.Add ("success", true);
            retparam.Add ("currency", currency);
            retparam.Add ("membership", membership);
            retparam.Add ("landuse", landuse);
            retparam.Add ("confirm", "asdfajsdkfjasdkfjalsdfjasdf");
            
            ret.Value = retparam;
            
            return ret;
        }

        public XmlRpcResponse landBuy_func (XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse ret = new XmlRpcResponse ();
            Hashtable retparam = new Hashtable ();
            
            retparam.Add ("success", true);
            ret.Value = retparam;
            
            return ret;
        }

        // Return a money-server-specific session key for the user
        // This can be used for the address-handling server to confirm that the user is who they claim to be...
        // ...if they want to register a Bitcoin address and tie it to their avatar.
        // ...creating it and adding it to m_usersessionkey in the process if it doesn't already exist.
        // TODO: Figure out if this dictionary is supposed to be locked or something.
        private UUID GetSessionKey(UUID userkey)
        {
            if (m_usersessionkey.ContainsKey(userkey)) {
                return m_usersessionkey[userkey];
            }

            byte[] randomBuf = new byte[16];
            RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
            random.GetBytes(randomBuf);
            Guid sID = new Guid(randomBuf);

            UUID userUUID = new UUID(sID);

            m_usersessionkey.Add(userkey, userUUID);
            m_sessionkeyuser.Add(userUUID, userkey);

            return userUUID;

        }

        private bool GetEmail (UUID scope, UUID key, out string email)
        {
            if (m_usersemail.TryGetValue (key, out email))
                return !string.IsNullOrEmpty (email);
            
            if (!m_allowGridEmails)
                return false;
            
            m_log.Info ("[FreeMoney] Fetching email address from grid for " + key);
            
            IUserAccountService userAccountService = m_scenes[0].UserAccountService;
            UserAccount ua;
            
            ua = userAccountService.GetUserAccount (scope, key);
            
            if (ua == null)
                return false;
            
            if (string.IsNullOrEmpty (ua.Email))
                return false;
            
            // return email address found and cache it
            email = ua.Email;
            m_usersemail[ua.PrincipalID] = email;
            return true;
        }

        private void SendInstantMessage (UUID dest, string message)
        {
            IClientAPI user = null;
            
            // Find the user's controlling client.
            lock (m_scenes) {
                foreach (Scene sc in m_scenes) {
                    ScenePresence av = sc.GetScenePresence (dest);
                    
                    if ((av != null) && (av.IsChildAgent == false)) {
                        // Found the client,
                        // and their root scene.
                        user = av.ControllingClient;
                    }
                }
            }
            
            if (user == null)
                return;
            
            UUID transaction = UUID.Random ();
            
            GridInstantMessage msg = new GridInstantMessage ();
            msg.fromAgentID = new Guid (UUID.Zero.ToString ());
            // From server
            msg.toAgentID = new Guid (dest.ToString ());
            msg.imSessionID = new Guid (transaction.ToString ());
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch ();
            msg.fromAgentName = "FreeMoney";
            msg.dialog = (byte)19;
            // Object msg
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = (uint)0;
            msg.Position = Vector3.Zero;
            msg.RegionID = new Guid (UUID.Zero.ToString ());
            msg.binaryBucket = new byte[0];
            msg.message = message;
            
            user.SendInstantMessage (msg);
        }

        private void SendEntryMessage(IClientAPI client)
        {
            GridInstantMessage msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = UUID.Zero.Guid;
            msg.toAgentID = client.AgentId.Guid;
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.fromAgentName = "System";
            msg.message = m_messageOnEntry;
            msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.ConsoleAndChatHistory;
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = UUID.Zero.Guid;
            msg.binaryBucket = new byte[0];

            client.SendInstantMessage(msg);
        }

        #endregion

        public void RemoveRegion (Scene scene)
        {
            lock (m_scenes)
                m_scenes.Remove (scene);
            
            if (m_enabled)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                //scene.EventManager.OnMakeRootAgent -= MakeRootAgent;
                scene.EventManager.OnMoneyTransfer -= OnMoneyTransfer;
                scene.EventManager.OnValidateLandBuy -= ValidateLandBuy;
                scene.EventManager.OnLandBuy -= processLandBuy;
            }
        }

        public void RegionLoaded (Scene scene)
        {
            if (m_enabled)
            {
                lock (m_setupLock)
                    if (m_setup == false) {
                        m_setup = true;
                        FirstRegionLoaded ();
                    }
            }
        }

        public void FirstRegionLoaded ()
        {
            m_log.Info ("[FreeMoney] Loading predefined users and groups.");

            // Users
            IConfig users = m_config.Configs["PayPal Users"];
            
            if (null == users) {
                m_log.Warn ("[FreeMoney] No users specified in local ini file.");
            } else {
                IUserAccountService userAccountService = m_scenes[0].UserAccountService;
                
                // This aborts at the slightest provocation
                // We realise this may be inconvenient for you,
                // however it is important when dealing with
                // financial matters to error check everything.
                
                foreach (string user in users.GetKeys ()) {
                    UUID tmp;
                    if (UUID.TryParse (user, out tmp)) {
                        m_log.Debug ("[FreeMoney] User is UUID, skipping lookup...");
                        string email = users.GetString (user);
                        m_usersemail[tmp] = email;
                        continue;
                    }
                    
                    m_log.Debug ("[FreeMoney] Looking up UUID for user " + user);
                    string[] username = user.Split (new[] { ' ' }, 2);
                    UserAccount ua = userAccountService.GetUserAccount (UUID.Zero, username[0], username[1]);
                    
                    if (ua != null) {
                        m_log.Debug ("[FreeMoney] Found user, " + user + " = " + ua.PrincipalID);
                        string email = users.GetString (user);
                        
                        if (string.IsNullOrEmpty (email)) {
                            m_log.Error ("[FreeMoney] FreeMoney email address not set for user " + user +
                                         " in [FreeMoney Users] config section. Skipping.");
                            m_usersemail[ua.PrincipalID] = "";
                        } else {
                            if (!FreeMoneyHelpers.IsValidEmail (email)) {
                                m_log.Error ("[FreeMoney] FreeMoney email address not valid for user " + user +
                                             " in [FreeMoney Users] config section. Skipping.");
                                m_usersemail[ua.PrincipalID] = "";
                            } else {
                                m_usersemail[ua.PrincipalID] = email;
                            }
                        }
                    // UserProfileData was null
                    } else {
                        m_log.Error ("[FreeMoney] Error, User Profile not found for user " + user +
                                     ". Check the spelling and/or any associated grid services.");
                    }
                }
            }
            
            // Groups
            IConfig groups = m_config.Configs["PayPal Groups"];
            
            if (!m_allowGroups || null == groups) {
                m_log.Warn ("[FreeMoney] Groups disabled or no groups specified in local ini file.");
            } else {
                // This aborts at the slightest provocation
                // We realise this may be inconvenient for you,
                // however it is important when dealing with
                // financial matters to error check everything.
                
                foreach (string @group in groups.GetKeys ()) {
                    m_log.Debug ("[FreeMoney] Defining email address for UUID for group " + @group);
                    UUID groupID = new UUID (@group);
                    string email = groups.GetString (@group);
                    
                    if (string.IsNullOrEmpty (email)) {
                        m_log.Error ("[FreeMoney] FreeMoney email address not set for group " +
                                     @group + " in [FreeMoney Groups] config section. Skipping.");
                        m_usersemail[groupID] = "";
                    } else {
                        if (!FreeMoneyHelpers.IsValidEmail (email)) {
                            m_log.Error ("[FreeMoney] FreeMoney email address not valid for group " +
                                         @group + " in [FreeMoney Groups] config section. Skipping.");
                            m_usersemail[groupID] = "";
                        } else {
                            m_usersemail[groupID] = email;
                        }
                    }
                }
            }
            
            // Add HTTP Handlers (user, then PP-IPN)
            MainServer.Instance.AddHTTPHandler ("/ppipn/", PayPalIPN);
            MainServer.Instance.AddHTTPHandler ("/btcipn/", BitcoinIPN); 

            // Allow the Bitcoin server to ask us to confirm the email and UUID of the buyer.
            MainServer.Instance.AddHTTPHandler ("/btcconfirm/", BitcoinConfirmAvatarInfo); 
            
            // XMLRPC Handlers for Standalone
            MainServer.Instance.AddXmlRPCHandler ("getCurrencyQuote", quote_func);
            MainServer.Instance.AddXmlRPCHandler ("buyCurrency", buy_func);
            
            m_active = true;
        }

        #endregion

        #region Implementation of IMoneyModule

        public bool ObjectGiveMoney (UUID objectID, UUID fromID, UUID toID, int amount)
        {
            return false;
            // Objects cant give PP Money. (in theory it's doable however, if the user is in the sim.)
        }

        // This will be the maximum amount the user
        // is able to spend due to client limitations.
        // It is set to the equivilent of US$10K
        // as this is FreeMoney's maximum transaction
        // size.
        //
        // This is 1 Million cents.
        public int GetBalance (UUID agentID)
        {
            return m_maxBalance;
        }

        public bool UploadCovered (UUID agentID, int amount)
        {
            return true;
        }

        public bool AmountCovered (UUID agentID, int amount)
        {
            return true;
        }

        public void ApplyCharge (UUID agentID, int amount, string text)
        {
            // N/A
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string extraData)
        {
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type)
        {
        }
        public void ApplyUploadCharge (UUID agentID, int amount, string text)
        {
            // N/A
        }

        public int UploadCharge {
            get { return 0; }
        }

        public int GroupCreationCharge {
            get { return 0; }
        }

        public event ObjectPaid OnObjectPaid;

        #endregion

        #region Some Quick Funcs needed for the client

        public XmlRpcResponse quote_func (XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Hashtable requestData = (Hashtable) request.Params[0];
            // UUID agentId = UUID.Zero;
            Hashtable quoteResponse = new Hashtable ();
            XmlRpcResponse returnval = new XmlRpcResponse ();
            
            
            Hashtable currencyResponse = new Hashtable ();
            currencyResponse.Add ("estimatedCost", 0);
            currencyResponse.Add ("currencyBuy", m_maxBalance);
            
            quoteResponse.Add ("success", false);
            quoteResponse.Add ("currency", currencyResponse);
            quoteResponse.Add ("confirm", "asdfad9fj39ma9fj");
            
            returnval.Value = quoteResponse;
            return returnval;
        }

        public XmlRpcResponse buy_func (XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse returnval = new XmlRpcResponse ();
            Hashtable returnresp = new Hashtable ();
            returnresp.Add ("success", true);
            returnval.Value = returnresp;
            return returnval;
        }
        
        #endregion
        

    }

}
