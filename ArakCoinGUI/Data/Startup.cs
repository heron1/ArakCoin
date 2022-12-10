using ArakCoin;
using ArakCoin.Networking;
using ArakCoin_GUI.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArakCoin_GUI.Data
{
	/**
	 * Begin background GUI startup tasks
	 */
	static internal class Startup
	{
		static bool begun = false;
        public static bool loaded = false;

		public static void Begin()
		{
			if (begun)
				return;
			begun = true;

            Task.Run(() =>
            {
                GuiUtilities.guiLog("Initializing Startup routine..");

                //load settings file, generate one with default values if it doesn't exist
                if (!Settings.loadSettingsFileAtRuntime())
                {
                    GuiUtilities.guiLog("No settings file found, generating one with default values..");
                }
                
                //subscribe to the block update event
                GlobalHandler.latestBlockUpdateEvent += blockUpdateHandler;

                //load node services if applicable
                if (Settings.isNode)
                {
                    //load local masterchain and retrieve consensus chain from network
                    if (Blockchain.loadMasterChainFromDisk())
                        GuiUtilities.guiLog("Successfully loaded local chain from disk..");
                    else
                        GuiUtilities.guiLog("Failed to load a local chain from disk..");
                    GuiUtilities.guiLog("Attempting to establish consensus chain from network..");
                    NetworkingManager.synchronizeConsensusChainFromNetwork();
                    GuiUtilities.guiLog($"Local chain set with length {ArakCoin.Globals.masterChain.getLength()} " +
                           $"and accumulative hashpower of" +
                           $" {ArakCoin.Globals.masterChain.calculateAccumulativeChainDifficulty()}");

                    //begin node services
                    GlobalHandler.enableNodeServices();
                    GuiUtilities.guiLog("Node services started..");
                }

                if (Settings.isMiner)
                {
                    //begin mining as a new Task in the background
                    GlobalHandler.enableMining();
                    GuiUtilities.guiLog("Background mining started..");
                }

                //initialize local fields from network
                GuiUtilities.guiLog("Getting current chain details from network..");
                GuiUtilities.updateLocalFieldsFromNetwork();

                //begin periodic address balance update task
                Task.Run(GuiUtilities.updateLocalFieldsFromNetworkTask);

                GuiUtilities.guiLog("Program is now loaded..");
                loaded = true;
            });
        }

        /**
		 * Event handler for block updates
		 */
        static void blockUpdateHandler(object? o, Block nextBlock)
        {
            if (Settings.isNode)
            {
                GuiUtilities.updateLocalFieldsFromNetwork();
            }
        }
    }
}
