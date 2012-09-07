A currency module for OpenSimulator (www.opensimulator.org)

THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY

EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED

WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE

DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;

LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT

(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS

SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

----


AS OF 2012-09-07, THIS MODULE BARELY WORKS, AND MAY WELL GO HORRIBLY WRONG.


* This is a payment module for OpenSim.
* It uses PayPal or Bitcoin as the backend, rather than storing money on the server. 
* All transactions are real hard transactions, either in a regular currency like US Dollars or in Bitcoin.
* The server does not have access to users' money. It only handles information that allows people to pay money _to_ the user, namely email or Bitcoin addresses.
* You can enable either PayPal, Bitcoin or both. See below for the details about each of these payment methods.
* Payments are made by following the instructions on a web page provided by URL dialog.
* This module a heavily altered version of Mod-PayPal, originally developed by Adam Frisby and currently maintained by Snoopy Pfeffer. 
* If you only need PayPal not Bitcoin, you may prefer to use the original Mod-PayPal:
  https://github.com/SnoopyPfeffer/Mod-PayPal.


How PayPal payments work:

* Prices are set in US Cents, or some similar denomination small enough not to need fractions.
* PayPal accepts payment to email addresses. You can specify the email addresses of sellers in the configuration file, or use the email addresses registered for each user. 
* If a user has not registered with PayPal, PayPal will keep the money for them and send them an email asking them to claim it.
* When transactions are completed, PayPal payments are confirmed by the buyer through PayPal's standard purchasing interface (IPN) and inventory delivered etc. 

Warnings about using PayPal:

* There may well be all kinds of flaws and security holes in this module, for which the developers accept no responsibility.
* Initiating a payment to a user reveals that user's email address, so don't enable this unless your users will be happy making their registered email addresses public.
* If the payee does not yet have a PayPal address, and does not claim their money immediately, the payer can cancel the payment, even if they have already received their inventory.
* PayPal is not available in all countries in the world.
* PayPal has a fairly high minimum transaction fee, which may be greater than the amount you want to spend in a single transaction.
* PayPal payments can be reversed months after they have been made.


How Bitcoin payments work:

* You set your price in either Bitcoins or a traditional currency like US Dollars.
* If your prices are set in a traditional currency, it can be converted into Bitcoins automatically, either at a rate you specify or based on a moving 24-hour average exchange rate.
* The provides a web page for users to submit one or many Bitcoin addresses to which payments can be made.
* The module can be configured so that if a payee does not have an available Bitcoin address, one will be created automatically using the Coinapult service (www.coinapult.com) which stores money on the user's behalf and emails them to ask them to collect it. Coinapult say they will return money to the payer if it is still uncollected after 30 days.
* Bitcoin payments are confirmed by an external monitoring service. We are currently using bitcoinmonitor.net. You will need to sign up for an API key with them and add it to your configuration.
* Users can make payments from client software running on their computer or from a web-based e-wallet service. In future it should be possible to build this functionality into the OpenSim viewer, making the experience more seamless.

Warnings about using Bitcoin:

* There may well be all kinds of flaws and security holes in this module, for which the developers accept no responsibility.
* The bitcoinmonitor.net service may go down, which would prevent the server from starting new Bitcoin transactions, or completing existing ones.
* If the bitcoinmonitor.net service malfunctions or is compromised, it may tell you that payments have been completed when they haven't really.
* If you use Coinapult to enable payments to people who haven't registered Bitcoin addresses, any uncollected money could be stolen or lost if the Coinapult people were technically or ethically compromised.
* If you use an automated exchange rate average, failure or an external service could stop your users making payments. If it somehow produced incorrect information, you could end up selling things for the wrong prices.
* The Bitcoin exchange rate can be quite volatile, and the value of your users' Bitcoins may go down as well as up. This may also require you to change your pricing, if you have priced in Bitcoins rather using a traditional currency and letting the system convert it.
* By default the module accepts payments immediately, as soon as they become visible on the network. In theory it is possible to double-spend money at this point, with the result that the payee never receives it. This can be avoided by checking how far the payment has been confirmed by the network, but this will cause payments to take longer to process. (To be certain of payment, you need to set to 6 confirmations, which takes an average of 1 hour.) 


See the comments in the freemoney.ini file for the specific configuration options.


