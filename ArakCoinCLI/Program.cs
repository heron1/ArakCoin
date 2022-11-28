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
		private static long chainHeight; //last known height of the network chain
		
		static void Main(string[] args)
		{
			//subscribe to the block update event
			GlobalHandler.latestBlockUpdateEvent += blockUpdateHandler;
			
			//load node services if applicable
			if (Settings.isNode)
			{
				//load local masterchain and retrieve consensus chain from network
				Blockchain.loadMasterChainFromDisk();
				cliLog("Attempting to establish consensus chain from network..");
				NetworkingManager.synchronizeConsensusChainFromNetwork();
				cliLog($"Local chain set with length {ArakCoin.Globals.masterChain.getLength()} " +
				       $"and accumulative hashpower of" +
				       $" {ArakCoin.Globals.masterChain.calculateAccumulativeChainDifficulty()}");
				
				//begin the node listening server
				ArakCoin.Globals.nodeListener.startListeningServer();
				
				//begin node discovery & registration as a new Task in the background
				ArakCoin.Globals.nodeDiscoveryCancelToken = 
					AsyncTasks.nodeDiscoveryAsync(Settings.nodeDiscoveryDelaySeconds);
				
				//begin periodic mempool broadcasting as a new Task in the background
				ArakCoin.Globals.mempoolCancelToken = 
					AsyncTasks.shareMempoolAsync(Settings.mempoolSharingDelaySeconds);
			}

			if (Settings.isMiner)
			{
				//begin mining as a new Task in the background
				ArakCoin.Globals.miningCancelToken = AsyncTasks.mineBlocksAsync();
			}
			
			//don't display log messages on main UI
			Settings.displayLogMessages = false;
			
			//initialize local fields from network
			updateLocalFieldsFromNetwork();
			
			//begin periodic address balance update task
			Task.Run(updateLocalFieldsFromNetworkTask);
			
			cliLog("\nWelcome to Arak Coin command line interface!\n" +
			               "===============================================\n");
			while (true)
			{
				UILoop();
			}
		}

		/**
		 * Event handler for block updates
		 */
		static void blockUpdateHandler(object? o, Block nextBlock)
		{
			if (Settings.isNode)
			{
				updateLocalFieldsFromNetwork();
			}
		}

		static void UILoop()
		{
			cliLog("\nSTATUS: ");
			cliLog($"\tNode Services: {(Settings.isNode ? "Online" : "offline")}");
			cliLog($"\tBackground Mining: {(Settings.isMiner ? "Online" : "offline")}");
			cliLog($"\tLocal wallet balance: {lastBalance}");
			cliLog($"\tNetwork chain height: {chainHeight}");

			cliLog("\nPlease enter the number corresponding to your action (Enter to refresh):");
			cliLog("\t1) Create & broadcast new transaction");
			cliLog("\t2) View specific address balance");
			cliLog("\t3) View specific block");
			cliLog("\t4) View live log");
			cliLog("\t5) Manually add a new node");
			cliLog("\t6) Manually blacklist a node");
			cliLog("\t7) Display known nodes");
			cliLog("\t8) Manually retrieve consensus chain from network");
			cliLog("\t9) Reset settings file");
			cliLog("\t0) Exit..");

			var input = getInput();
			handleUIInput(input);
		}

		/**
		 * Update local fields from the network periodically (eg: local balance, etc). The thread calling this
		 * won't ever exit this method
		 */
		static void updateLocalFieldsFromNetworkTask()
		{
			while (true)
			{
				updateLocalFieldsFromNetwork();
				Utilities.sleep(10000);
			}
		}

		static void updateLocalFieldsFromNetwork()
		{
			lastBalance = getAddressBalance(Settings.nodePublicKey);
			var chainResp = getChainHeight();
			if (chainResp != -1) //-1 indicates failure, so leave current chain height alone
				chainHeight = chainResp;
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
				case "3":
					handleGetBlock();
					break;
				case "4":
					handleLiveLog();
					break;
				case "5":
					handleAddNewNode();
					break;
				case "6":
					handleBlacklistNode();
					break;
				case "7":
					handleDisplayNodes();
					break;
				case "8":
					handleRetrieveConsensusChain();
					break;
					
			}
		}

		static void handleCreationTransactionInput()
		{
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
			UTxOut[]? receivedUtxOuts = null;
			Host? receivedNode = null;

			//if we are a node, we can just retrieve the utxouts from the local chain, otherwise we must request
			//the utxouts from the network
			if (Settings.isNode)
			{
				receivedNode = new Host(Settings.nodeIp, Settings.nodePort);
				receivedUtxOuts = ArakCoin.Globals.masterChain.uTxOuts;
			}
			else
			{
				var nm = new NetworkMessage(MessageTypeEnum.GETUTXOUTS, "");
				cliLog("\tretrieving latest utxout list from network..");
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
				cliLog("\tLocal serialization of our transaction failed. It was *not* broadcast to the network..");
				return;
			}
			cliLog($"\tTransaction has been broadcast to the network..");
		}

		static void handleGetAddressBalance()
		{
			cliLog($"\tEnter address of balance to check: ");
			var address = getInput();
			var balance = getAddressBalance(address);
			cliLog($"\tAddress {address} has a balance of {balance} coins");
		}
		
		static void handleGetBlock()
		{
			cliLog($"\tEnter the block index to retrieve from the network " +
			       $"(current chain height is {chainHeight})");
			var blockIndex = getIntInput();
			if (blockIndex == 0) //index begins at 1, 0 indicates exiting this method
				return;
			cliLog($"Attempting to retrieve block {blockIndex} from network..");

			Block? retrievedBlock = null;
			Host? receivedNode = null;
			if (Settings.isNode) //retrieve block from the local chain if we're a node
			{
				retrievedBlock = ArakCoin.Globals.masterChain.getBlockByIndex(blockIndex);
				receivedNode = new Host(Settings.nodeIp, Settings.nodePort);
			}
			else //else we must get it from the network
			{
				var nm = new NetworkMessage(MessageTypeEnum.GETBLOCK, blockIndex.ToString());
				foreach (var node in HostsManager.getNodes()) //iterate through known nodes until valid response
				{
					var respMsg = Communication.communicateWithNode(nm, node).Result;
					if (respMsg is null)
						continue;

					//ensure received raw message can be deserialized into a valid block
					Block? deserializedBlock = Serialize.deserializeJsonToBlock(respMsg.rawMessage);
					if (deserializedBlock is null)
						continue;
					if (deserializedBlock.calculateBlockHash() is null)
						continue;

					retrievedBlock = deserializedBlock;
					receivedNode = node;
				}
			}
			if (retrievedBlock is null || receivedNode is null)
			{
				cliLog("\tError retrieving the given block from the network..");
				return;
			}

			string minedByString = retrievedBlock.index == 1 ? ""
				: $"\t\tMined by: {retrievedBlock.transactions[0].txOuts[0].address}\n";
			cliLog($"\tBlock {retrievedBlock.index} successfully retrieved " +
			       $"(difficulty {retrievedBlock.difficulty}):\n" +
			       $"\t\tBlock hash: {retrievedBlock.calculateBlockHash()}\n" +
			       $"{minedByString}" +
			       $"\t\tNonce: {retrievedBlock.nonce}, Timestamp: {retrievedBlock.timestamp}\n" +
			       $"\t\tPrevious block hash: {retrievedBlock.prevBlockHash}\n" +
			       $"\t\tTransactions:");
			foreach (var tx in retrievedBlock.transactions)
			{
				cliLog($"\t\t\tTx id: {tx.id}\n\t\t\t\tTx Outs:");
				foreach (var txout in tx.txOuts)
				{
					cliLog($"\t\t\t\t\t{txout.address} received {txout.amount} coins");
				}
				cliLog(""); //newline
			}
		}

		static void handleLiveLog()
		{
			//retrive and write buffered strings to output
			var strList = StringQueue.retrieveOrderedQueue();
			foreach (var s in strList)
				cliLog(s);

			//display new messages to console
			Settings.displayLogMessages = true;
			
			//wait for user to press enter to exit
			Console.ReadLine(); 
			
			//turn off displaying new messages to console
			Settings.displayLogMessages = false;
		}

		static void handleAddNewNode()
		{
			cliLog("Enter the ipv4 address of the node");
			var ip = getInput();
			if (ip == "0")
				return;
			cliLog("Enter the port number of the node");
			var port = getIntInput();
			if (port == 0)
				return;
			Host host;
			try
			{
				host = new Host(ip, port);
			}
			catch (ArgumentException e)
			{
				cliLog($"Error creating the host: {e.Message}");
				return;
			}

			if (!HostsManager.addNode(host))
				cliLog($"The node {host} was *not* added. Does it already exist? Or is it blacklisted?");
			else
			{
				cliLog($"Successfully added new node {host}");
			}
		}

		static void handleBlacklistNode()
		{
			cliLog("Enter the ipv4 address of the node to blacklist");
			var ip = getInput();
			if (ip == "0")
				return;
			cliLog("Enter the port number of the node to blacklist");
			var port = getIntInput();
			if (port == 0)
				return;
			Host host;
			try
			{
				host = new Host(ip, port);
			}
			catch (ArgumentException e)
			{
				cliLog($"Error creating the host for blacklist: {e.Message}");
				return;
			}
			if (!HostsManager.addNodeToBlacklist(host))
				cliLog($"The node {host} was *not* added to the blacklist. Is it already blacklisted?");
			else
			{
				cliLog($"Successfully added new node {host} to the blacklist");
			}
		}

		/**
		 * Displayed nodes should display nodes both from the hosts file, and the blacklisted hosts file
		 */
		static void handleDisplayNodes()
		{
			cliLog("Currently known nodes: ");
			foreach (var node in HostsManager.getNodes())
			{
				cliLog($"\t{node}");
			}

			cliLog("Blacklisted nodes: ");
			var blacklistedNodes = HostsManager.getBlacklistedNodes();
			if (blacklistedNodes is null)
				return;
			foreach (var node in blacklistedNodes)
			{
				cliLog($"\t{node}");
			}
		}

		static void handleRetrieveConsensusChain()
		{
			string? originalLastBlockHash = ArakCoin.Globals.masterChain.getLastBlock()?.calculateBlockHash();
			cliLog("Retrieving consensus chain from network..");
			NetworkingManager.synchronizeConsensusChainFromNetwork();
			string? newLastBlockHash = ArakCoin.Globals.masterChain.getLastBlock()?.calculateBlockHash();
			if (originalLastBlockHash is not null && newLastBlockHash is not null)
			{
				if (originalLastBlockHash == newLastBlockHash)
				{
					cliLog("Network consensus chain is already the same as the local chain");
					return;
				}

				cliLog($"Retrieved consensus chain of length {ArakCoin.Globals.masterChain.getLength()} " +
				       $"from the network");
				bool success = Blockchain.saveMasterChainToDisk();
				if (!success)
				{
					cliLog("Failed to save the chain to disk..");
					return;
				}

				string savePath = Path.Combine(Storage.appDirectoryPath, "master_blockchain");
				cliLog($"Saved the chain to disk at {savePath}");
			}
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

		/**
		 * Get the chain height from the network. return of -1 indicates failure
		 */
		static long getChainHeight()
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
	}
}