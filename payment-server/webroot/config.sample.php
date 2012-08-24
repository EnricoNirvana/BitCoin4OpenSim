<?php
define('OPENSIM_BITCOIN_DB_HOST', 'localhost');
define('OPENSIM_BITCOIN_DB_NAME', 'opensim_btc');
define('OPENSIM_BITCOIN_DB_USER', 'opensim_btc_user');
define('OPENSIM_BITCOIN_DB_PASSWORD', 'password');

//define('OPENSIM_BITCOIN_EXTERNAL_URL', '');
define('OPENSIM_BITCOIN_PING_SERVICES', serialize(
	array(
		'bitcoinmonitor' => array(
			'agent_name' => 'opensim_bitcoin_dev_agent',
			'base_url' => 'http://www.bitcoinmonitor.net/api/v1/agent', // //www.bitcoinmonitor.net/api/v1/agent/17/notification/url/
			'accesskey' => 'youraccesskey',
			'verificationkey' => 'yourverificationkey'
		)
	)
));
define('OPENSIM_BITCOIN_EXCHANGE_RATE_SERVICES', serialize(
	array(
		'bitcoincharts' => array(
			'url' => 'http://bitcoincharts.com/t/weighted_prices.json'
		)
	)
));
// TODO: This hits the front-end. We really need to be hitting the API, if they ever get around to giving us a key.
define('OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_SERVICES', serialize(
	array(
		'coinapult' => array(
			// TODO: This is hitting the front end. Need to change this to a proper API address.
			'url' => 'http://coinapult.com/payload/send/'
		)
	)
));
// The name we
define('OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_PAYER_NAME', 'OpenSim Bitcoin Processor');
define('OPENSIM_BITCOIN_ADDRESS_FOR_EMAIL_MESSAGE_TEXT', 'To make payments go directly to your own wallet in future instead of via email, please login and register some Bitcoin addresses.');

