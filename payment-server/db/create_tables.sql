drop database if exists opensim_btc;
create database opensim_btc;

-- Table for keeping a list of Bitcoin addresses for each avatar.
-- We may later want to break avatar into its own table with more information.
-- Where possible, we will keep many addresses for each avatar.
create table opensim_btc.opensim_btc_addresses(
	btc_address varchar(255) primary key not null, -- A Bitcoin address somebody can pay money to.
	avatar_uuid varchar(255) not null -- The avatar name.
);

-- Table for lists of transactions.
-- The key for each transaction is created on the OpenSim server.
-- This first part of this table is modelled on the fields used by PayPal IPN.
-- This is to stay as close as possible to the PayPal payment module on which the Bitcoin module is based. 
create table opensim_btc.opensim_btc_transactions(
	id integer not null auto_increment unique,
	payee varchar(255) not null, -- The payee. In the PayPal world this would be "business", which is an email address. We will have to do some kind of lookup to get a Bitcoin address for this person.
	item_name varchar(255) not null, -- The name of the object to be transferred, for humans.
	transaction_code varchar(255) not null, -- The transaction ID. In the Paypay world it's called "item_number".
	original_amount float(10, 8) not null, -- The original amount requested in the original currency. If quoted in a non-BTC currency, we may convert it. In PayPal, "amount".
	original_currency_code varchar(127) not null, -- The currency originally requested. In PayPal, "currency_code".
	btc_amount float(10, 8) not null, -- The BTC equivalent at the time when the payment address was given to the user.
	notify_url varchar(1023) not null, -- The URL we  should notify when the payment is confirmed.
	btc_address varchar(255) not null, -- The address we gave the user to pay.
	num_confirmations_required integer(10) not null default 0, -- The number of confirmations we are expected to wait for before considering the payment paid.
	created_ts integer(10) not null, -- A unix timestamp for the time we first heard about the transaction.
	payment_detected_ts integer(10) not null default 0, -- A timestamp for the first time we saw the payment on the network. 
	num_confirmations_received integer(10) not null default 0, -- The number of confirmations received, last time we checked.
	confirmation_sent_ts integer(10) not null default 0 -- A timestamp for the time we notified the server that payment was complete.
);

grant select, update, insert, delete on opensim_btc.* to 'opensim_btc_user'@'localhost' identified by 'somepassword';
