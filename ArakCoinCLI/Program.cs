global using ArakCoin;
global using ArakCoin.Networking;
global using ArakCoin.Transactions;
global using static ArakCoinCLI.Handlers;
global using static ArakCoinCLI.Utilities;

namespace ArakCoinCLI
{
	static internal class Program
	{
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
		
		/**
		 * The command line entry point for the ArakCoin project
		 */
		static void Main(string[] args)
		{
			//handle settings setup
			if (!Settings.loadSettingsFileAtRuntime())
				Handlers.handleSettingsSetup();
			
			//subscribe to the block update event
			GlobalHandler.latestBlockUpdateEvent += blockUpdateHandler;
			
			//load node services if applicable
			if (Settings.isNode)
			{
				//load local masterchain and retrieve consensus chain from network
				if (Blockchain.loadMasterChainFromDisk())
					cliLog("Successfully loaded local chain from disk..");
				else
					cliLog("Failed to load a local chain from disk..");
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
			cliLog("Getting current chain details from network..");
			updateLocalFieldsFromNetwork();
			
			//begin periodic address balance update task
			Task.Run(updateLocalFieldsFromNetworkTask);
			
			cliLog("\nWelcome to Arak Coin command line interface!\n" +
			               "===============================================\n");
			while (true)
			{
				UILoop(); //main program UI loop
			}
		}

		static void UILoop()
		{
			cliLog("\nSTATUS: ");
			cliLog($"\tNode Services: {(Settings.isNode ? "Online" : "offline")}");
			cliLog($"\tBackground Mining: {(Settings.isMiner ? "Online" : "offline")}");
			if (Settings.isNode)
				cliLog($"\tNode IP: {Settings.nodeIp}, Port: {Settings.nodePort}");
			cliLog($"\tLocal wallet addres: {Settings.nodePublicKey}");
			cliLog($"\tLocal wallet balance: {Globals.lastBalance}");
			cliLog($"\tNetwork chain height: {Globals.chainHeight}");

			cliLog("\nPlease enter the number corresponding to your action (Enter to refresh):");
			cliLog("\t1) Create & broadcast new transaction");
			cliLog("\t2) View specific address balance");
			cliLog("\t3) View specific block");
			cliLog("\t4) View live log");
			cliLog("\t5) Manually add a new node");
			cliLog("\t6) Manually blacklist a node");
			cliLog("\t7) Display known nodes");
			cliLog("\t8) Manually retrieve consensus chain from network");
			cliLog("\t9) Reset settings file (requires program restart)");
			cliLog("\t0) Exit..");

			var input = getInput();
			handleUIInput(input);
		}
	}
}