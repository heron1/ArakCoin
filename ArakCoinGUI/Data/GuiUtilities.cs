using ArakCoin;
using ArakCoin.Networking;
using ArakCoin.Transactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArakCoin_GUI.Data
{
    public static class GuiUtilities
    {
        /**
		 * Update local fields from the network periodically (eg: local balance, etc). The thread calling this
		 * won't ever exit this method
		 */
        public static void updateLocalFieldsFromNetworkTask()
        {
            while (true)
            {
                updateLocalFieldsFromNetwork();
                ArakCoin.Utilities.sleep(10000);
            }
        }

        public static void updateLocalFieldsFromNetwork()
        {
            GuiGlobals.lastBalance = getAddressBalance(Settings.nodePublicKey);
            var chainResp = getChainHeight();
            if (chainResp != -1) //-1 indicates failure, so leave current chain height alone
                GuiGlobals.chainHeight = chainResp;
            GuiHandler.OnStateUpdate(""); //trigger a state update event (eg: for component refresh)
        }

        /**
		 * Get the chain height from the network. return of -1 indicates failure
		 */
        public static long getChainHeight()
        {
            //retrieve chain height from the local chain if we're a node
            if (Settings.isNode)
            {
                return ArakCoin.Globals.masterChain.getLength();
            }

            //otherwise we must get it from the network
            var nm = new NetworkMessage(MessageTypeEnum.GETCHAINHEIGHT, "");
            long receivedHeight = 0;
            Host? receivedNode = null;
            foreach (var node in HostsManager.getNodes()) //iterate through known nodes until valid response
            {
                var respMsg = Communication.communicateWithNode(nm, node).Result;
                if (respMsg is null)
                    continue;

                //ensure received raw message is a valid integer
                if (!Int64.TryParse(respMsg.rawMessage, out receivedHeight))
                    continue;

                receivedNode = node;
            }

            if (receivedNode is null)
            {
                guiLog("Could not retrieve block height from network..\n");
                return -1; //-1 indicates failure
            }

            return receivedHeight;
        }

        /**
        * Get balance for the given address. If this host is a node, we can retrieve it locally, otherwise we must
        * get it from the network)
        */
        public static long getAddressBalance(string address)
        {
            //retrieve balance from the local chain if we're a node
            if (Settings.isNode)
            {
                return Wallet.getAddressBalance(address);
            }

            //otherwise we must get it from the network
            var nm = new NetworkMessage(MessageTypeEnum.GETBALANCE, address);
            long receivedBalance = 0;
            Host? receivedNode = null;
            foreach (var node in HostsManager.getNodes()) //iterate through known nodes until valid response
            {
                var respMsg = Communication.communicateWithNode(nm, node).Result;
                if (respMsg is null)
                    continue;

                //ensure received raw message is a valid integer
                if (!Int64.TryParse(respMsg.rawMessage, out receivedBalance))
                    continue;

                receivedNode = node;
            }

            if (receivedNode is null)
            {
                guiLog("Could not retrieve address balance from network..\n");
                return 0;
            }

            return receivedBalance;
        }

        /**
	     * Log a message on the GUI
	     */
        public static void guiLog(string msg)
        {
            Utilities.log(msg);
        }

        /**
         * Alert message for the GUI
         */
        public static void guiAlert(string msg)
        {
            Application.Current?.MainPage?.DisplayAlert("", msg, "Ok");
        }

        /**
         * Display a GUI prompt and return its input. The requestMsg parameter displays a message for the user
         */
        public static Task<string> getPromptValue(string requestMsg)
        {
            return Application.Current?.MainPage?.
                DisplayPromptAsync("", requestMsg, "Ok");
        }
    }
}
