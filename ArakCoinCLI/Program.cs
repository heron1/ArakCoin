using ArakCoin;
using ArakCoin.Networking;
using ArakCoin.Transactions;
using static ArakCoinCLI.Globals;

namespace ArakCoinCLI
{
	/**
	 * The command line entry point for the ArakCoin project
	 */
	static internal class Program
	{
		private static long lastBalance; //last known address balance for this host
		
		static void Main(string[] args)
		{
			//don't display log messages on main UI
			Settings.displayLogMessages = false;
			
			//begin periodic address balance update task
			Task.Run(addressBalanceUpdaterTask);
			
			cliLog("\nWelcome to Arak Coin command line interface!\n" +
			               "===============================================\n");
			while (true)
			{
				UILoop();
			}
		}

		static void UILoop()
		{
			cliLog("\nSTATUS: ");
			cliLog($"\tNode Server: Offline");
			cliLog($"\tBackground Mining: Offline");
			cliLog($"\tLocal wallet balance: {lastBalance}");

			cliLog("\nPlease enter the number corresponding to your action (Enter to refresh):");
			cliLog("\t1) Create & broadcast new transaction");
			cliLog("\t2) View specific address balance");
			cliLog("\t3) View specific block");
			cliLog("\t4) View live log");
			cliLog("\t5) Manually add a new node");
			cliLog("\t6) Manually retrieve consensus chain from network");
			cliLog("\t7) Display known nodes");
			cliLog("\t8) Reset settings file");
			cliLog("\t0) Exit..");

			var input = getInput();
			handleUIInput(input);
		}

		/**
		 * Update the local address balance periodically. The thread calling this won't ever exit this method
		 */
		static void addressBalanceUpdaterTask()
		{
			while (true)
			{
				lastBalance = getAddressBalance(Settings.nodePublicKey);
				Utilities.sleep(10000);
			}
		}

		static string getInput()
		{
			Console.Write("Input: ");
			return Console.ReadLine();
		}
		
		/**
		 * Retrieve an integer from user input in a loop that is >= 0. Notifies that 0 = cancel (caller should
		 * ensure this)
		 */
		static int getIntInput()
		{
			var input = getInput();
			int inputNum;
			while (!Int32.TryParse(input, out inputNum) || inputNum < 0)
			{
				cliLog($"Input wasn't recognized. Please enter a number, or 0 to cancel");
				input = getInput();
			}

			return inputNum;
		}

		static void handleUIInput(string uiInput)
		{
			switch (uiInput)
			{
				case "0":
					Environment.Exit(0);
					break;
				case "1":
					handleCreationTransactionInput();
					break;
				case "2":
					handleGetAddressBalance();
					break;
				
			}
		}

		static void handleCreationTransactionInput()
		{
			cliLog("Retrieving updated wallet balance..");
			lastBalance = getAddressBalance(Settings.nodePublicKey);
			cliLog($"\tYour wallet has: {lastBalance} coins");
			cliLog($"\tWhat mining fee will you pay for this transaction?");
			long minerFee = getIntInput();
			cliLog($"\tHow many destination addresses will there be in this transaction?");
			int addresses = getIntInput();
			if (addresses == 0)
				return;

			long runningInput = 0;
			TxOut[] txouts = new TxOut[addresses];
			for (int i = 0; i < addresses; i++)
			{
				cliLog($"\tEnter address {i + 1} -");
				var addressInput = getInput();
				cliLog("\tEnter the number of coins to send -");
				var amountInput = getIntInput();
				if (amountInput == 0)
				{
					cliLog("\t0 entered, cancelling entire transaction..");
					return;
				}
				cliLog($"\t{amountInput} coins will be sent to {addressInput}\n");
				txouts[i] = new TxOut(addressInput, amountInput);
			}

			cliLog($"\tWe will broadcast the following transaction to the network (paying {minerFee} miner fee): ");
			foreach (var txout in txouts)
			{
				cliLog($"\t\t{txout.amount} coins to be sent to {txout.address}");
			}
			cliLog($"\t\tTotal: {Transaction.getTotalAmountFromTxOuts(txouts) + minerFee} coins");
			
			if (lastBalance - Transaction.getTotalAmountFromTxOuts(txouts) < 0)
				cliLog("\tWarning: Your spend amount is greater than your known balance..");
			cliLog("\tEnter 1 to proceed, any other number to cancel..");
			var inputInt = getIntInput();
			if (inputInt != 1)
				return;
			
			//create network message request
			var nm = new NetworkMessage(MessageTypeEnum.GETUTXOUTS, "");
			cliLog("\tretrieving latest utxout list from network..");
			UTxOut[]? receivedUtxOuts = null;
			Host? receivedNode = null;
			foreach (var node in HostsManager.getNodes()) //iterate through known nodes until valid response
			{
				var respMsg = Communication.communicateWithNode(nm, node).Result;
				if (respMsg is null)
					continue;
				
				UTxOut[]? utxouts = Serialize.deserializeJsonToContainer<UTxOut[]>(respMsg.rawMessage);
				if (utxouts is null)
					continue;

				receivedUtxOuts = utxouts;
				receivedNode = node;
				break; //utxouts received from a node
			}

			if (receivedNode is null || receivedUtxOuts is null)
			{
				cliLog($"\tFailed to retrieve any utxout list from the network, tx failed..");
				return;
			}
			cliLog($"\treceived utxouts from node {receivedNode.ip} ({receivedUtxOuts.Length}) " +
			       $"size, creating transaction..");
			var tx = TransactionFactory.createTransaction(txouts, Settings.nodePrivateKey, receivedUtxOuts,
				new List<Transaction>(), minerFee, false);
			if (tx is null)
			{
				cliLog($"\tTransaction creation failed. Your wallet balance may not be enough for this transaction, " +
				       $"or you may be sending to the same address twice");
				return;
			}
			cliLog($"\ttransaction successfully created with tx id: {tx.id}\n\tBroadcasting tx to network..");
			
			//we immediately broadcast the transaction as a single item in a mempool - this is because the logic
			//of receiving new mempools and transactions should be the same at nodes. However if this client spams this
			//ability they risk being blacklisted by nodes, the same way as a spamming node would be
			var broadcast = NetworkingManager.broadcastMempool(new List<Transaction>() { tx });
			if (!broadcast)
			{
				cliLog("Local serialization of our transaction failed. It was *not* broadcast to the network..");
				return;
			}
			cliLog($"\tTransaction has been broadcast to the network..");
		}

		static void handleGetAddressBalance()
		{
			cliLog($"\tEnter address of balance to check: ");
			var address = getInput();
			var balance = getAddressBalance(address);
			cliLog($"Address {address} has a balance of {balance} coins");
		}

		/**
		 * Get balance for the given address. If this host is a node, we can retrieve it locally, otherwise we must
		 * get it from the network)
		 */
		static long getAddressBalance(string address)
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
	}
}