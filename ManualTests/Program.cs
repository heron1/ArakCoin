//todo - ongoing refactoring of moving the manual tests from the TestSuite project into its own project
//note: ManualTests in the TestSuite project should be refactored into terminating functional tests that do
//assertions

using ArakCoin;
using ArakCoin.Networking;
using ArakCoin.Transactions;


namespace ManualTests
{
/**
 * This ManualTests project serves as a way to manually test different parts of the blockchain. It does not
 * assert anything - manual observation is required. To run proper local unit tests, execute the TestSuite project
 * instead. Some tests may throw an exception to simulate an assertion - this will be indicated in the test description
 */
    internal static class Program
    {
        // MANUALLY SETUP PUBLIC KEYS OF ALL NODES HERE FOR TX SIMULATION
        private static List<string> publicKeys = new List<string>()
        {
            "1f62745d8f64ac7c9e28a17ad113cb2e4d1bd85e6eb6896f58de3bf3cabcd1b9",
            "dcacb71463dc0d168c9ed87f58c669ad0c96c4ecad08810b4d35dbdb7e50934e"
        };
        
        static void Main(string[] args)
        {
            //todo advanced - optional UI here for user to select the desired test. For now, just change in source code
            //as desired. ALTERNATIVELY: This project could be executed with different command line arguments indicating
            //the desired test, along with any arguments for the test (where applicable).
            TestSimulatedNetworkInteraction().Wait();

        }
        
        //display the balances of every known test public key address on a block update, and do test assertions
        //(this method will need to be subscribed to a block update event)
        private static void testBlockHandler(Object? obj, Block latestBlock)
        {
            //UPDATE -> The follow test has been commented out as it's no longer relevant. Coin supply can no 
            //longer be asserted correctly by simply multiplying a fixed block reward by the chain height, as the
            //block reward is now dynamic 
            
            // Utilities.log("Current test address balances: ");
            // foreach (var key in publicKeys)
            // {
            //     Utilities.log($"\t{key.Substring(0, 3)}..: {Wallet.getAddressBalance(key)} coins");
            // }
            //
            // //assert the coin supply from utxouts matches the intended total block mining rewards
            // //Also assert blockchain is valid
            // lock (Globals.masterChain.blockChainLock)
            // {
            //     long correctCoinSupply = (Globals.masterChain.getLength() - 1) * Protocol.INITIALIZED_BLOCK_REWARD;
            //     long actualSupply = Wallet.getCurrentCirculatingCoinSupply(Globals.masterChain);
            //     if (correctCoinSupply != actualSupply)
            //     {
            //         Utilities.exceptionLog("Groundbreaking error :'( -> coin supply should be" +
            //                                $" {correctCoinSupply} but is {actualSupply}");
            //         throw new Exception($"Groundbreaking error :'( -> coin supply should be" +
            //                             $" {correctCoinSupply} but is {actualSupply}");
            //     }
            //     else
            //     {
            //         Utilities.log($"Correct coin supply asserted as: {actualSupply} (test passed)");
            //     }
            //
            //     if (!Blockchain.isBlockchainValid(Globals.masterChain))
            //     {
            //         throw new Exception($"Blockchain with height {Globals.masterChain.getLength()} is not valid");
            //     }
            // }
        }

        /**
         * Basic test to see if the nodes in the hosts file can communicate. Make sure all nodes have executed
         * the test before pressing the key to continue, which will begin the test. All nodes should share the same
         * hosts file for this test (the test will not undergo node discovery like the live chain does)
         */
        public static async Task TestNetworkConnectivityFromHostsfile()
        {
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue (once all hosts running test)..");
            Console.ReadLine();
            Console.WriteLine("Continuing..");

            Console.WriteLine("Registering node with hosts..");
            foreach (var node in HostsManager.getNodes())
            {
                //critical section
                await NetworkingManager.registerThisNodeWithAnotherNode(node);
            }
		
            foreach (var node in HostsManager.getNodes())
            {
                var networkMsg = new NetworkMessage(MessageTypeEnum.ECHO, "hello world");
                var resp = await Communication.communicateWithNode(networkMsg, node);
                if (resp is null || resp.messageTypeEnum != MessageTypeEnum.ECHO)
                {
                    Console.WriteLine($"Communication failed with {node.ToString()}");
                }
                else
                {
                    Console.WriteLine($"Communication succeeded with {node.ToString()}");
                }
            }

            Console.WriteLine("Press Enter to end the program..");
            Console.ReadLine();
        }
        
        /**
         * Multiple nodes can execute this same unit test to see if the blockchain networking protocol is working
         * as intended - this is a manual observation test
         */
        public static async Task TestBasicNetworkInteraction()
        {   
            //load any local chain we have stored as a candidate for the network consensus chain
            Blockchain.loadMasterChainFromDisk();
            
            Console.WriteLine("Attempting to establish consensus chain from network..");
            NetworkingManager.synchronizeConsensusChainFromNetwork();
            Console.WriteLine($"Local chain set with length {Globals.masterChain.getLength()} " +
                              $"and accumulative hashpower of" +
                              $" {Globals.masterChain.calculateAccumulativeChainDifficulty()}");
            
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
            
            //subscribe to the block update event
            GlobalHandler.latestBlockUpdateEvent += testBlockHandler;
            
            //begin mining as a new Task in the background
            Globals.miningCancelToken = AsyncTasks.mineBlocksAsync();
            
            //begin node discovery & registration as a new Task in the background
            Globals.nodeDiscoveryCancelToken = AsyncTasks.nodeDiscoveryAsync(Settings.nodeDiscoveryDelaySeconds);
            
            //begin periodic mempool broadcasting as a new Task in the background
            Globals.mempoolCancelToken = AsyncTasks.shareMempoolAsync(Settings.mempoolSharingDelaySeconds);
            
            while (true)
            {
                Utilities.sleep(10000); //this thread doesn't actually need to do anything else
            }
        }

        /**
         * Similar to the TestBasicNetworkInteraction test except nodes will also create and share random transactions,
         * and keep track of their wallets. Transactions, balances, and block reward details will also be displayed.
         * Additionally, periodic assertions (via throwing an exception) will take place if the blockchain enters into
         * an invalid state, or the circulating supply does not equal the currently mined block supply that should
         * exist.
         */
        public static async Task TestSimulatedNetworkInteraction()
        {
            var localKeys = publicKeys.ToList();
            localKeys.Remove(Settings.nodePublicKey); //remove this node's public key
            
            Console.WriteLine($"This node has public key: {Settings.nodePublicKey}");
            // END TEST SETUP, BEGIN TEST INITIALIZATION

            //load any local chain we have stored as a candidate for the network consensus chain
            Blockchain.loadMasterChainFromDisk();
            
            Console.WriteLine("Attempting to establish consensus chain from network..");
            NetworkingManager.synchronizeConsensusChainFromNetwork();
            Console.WriteLine($"Local chain set with length {Globals.masterChain.getLength()} " +
                              $"and accumulative hashpower of" +
                              $" {Globals.masterChain.calculateAccumulativeChainDifficulty()}");
            
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
            
            //subscribe to the block update event
            GlobalHandler.latestBlockUpdateEvent += testBlockHandler;
            
            //begin mining as a new Task in the background
            Globals.miningCancelToken = AsyncTasks.mineBlocksAsync();
            
            //begin node discovery & registration as a new Task in the background
            Globals.nodeDiscoveryCancelToken = AsyncTasks.nodeDiscoveryAsync(Settings.nodeDiscoveryDelaySeconds);
            
            //begin periodic mempool broadcasting as a new Task in the background
            Globals.mempoolCancelToken = AsyncTasks.shareMempoolAsync(Settings.mempoolSharingDelaySeconds);
            // END OF CORE PROTOCOL SETUP - AUTOMATED BEHAVIOUR OF NODES FOLLOW

            //initialize the RNG
            int seed = Utilities.getTrulyRandomNumber();
            Random random = new Random(seed);
            while (true)
            {
                List <TxRecord> txRecords = new List<TxRecord>(); //easy way to keep track of transfers for this test

                //attempt to create some random number of TxOuts with a 25% probability this operation stops after each
                List<TxOut> txouts = new List<TxOut>();
                int balance = (int)Wallet.getAddressBalance(Settings.nodePublicKey);
                while (random.NextDouble() < 0.75)
                {
                    if (balance == 0)
                        break;

                    string randomPublicKey = localKeys[random.Next(0, localKeys.Count - 1)];
                    txouts.Add(new TxOut(randomPublicKey, random.Next(1,balance/10)));
                }
                
                //create tx if there's at least 2 txouts. tx creation may or may not succeed, but this doesn't matter
                //(we test both valid and invalid transaction creation)
                if (txouts.Count >= 2)
                {
                    long minerFee = random.Next(0, balance / 20);
                    Transaction? tx = TransactionFactory.createNewTransactionForBlockchain(txouts.ToArray(),
                        Settings.nodePrivateKey, Globals.masterChain, minerFee);
                    if (tx is not null)
                    {
                        foreach (var txOut in tx.txOuts)
                        {
                            if (txOut.address != Protocol.FEE_ADDRESS)
                                txRecords.Add(new TxRecord(Settings.nodePublicKey, txOut.address,
                                    txOut.amount, tx.id!, minerFee));
                        }
                    }

                    if (tx is not null)
                    {
                        Utilities.log($"We created a new locally valid tx and added it to our " +
                                      $"mempool (new size {Globals.masterChain.mempool.Count}):");
                        foreach (var txRecord in txRecords)
                        {
                            Utilities.log($"\t{txRecord.amount} coins to be sent to " +
                                          $"{txRecord.receiver.Substring(0, 3)}..., "
                                          + $"(from tx: {txRecord.transactionId.Substring(0, 3)}, " +
                                          $"fee: {txRecord.minerFee})...");
                        }
                    }
                }
                
                //sleep this thread for some random time (async background threads will still run)
                Utilities.sleep(random.Next(0, 10000)); 
            }
        }
        
        
    }
	
	

}




