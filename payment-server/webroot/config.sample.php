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

