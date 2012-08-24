<?php

/*
array(15) { ["cmd"]=> string(7) "_xclick" ["business"]=> string(18) "online@edochan.com" ["item_name"]=> string(29) "Item Purchase - Primitive (2)" ["item_number"]=> string(36) "fb723587-e734-49e3-2f11-6312b75ecff6" ["amount"]=> string(4) "0.10" ["page_style"]=> string(6) "Paypal" ["no_shipping"]=> string(1) "1" ["return"]=> string(25) "http://192.168.1.80:9000/" ["cancel_return"]=> string(25) "http://192.168.1.80:9000/" ["notify_url"]=> string(31) "http://192.168.1.80:9000/ppipn/" ["no_note"]=> string(1) "1" ["currency_code"]=> string(3) "USD" ["lc"]=> string(2) "US" ["bn"]=> string(11) "PP-BuyNowBF" ["charset"]=> string(5) "UTF-8" } 
*/

// Get a database mysqliection or give up.

require_once(dirname(__FILE__).'/config.php');

$mysqli = new mysqli(OPENSIM_BITCOIN_DB_HOST, OPENSIM_BITCOIN_DB_USER,  OPENSIM_BITCOIN_DB_PASSWORD, OPENSIM_BITCOIN_DB_NAME);
if ($mysqli->mysqliect_errno) {
	echo "Failed to mysqliect to MySQL: (" . $mysqli->mysqliect_errno . ") " . $mysqli->mysqliect_error;
	exit;
}

$cmd = isset($_REQUEST['cmd']) ? $_REQUEST['cmd'] : '';
if ($cmd == '') {
	$content = file_get_contents("php://input");
	parse_str($content, $params);
	$cmd = $params['cmd'];
}

error_log("request for :".$_REQUEST['cmd'].':');
error_log(join('--', array_keys($_REQUEST)));

// Notify request from the payment confirmer
if ( $cmd == 'notify' ) {

	handle_completion_ping($mysqli);

} else if ( $cmd == 'initialize_transaction' ) {
	// TODO: Handle a notification directly from the OpenSim server.
	initialize_transaction($mysqli);

} else if ( $cmd == '_notify-validate') {

	handle_notify_validate($mysqli, $params);

} else if ( $cmd == 'show_address_page' ) {
	
	show_address_page($mysqli, $_GET['btc_session_id']);

} else if ( $cmd == 'process_address_page' ) {

	process_address_page($mysqli);
	
} else if ( $cmd == '_xclick') {

	// Request from a user to start a transaction and show them a webpage
	// This step will probably be removed and replaced with a request directly from the server.	
	show_payment_web_page($mysqli);

} else {

	print_simple_and_exit("No cmd specified ($cmd) ".file_get_contents("php://input")."::");

}

function show_address_page($mysqli, $btc_session_id) {

//payment-server/webroot/templates/bitcoin-register-template.htm  payment-server/webroot/templates/bitcoin-template.htm
	$count_all_addresses = 0;
	$count_usable_addresses = 0;

	$page = file_get_contents(dirname(__FILE__).'/templates/bitcoin-register-template.htm');
	$page = str_replace('{BTC_SESSION_ID}', htmlentities($btc_session_id), $page);
	$page = str_replace('{BTC_SIM_BASE_URL}', htmlentities($_REQUEST['sim_base_url']), $page);

	$page = str_replace('{BTC_DISABLE_INCOMPLETE_START}', '<!--', $page);
	$page = str_replace('{BTC_DISABLE_INCOMPLETE_END}', '-->', $page);

	$page = str_replace('{BTC_DISABLE_COMPLETE_START}', '', $page);
	$page = str_replace('{BTC_DISABLE_COMPLETE_END}', '', $page);

	$page = str_replace('{BTC_COUNT_NEW_ADDRESSES}', 0, $page);

	$page = str_replace('{BTC_COUNT_ALL_ADDRESSES}', htmlentities($count_all_addresses), $page);
	$page = str_replace('{BTC_COUNT_USABLE_ADDRESSES}', htmlentities($count_usable_addresses), $page);

	print $page;
	exit;

}

function initialize_transaction($mysqli) {

	$btc_transaction = new BitcoinTransaction($mysqli);

	// Better to use POST here.
	// For now let GET work as well in case we want to run this code using the unhacked money server with just the URL changed.
	//$params = $_POST;

	$content = file_get_contents("php://input");
	parse_str($content, $params);

	$btc_transaction->transaction_code = $params['item_number'];
	$btc_transaction->payee = $params['business'];
	$btc_transaction->item_name = $params['item_name'];
	$btc_transaction->original_amount = $params['amount'];
	$btc_transaction->original_currency_code = $params['currency_code'];
	$btc_transaction->notify_url = $params['notify_url'];
	$btc_transaction->num_confirmations_required = 0; // TODO: We may get this from the server

	if (!$btc_transaction->initialize()) {
		print_simple_and_exit("Error: I was unable to initialize this transaction.");
	}

	print_simple_and_exit("OK", 200);
	exit;

}

function show_payment_web_page($mysqli) {

	$btc_transaction = new BitcoinTransaction($mysqli);

	// Better to use POST here.
	// For now let GET work as well in case we want to run this code using the unhacked money server with just the URL changed.
	$params = isset($_POST['item_number']) ? $_POST : $_GET;

	$btc_transaction->transaction_code = $params['item_number'];
	$btc_transaction->payee = $params['business'];
	$btc_transaction->item_name = $params['item_name'];
	$btc_transaction->original_amount = $params['amount'];
	$btc_transaction->original_currency_code = $params['currency_code'];
	$btc_transaction->notify_url = $params['notify_url'];
	$btc_transaction->num_confirmations_required = 0; // TODO: We may get this from the server

	$btc_session_id = $_POST['btc_session_id'];

	if (!$btc_transaction->initialize()) {
		print_simple_and_exit("Error: I was unable to initialize this transaction.");
	}

	// TODO: Fetch a count of addresses
	$count_all_addresses = 10;
	$count_usable_addresses = 5;

	$page = file_get_contents(dirname(__FILE__).'/templates/bitcoin-pay-template.htm');
	$page = str_replace('{BTC_SESSION_ID}', htmlentities($btc_session_id), $page);
	$page = str_replace('{BTC_SIM_BASE_URL}', htmlentities($params['sim_base_url']), $page);
	$page = str_replace('{BTC_AMOUNT}', htmlentities($btc_transaction->btc_amount), $page);
	$page = str_replace('{BTC_ADDRESS}', htmlentities($btc_transaction->btc_address), $page);

	$page = str_replace('{BTC_COUNT_ALL_ADDRESSES}', htmlentities($count_all_addresses), $page);
	$page = str_replace('{BTC_COUNT_USABLE_ADDRESSES}', htmlentities($count_usable_addresses), $page);

	//echo "Please pay ".$btc_transaction->btc_amount." BTC to the address ".htmlentities($btc_transaction->btc_address);
	echo $page;
	exit;

}

function process_address_page($mysqli) {

	// TODO: Sort this out - need to get the address and verify it somehow...
	// originally was supposed to be avatars, currently using emails
	// should rename if we stick with email
	if (!$btc_session_id = $_POST['btc_session_id']) {
		print_simple_and_exit("Error: Session ID missing");
	}
	if (!$sim_base_url = $_POST['sim_base_url']) {
		print_simple_and_exit("Error: Sim base URL missing");
	}

	$email = BitcoinWebServiceClient::Http_response($sim_base_url.'/btcbuyerinfo/', "btc_session_id=".$btc_session_id, array());
	if (!$email) {
		print_simple_and_exit("Error: Could not find email - maybe logged out?");
	}

	$addresstext = $_POST['addresses'];
	$format = 'plain';
	$giveawaystr = '"Label","Address"';
	if (preg_match('/'.preg_quote($giveawaystr).'/', $addresstext)) {
		$format = 'csv';
	}

	$lines = explode("\n", $addresstext);
	$addresses = array();
	foreach($lines as $line) {
		$line = trim($line);
		if ($format == 'csv') {
			if (preg_match('/^.*?,\"(.*?)\"$/', $line, $matches)) {
				if ($matches[1] == 'Address') {
					continue;
				}
				$addresses[] = $matches[1];
			}
		} else {
			$addresses[] = $line;
		}
	}

	$addresses_created = array();
	$addresses_failed  = array();

	foreach($addresses as $address) {
		$bitcoin_address = new BitcoinAddress($mysqli);
		$bitcoin_address->btc_address = $address;
		$bitcoin_address->user_identifier = $email;
		// This may fail if it's a duplicate. 
		if ($bitcoin_address->insert()) {
			$addresses_created[] = $address;
		} else {
			$addresses_failed[] = $address;
		}
	}

	$count_all_addresses = 0;
	$count_usable_addresses = 0;

	$page = file_get_contents(dirname(__FILE__).'/templates/bitcoin-register-template.htm');
	$page = str_replace('{BTC_SESSION_ID}', htmlentities($btc_session_id), $page);

	$page = str_replace('{BTC_DISABLE_COMPLETE_START}', '<!--', $page);
	$page = str_replace('{BTC_DISABLE_COMPLETE_END}', '-->', $page);

	$page = str_replace('{BTC_DISABLE_INCOMPLETE_START}', '', $page);
	$page = str_replace('{BTC_DISABLE_INCOMPLETE_END}', '', $page);

	$page = str_replace('{BTC_COUNT_NEW_ADDRESSES}', count($addresses_created), $page);

	$page = str_replace('{BTC_COUNT_ALL_ADDRESSES}', htmlentities($count_all_addresses), $page);
	$page = str_replace('{BTC_COUNT_USABLE_ADDRESSES}', htmlentities($count_usable_addresses), $page);

	print $page;
	exit;

}

// This handle the request we get from the OpenSim server. After we send it a message to say that we got a payment.
// It's designed to imitate the request sent to PayPal.
function handle_notify_validate($mysqli, $params) {

	$transaction = new BitcoinTransaction($mysqli);
	$transaction->transaction_code = $params['item_number'];
	if (!$transaction->populate()) {
		print_simple_and_exit("Could not find transaction with code ".htmlentities($params['item_number']));
	}

	//$header = "HTTP/1.1 200";

	$body =  "status=VERIFIED";
	$body .= "&payment_status=Completed";
	$body .= "&mc_currency=USD";  // TODO: Sort this out. It should probably be configurable at the PayPal end.
	$body .= "&item_number=".$transaction->transaction_code; 
	$body .= "&mc_gross=".$transaction->btc_amount; 
	$body .= "&business=".$transaction->payee; 

	//header($header);
	print $body;
	exit;

}

// TODO: Make this able to handle different formats from different notification services.
function handle_completion_ping($mysqli) {

	if (!isset($_GET['service'])) {
		print_simple_and_exit("Error: Service name not set.");
		exit;
	}

	if (!$service = BitcoinNotificationService::ForServiceName($_GET['service'])) {
		print_simple_and_exit("Error: Service not recognized.");
		exit;
	}

	if (!$service->parseRequestBody()) {
		print_simple_and_exit("Error: Service did not provide a response in a form we could parse.");
		exit;
	}

	if (!$service->isValid()) {
		print_simple_and_exit("Error: Request claiming to be from service ".htmlentities($_GET['service'])." seems to be invalid.");
	}

	$address = $service->btc_address();	
	$transaction = new BitcoinTransaction($mysqli);
	$transaction->btc_address = $address;
	if (!$transaction->populate('btc_address')) {
		print_simple_and_exit("Error: Could not find transaction for address ".htmlentities($address));
	}

	// Always mark the latest number of confirmations, assuming it's more than we had last time.
	if (!$transaction->mark_confirmed($num_confirmations_received)) {
		print_simple_and_exit("Could not mark transaction confirmed despite receiving ".htmlentities($num_confirmations_received)." confirmations.");
	}

	// If it's less than we need, there should be nothing more to do.
	if ($service->num_confirmations_received() < $transaction->num_confirmations_required) {
		print_simple_and_exit("Not enough confirmations, ignoring.", 200);
	}

	// If we've already notified the client about this transaction, no need to do anything else.
	if ($transaction->confirmation_sent_ts > 0) {
		//print_simple_and_exit("Already confirmed, nothing more to do.", 200);
	}

	if (!$transaction->notifySim()) {
		print_simple_and_exit("Unable to notify sim.");
	}

	if (!$transaction->mark_notified()) {
		print_simple_and_exit("Notified sim, but unable to record the fact that we did.");
	}

	// Doesn't matter what we tell the ping service.
	print_simple_and_exit("OK, Thanks for letting me know. Keep up the good work.", 200);

}

class BitcoinAddress {

	// table opensim_btc.opensim_btc_addresses

	var $_mysqli = null; // A mysqli database mysqliection
	var $btc_address; // A Bitcoin address somebody can pay money to.
	var $user_identifier; // The avatar name.

	function BitcoinAddress($mysqli) {

		$this->_mysqli = $mysqli;

	}

	function insert() {

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		if (!$this->btc_address || !$this->user_identifier) {
			return false;
		}

		$query  = 'INSERT INTO opensim_btc_addresses(';
		$query .= 'btc_address, ';
		$query .= 'user_identifier ';
		$query .= ') values(?,?)';

		if (!$stmt = $mysqli->prepare($query))  {
			return false;	
		}

		$stmt->bind_param( 
			'ss', 
			$this->btc_address,
			$this->user_identifier
		);

		if (!$stmt->execute()) {
			//print $stmt->error();
			return false;
		}

		$stmt->close();
		return true;

	}

	function AddressForAvatar($av) {

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		if (!$av) {
			return false;
		}

		// Find an address for the avatar that isn't currently in use.
		$query = 'select a.btc_address from opensim_btc_addresses a left outer join opensim_btc_transactions t on a.btc_address=t.btc_address where a.user_identifier=? AND t.confirmation_sent_ts > 0 OR t.id IS NULL limit 1';

		if (!$stmt = $mysqli->prepare($query))  {
			return false;	
		}

		$stmt->bind_param( 
			's', 
			$av
		);

		if (!$stmt->execute()) {
			return false;
		}

		$stmt->bind_result(
			$address
		);
      
		if ($stmt->fetch()) {
			return $address;
		}

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

		return false;

	}

}

class BitcoinTransaction {

	var $_mysqli = null;

	var $payee; // The payee. In the PayPal world this would be "business", which is an email address. We will have to do some kind of lookup to get a Bitcoin address for this person.
	var $item_name; // The name of the object to be transferred, for humans.
	var $transaction_code; // The transaction ID. In the Paypay world it's called "item_number".
	var $original_amount; // The original amount requested in the original currency. If quoted in a non-BTC currency, we may convert it. In PayPal, "amount".
	var $original_currency_code; // The currency originally requested. In PayPal, "currency_code".
	var $btc_amount; // The BTC equivalent at the time when the payment address was given to the user.
	var $notify_url; // The URL we  should notify when the payment is confirmed.
	var $btc_address; // The address we gave the user to pay.
	var $num_confirmations_required; // The number of confirmations we are expected to wait for before considering the payment paid.
	var $created_ts; // A unix timestamp for the time we first heard about the transaction.
	var $payment_detected_ts; // A timestamp for the first time we saw the payment on the network. 
	var $num_confirmations_received; // The number of confirmations received, last time we checked.
	var $confirmation_sent_ts; // Atimestamp for the time we notified the server that payment was complete.

	function BitcoinTransaction($mysqli) {

		$this->_mysqli = $mysqli;

	}

	function populate($by = 'transaction_code') {

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		$query = 'SELECT payee, item_name, transaction_code, original_amount, btc_amount, notify_url, btc_address, num_confirmations_required, created_ts, payment_detected_ts, num_confirmations_required, confirmation_sent_ts FROM opensim_btc_transactions';
		$param = '';
		if ($by == 'transaction_code') {
			$query .= ' WHERE transaction_code=?';
			$param = $this->transaction_code;
		} else if ($by == 'btc_address') {
			$query .= ' WHERE btc_address=?';
			$param = $this->btc_address;
		} else {
			// not supported
			return false;
		}

		$stmt = $mysqli->prepare($query);
		$stmt->bind_param('s', $param);
		if (!$stmt->execute()) {
			return false;
		}

		$stmt->bind_result(
			$payee, 
			$item_name, 
			$transaction_code, 
			$original_amount, 
			$btc_amount, 
			$notify_url, 
			$btc_address, 
			$num_confirmations_required, 
			$created_ts, 
			$payment_detected_ts, 
			$num_confirmations_required, 
			$confirmation_sent_ts
		);
      
		if ($stmt->fetch()) {
			$this->payee = $payee;
			$this->item_name = $item_name;
			$this->transaction_code = $transaction_code;
			$this->original_amount = $original_amount;
			$this->original_currency_code = $original_currency_code;
			$this->btc_amount = $btc_amount;
			$this->notify_url = $notify_url;
			$this->btc_address = $btc_address;
			$this->num_confirmations_required = $num_confirmations_required;
			$this->created_ts = $created_ts;
			$this->payment_detected_ts = $payment_detected_ts;
			$this->num_confirmations_received = $num_confirmations_required;
			$this->confirmation_sent_ts = $confirmation_sent_ts;
			return true;
		}
		return false;

	}

	function to_btc($amount, $currency_code) {

		$btc = BitcoinExchangeRateService::ToBTC($amount, $currency_code);
		return $btc;

	}

	function create() {

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		$this->created_ts = time();

		if ($this->original_currency_code == 'BTC') {
			$this->btc_amount = $this->original_amount;
		} else {
			if (!$this->btc_amount = $this->to_btc($this->original_amount, $this->original_currency_code)) {
				//print "btc amount for ".$this->original_amount." ".$this->original_currency_code." is ".$this->btc_amount ;
				return false;
			}
		}

		if (!$this->btc_address = BitcoinAddress::AddressForAvatar($this->payee)) {
			return false;
		}

		$query  = 'INSERT INTO opensim_btc_transactions (';
		$query .= 'payee, ';
		$query .= 'item_name, ';
		$query .= 'transaction_code, ';
		$query .= 'original_amount, ';
		$query .= 'original_currency_code, ';
		$query .= 'btc_amount, ';
		$query .= 'notify_url, ';
		$query .= 'btc_address, ';
		$query .= 'num_confirmations_required, ';
		$query .= 'created_ts';
		$query .= ') values(?,?,?,?,?,?,?,?,?,?)';

		if (!$stmt = $mysqli->prepare($query))  {
			return false;	
		}

		$stmt->bind_param( 
			'sssdsdssii', 
			$this->payee,
			$this->item_name,
			$this->transaction_code,
			$this->original_amount,
			$this->original_currency_code,
			$this->btc_amount,
			$this->notify_url,
			$this->btc_address,
			$this->num_confirmations_required,
			$this->created_ts
		);

		if (!$stmt->execute()) {
			//print $stmt->error();
			return false;
		}

		$stmt->close();
		return true;

	}

	function mark_notified() {

		if (!$transaction_code = $this->transaction_code) {
			print_simple_and_exit("No transaction code set, could not mark notified");
			return false;
		}

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		// TODO: Handle amount received
		$query = "update opensim_btc_transactions set confirmation_sent_ts=? where confirmation_sent_ts = 0 and transaction_code=?";

		if (!$stmt = $mysqli->prepare($query))  {
			return false;	
		}

		$stmt->bind_param( 
			'is', 
			time(),
			$this->transaction_code
		);
			
		if (!$stmt->execute()) {
			return false;
		}

		$stmt->close();
		return true;

	}

	function mark_confirmed($num_confirmations_received) {

		if (!$transaction_code = $this->transaction_code) {
			print_simple_and_exit("No transaction code set, could not mark confirmed");
			return false;
		}

		if (!$mysqli = $this->_mysqli) {
			return false;
		}

		// TODO: Handle amount received
		$query = "update opensim_btc_transactions set num_confirmations_received=?, payment_detected_ts=? where num_confirmations_received<? and transaction_code=?";

		if (!$stmt = $mysqli->prepare($query))  {
			return false;	
		}

		$stmt->bind_param( 
			'idis', 
			$num_confirmations_received,
			time(),
			$num_confirmations_received,
			$this->transaction_code
		);
			
		if (!$stmt->execute()) {
			return false;
		}

		$stmt->close();
		return true;

	}

	function initialize() {

		if (!$this->_mysqli) {
			print_simple_and_exit( "No mysqli" );
			return false;
		}

		// If we already have an entry for the transaction id, return that.
		if (!$transaction_code = $this->transaction_code) {
			print_simple_and_exit("No transaction code, silly");
			return false;
		}	

		if ( !( $this->populate() || $this->create() ) ) {
			print_simple_and_exit("Could not populate or create");
			return false;
		}

		/*
		TODO: This sets up a subscription for the first service that's running.
		This will break if a service goes down between setting it up and handling the payment.
		We should probably either subscripe to multiple services or have some logic to resubscribe transactions if we don't hear anything.
		*/
		foreach( unserialize(OPENSIM_BITCOIN_PING_SERVICES) as $service_name => $service_settings) {
			$notification_service = BitcoinNotificationService::ForServiceName($service_name);
			if ($notification_service->subscribe($this->btc_address, $this->num_confirmations_required)) {
				return true;
			}
		}

		return false;
	
	}
	
	function notifySim() {

		if (!$url = $this->notify_url) {
			print_simple_and_exit("Cannot notify sim, notify URL not set");
		}

		if (!$transaction_code = $this->transaction_code) {
			print_simple_and_exit("Cannot notify sim, transaction code not set");
		}

		$body .= "item_number=".$transaction_code; 
		$body .= "&payment_status=Completed";
		$body .= "&mc_currency=USD";  // TODO: Sort this out. It should probably be configurable at the PayPal end.
		$body .= "&mc_gross=".$this->btc_amount; 
		$body .= "&business=".$this->payee; 

		if (!$response = BitcoinWebServiceClient::Http_response($url, $body, array())) {
			print_simple_and_exit("Could not contact sim to notify it.");
			return false;
		}
	
		return true;

	}

}



class BitcoinNotificationService {

	var $_service_name = null;
	var $_agent_id = null;

	var $_config;

	var $_body_json = null;
	/*
        array(
                'bitcoinmonitor' => array(
                        'url' => 'http://www.bitcoinmonitor.net/api/v1/agent/17/notification/url/',
                        'accesskey' => '8c673a5239d05f137e2fac4e3e3d0600be870afd',
                        'verificationkey' => 'cd65311804492d69cdaf896052e98fe442c0bac5'
                )
        )
	*/


	// Static method to return a BitcoinNotificationService object or subclass thereof.
	function ForServiceName($service_name) {

		// TODO: Check if there's a subclass defined for that service - if there is return that instead
		return new BitcoinNotificationService($service_name);

	}

	function parseRequestBody() {

		if (!$postdata = file_get_contents("php://input")) {
			return false;
		}

		// PHP too old?
		if (!function_exists('json_decode')) {
			return false;
		}

		$json = json_decode($postdata, true);
		if (!is_array($json)) {
			return false;
		}
		$this->_body_json = $json;

		return true;

	}

	function BitcoinNotificationService($service_name) {

		$configs = unserialize(OPENSIM_BITCOIN_PING_SERVICES);
		if (!isset($configs[$service_name])) {
			return false;
		}

		$this->_config = $configs[$service_name];
		$this->_service_name = $service_name;	

		return true;

	}

	function subscriptionURL() {

		if (!$agent_id = $this->_agent_id) {
			//print "Could not make subscription URL: no agent_id";
			return '';
		}

		if (!$base_url = $this->_config['base_url']) {
			//print "Could not find base_url";
			return '';
		}

		$url = $base_url.'/'.$agent_id.'/address/';

		return $url;

	}

	//$protocol = ( isset($_SERVER['HTTPS']) && ($_SERVER['HTTPS'] == 'on') ) ? 'https' : 'http';
	//$server_name = defined('OPENSIM_BITCOIN_EXTERNAL_URL') ? OPENSIM_BITCOIN_EXTERNAL_URL : $_SERVER['SERVER_NAME'];

	function subscribe($address, $confirmations) {

		if (!$address) {
			//print "Could not subscribe, address not set.";
			return false;
		}

		if (!$this->initializeAgent()) {
			//print "Could not initialize agent";
			return false;
		}

		if (!$url = $this->subscriptionURL()) {
			//print "Could not make URL";
			return false;
		}

		//print "subscribing";
		$config = $this->_config;

		if (!$this->initializeAgent()) {
			return false;
		}

		$data = "address=".$address;

		$useragent = 'Bitcoin payment module for OpenSim - https://github.com/edmundedgar/Mod-Bitcoin';
		$headers = array(
			'Authorization: ' . $config['accesskey'],
			'User-Agent: ' . $useragent
		);

		if (!$response = BitcoinWebServiceClient::Http_response($url, $data, $headers)) {
			print "subscribe http request failed";
			return false;
		}
	
		return true;

	}

	function initializeAgent() {

		$agent_name = $this->_config['agent_name'];

		// TODO: Instead of hard-coding this, fetch an angent with that name from the service.
		// ...or if it doesn't exist, create it.
		$this->_agent_id = 142;
		
		return true;

	}

/*
		$protocol = ( isset($_SERVER['HTTPS']) && ($_SERVER['HTTPS'] == 'on') ) ? 'https' : 'http';
		$server_name = defined('OPENSIM_BITCOIN_EXTERNAL_URL') ? OPENSIM_BITCOIN_EXTERNAL_URL : $_SERVER['SERVER_NAME'];
*/


	function isValid() {

		// TODO: Check the signature
		return true;

	}

	function notificationURL() {
		http://110-133-28-96.rev.home.ne.jp/payment-server/bitcoin_payment_handler.php?cmd=notify&service=bitcoinmonitor
	}

	function btc_address() {
		return $this->_body_json['signed_data']['address'];
	}

	function num_confirmations_received() {
		return $this->_body_json['signed_data']['confirmations'];
	}

	function btc_amount() {
		// Bitcoinmonitor gives us the amount in satoshis.
		return ( $this->_body_json['signed_data']['amount'] / 100000000 );
	}

}


class BitcoinExchangeRateService {

	var $_service_name;
	var $_config;

	// TODO: Cache the data so we don't have to hit the same service for every transaction.

	function ForServiceName($service_name) {

		// TODO: This should check if there's a more specific class defined
		return new BitcoinExchangeRateService($service_name);

	}

	function BitcoinExchangeRateService($service_name) {

		$configs = unserialize(OPENSIM_BITCOIN_EXCHANGE_RATE_SERVICES);
		if (!isset($configs[$service_name])) {
			return false;
		}

		$this->_config = $configs[$service_name];
		$this->_service_name = $service_name;	

		return true;

	}

	// Return the BTC equivalent of the amount of money in the currency_code, or 0 if it couldn't be found.	
	function ToBTC($amount, $currency_code) {

		if (!$amount || !$currency_code) {
			return 0;
		}

		if (!$rate = BitcoinExchangeRateService::BTCExchangeRate($currency_code)) {
			return 0;
		}

		$btc_amount = ($amount / $rate);
		//print "returning $amount / $rate = $btc_amount";
		return $btc_amount;

	}

	function BTCExchangeRate($currency_code) {

		if ($currency_code == 'BTC') {
			return 1;
		}

		foreach( unserialize(OPENSIM_BITCOIN_EXCHANGE_RATE_SERVICES) as $service_name => $service_settings) {
			$lookup_service = BitcoinExchangeRateService::ForServiceName($service_name);
			if ($btc_amount = $lookup_service->lookup_rate($currency_code)) {
				return $btc_amount;
			}
		}

		return 0;

	}

	function lookup_rate($currency_code) {

		if (!$url = $this->_config['url']) {
			return 0;
		}

		if (!$response = BitcoinWebServiceClient::Http_response( $url, '', array() )) {
			return 0;
		}

		if (!$json = json_decode($response, true)) {
			return 0;
		}

		// TODO: We should probably cache the responses at this point.
		
		if (isset($json[$currency_code]['24h'])) {
			return $json[$currency_code]['24h'];
		}

		return 0;

	}

}

class BitcoinAddressForEmailService {

	var $_service_name;
	var $_config;

	// TODO: Cache the data so we don't have to hit the same service for every transaction.

	function ForServiceName($service_name) {

		// TODO: This should check if there's a more specific class defined
		return new BitcoinAddressForEmailService($service_name);

	}

	function BitcoinAddressForEmailService($service_name) {

		$configs = unserialize(OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_SERVICES);
		if (!isset($configs[$service_name])) {
			return false;
		}

		$this->_config = $configs[$service_name];
		$this->_service_name = $service_name;	

		return true;

	}

	function btc_address_for_email($email_address) {

		if (!$url = $this->_config['url']) {
			return 0;
		}

		$data =  'to='.urlencode($email_address);
		$data .= '&from='.urlencode(OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_PAYER_NAME);
		$data .= '&message='.urlencode(OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_MESSAGE_TEXT);

		// TODO: Need to hit a proper API, not just the front-end...

		if (!$response = BitcoinWebServiceClient::Http_response( $url, $data, array() )) {
			return null;
		}

		//if (preg_match('/$^.*bitcoin\:(.*?)\"/m', $response, $matches)) {
		//}
		if (preg_match('/bitcoin\:(.*?)\"/m', $response, $matches)) {
			return $matches[1];
		} else {
			//print "no match";
		}

		return null;

	}

	function BTCAddressForEmail($email_address) {

		if (!$email_address) {
			return null;
		}

		foreach( unserialize(OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_SERVICES) as $service_name => $service_settings) {
			$address_service = BitcoinAddressForEmailService::ForServiceName($service_name);
			if ($btc_address = $address_service->btc_address_for_email($email_address)) {
				return $btc_address;
			}
		}

		return null;

	}

}

class BitcoinWebServiceClient {

	function Http_response($url, $data, $headers) {

		/*
		//print "<h3>hitting url :$url:</h3>";
		//print "<hr>";
		//var_dump($data);
		//print "<hr>";
		//var_dump($headers);
		//print "<hr>";
		*/

		$ch = curl_init();
		curl_setopt($ch, CURLOPT_URL, $url);

		if (count($headers)) {
			curl_setopt($ch, CURLOPT_HEADER, TRUE);
			curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
		}
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, TRUE);

		curl_setopt($ch, CURLOPT_POST, 1); // set POST method
		curl_setopt($ch, CURLOPT_POSTFIELDS, $data); 

		$result = curl_exec($ch);
		$http_code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
		$info = curl_getinfo($ch);

		curl_close($ch); 

		if ($http_code == 200) {
			return $result;
		}

		return null;

	}

}

function print_simple_and_exit($error, $code = 500) {
	error_log($error);
	header("HTTP/1.0 $code $error");
	exit;
}

