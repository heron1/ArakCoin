using static ArakCoinCLI.Globals;

namespace ArakCoinCLI;

public static class Handlers
{
	    public static void handleUIInput(string uiInput)
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
				case "9":
					handleResetSettings();
					break;
			}
		}

	    public static void handleSettingsSetup()
	    {
		    //determine whether host will be a node
		    string input = "";
		    int intInput = 0;
		    cliLog("No existing settings file detected, we will create one together " +
		           "(this can always be edited later)");
		    while (input != "y" && input != "n")
		    {
			    cliLog("Will this host be a node? Type (y/n) (note a miner must always be a node)");
			    input = getInput();
		    }
		    if (input == "y")
			    Settings.isNode = true;
		    else
			    Settings.isNode = false;

		    //if host will be a node, determine if they'll also be a miner
		    if (Settings.isNode)
		    {
			    input = "";
			    while (input != "y" && input != "n")
			    {
				    cliLog("You've selected for this host be a node. Will this node also be a miner? Type (y/n)");
				    input = getInput();
			    }
		    }
		    if (input == "y")
			    Settings.isMiner = true;
		    else
			    Settings.isMiner = false;
			
		    //if this host will be a node, we needs its network identifier for incoming sockets connections
		    if (Settings.isNode)
		    {
			    Host? node = null;
			    while (node is null)
			    {
				    cliLog("What is the externally accessible ipv4 address for this node for TCP sockets " +
				           "connections?");
				    var ip = getInput();
				    cliLog("What is the port number associated with this ipv4 address?");
				    var port = getIntInput(false);
				    try
				    {
					    node = new Host(ip, port);
				    }
				    catch (ArgumentException e)
				    {
					    cliLog($"Invalid details entered for ip: {ip} with port {port}: {e.Message}. " +
					           $"Please try again");
				    }
			    }

			    Settings.nodeIp = node.ip;
			    Settings.nodePort = node.port;
		    }
		    
		    //set public & private keypair
		    string data;
		    string? signature;
		    bool validated;
		    while (true)
		    {
			    cliLog("We will now enter the public/private keypair for this host (Ed25519 compliant). Please " +
			           "select one of the following two options:\n" +
			           "1) Automatically generate the keypair\n" +
			           "2) Manually enter your own keypair (must be Ed25519 compliant in 32-byte hex format)\n" +
			           "Simply type 1 or 2 depending upon your choice");
			    while (intInput != 1 && intInput != 2)
			    {
					intInput = getIntInput(false);
			    }

			    if (intInput == 1) //generate keypair selected
			    {
				    var keypair = Cryptography.generatePublicPrivateKeyPair();
				    
				    cliLog($"Successfully generated the following keypair -\n" +
				           $"\tpublic key: {keypair.publicKey}\n" +
				           $"\tprivate key: {keypair.privateKey}");
				    
				    validated = Cryptography.testKeypair(keypair.publicKey, keypair.privateKey);
				    if (!validated)
				    {
					    cliLog("This program has a broken cryptography implementation, please alert the developer");
					    throw new Exception($"Automatically generated keypair failed validation\n" +
					                        $"Public key: {keypair.publicKey}\n" +
					                        $"Private key: {keypair.privateKey}");
				    }
				    
				    cliLog("Keypair was succesfully validated");
				    break;
			    }
			    else //only 2 is the other valid option which indicates manual entering of keypair
			    {
				    cliLog("Enter the public key address for receiving coins to (including miner rewards)\n" +
				           "(must be Ed25519 compliant in 32-byte hex format)");
				    var publicKey = getInput();
				    Settings.nodePublicKey = input;
				    cliLog("Enter the corresponding private key (this will only be stored locally, not shared)\n" +
				           "(must be Ed25519 compliant in 32-byte hex format)");
				    var privateKey = getInput();
				    Settings.nodePrivateKey = input;

				    //test correct keypair was entered
				    validated = Cryptography.testKeypair(publicKey, privateKey);
				    if (validated)
				    {
					    cliLog($"Keypair was succesfully validated");
					    break;
				    }

				    cliLog("The public and private key pairs did not pass validation. Please try again..");
				    intInput = -1;
			    }
		    }

		    //set mempool settings if miner
		    if (Settings.isMiner)
		    {
			    cliLog("Since you've selected to be a miner, what is the minimum miner fee for transactions" +
			           "to be allowed into the local mempool? (enter 0 for none)");
			    Settings.minMinerFee = getIntInput(false);
			    cliLog($"What should the maximum local mempool size be? (this is recommended to be larger than" +
			           $" the protocol block transaction limit of {Protocol.MAX_TRANSACTIONS_PER_BLOCK}), however" +
			           $" any value is permitted");
			    Settings.maxMempoolSize = getIntInput(false);
		    }
		    
		    //set default starting nodes
		    intInput = -1;
		    cliLog("We need to enter a default starting node that this host can communicate with, " +
		           "so that the P2P discovery protocol can begin from there. Please select one of the following" +
		           " two options:\n" +
		           "1) Use built-in starting nodes list\n" +
		           "2) Enter manual starting node\n");
		    while (intInput != 1 && intInput != 2)
		    {
			    intInput = getIntInput(false);
		    }

		    if (intInput == 1)
		    {
			    cliLog("The following nodes have been registered as the default starting nodes: ");
			    foreach (var node in Settings.startingNodes)
			    {
				    cliLog(node.ToString());
			    }
		    }
		    else
		    {
			    Host? startingNode = null;
			    while (startingNode is null)
			    {
				    cliLog("What is the externally accessible ipv4 address for the starting node?");
				    var ip = getInput();
				    cliLog("What is the port number associated with its ipv4 address?");
				    var port = getIntInput(false);
				    try
				    {
					    startingNode = new Host(ip, port);
				    }
				    catch (ArgumentException e)
				    {
					    cliLog($"Invalid details entered for ip: {ip} with port {port}: {e.Message}. " +
					           $"Please try again");
				    }
			    }
		    }

		    bool saved = Settings.saveRuntimeSettingsToSettingsFile();
		    while (!saved)
		    {
			    cliLog($"Error saving the settings file, please ensure this program has write access to" +
			           $" the directory: {Storage.appDirectoryPath}" +
			           $" and try again, (press Enter to try again)");
			    getInput();
			    saved = Settings.saveRuntimeSettingsToSettingsFile();
		    }
		    
		    cliLog($"Successfully created the {Settings.jsonFilename} file with your given input values to " +
		           $"the directory: {Storage.appDirectoryPath}" +
		           $"\nNote that the {Settings.jsonFilename} file has additional options that can be configured " +
		           $"(eg: network timeouts, hard coded node blacklist, etc), however it's recommended to leave these " +
		           $"at the default values");
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
				cliLog($"\tTransaction creation failed. Your wallet balance may not be enough for this " +
				       $"transaction, or you may be sending to the same address twice");
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

		static void handleResetSettings()
		{
			bool deleted = Storage.deleteFile(Settings.jsonFilename);
			if (!deleted)
			{
				cliLog("Failed to delete settings file..");
			}
			
			cliLog("Successfully deleted the settings file, restart the program to initialize values again");
			Environment.Exit(0);
		}

		
}