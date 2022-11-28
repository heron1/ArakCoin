namespace ArakCoinCLI;

public static class Utilities
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
			Globals.lastBalance = getAddressBalance(Settings.nodePublicKey);
			var chainResp = getChainHeight();
			if (chainResp != -1) //-1 indicates failure, so leave current chain height alone
				Globals.chainHeight = chainResp;
		}

		public static string getInput()
		{
			Console.Write("Input: ");
			return Console.ReadLine();
		}
		
		/**
		 * Retrieve an integer from user input in a loop that is >= 0. Notifies that 0 = cancel (caller should
		 * ensure this) if the zeroExits property is true (default)
		 */
		public static int getIntInput(bool zeroExits = true)
		{
			string repeatStr = zeroExits
				? "Input wasn't recognized. Please enter a number, or 0 to cancel"
				: "Input wasn't recognized. Please enter a number";
			
			var input = getInput();
			int inputNum;
			while (!Int32.TryParse(input, out inputNum) || inputNum < 0)
			{
				cliLog(repeatStr);
				input = getInput();
			}

			return inputNum;
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
				cliLog("Could not retrieve block height from network..\n");
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
				cliLog("Could not retrieve address balance from network..\n");
				return 0;
			}

			return receivedBalance;
		}
		
	    /**
	     * Log a message that is part of the core command line UI
	     */
	    public static void cliLog(string msg)
	    {
	        Console.WriteLine(msg);
	    }
}