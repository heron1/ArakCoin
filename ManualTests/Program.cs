//todo - ongoing refactoring of moving the manual tests from the TestSuite project into its own project
//note: ManualTests in the TestSuite project should be refactored into terminating functional tests that do
//assertions

using System.Text;
using ArakCoin;
using ArakCoin.Networking;


namespace ManualTests
{
/**
 * This ManualTests project serves as a way to manually test different parts of the blockchain. It does not
 * assert anything - manual observation is required. To run proper local unit tests, execute the TestSuite project
 * instead. Some tests may throw an exception to simulate an assertion - this will be indicated in the test description
 */
    internal static class Program
    {
        //nodes to participate in the manual connectivity tests (different to hosts file)
        private static List<Host> nodes = new List<Host>()
        {
            new Host("192.168.1.7", 8000),
            new Host("192.168.1.19", 8000)
        };
        
        static void Main(string[] args)
        {
            //todo advanced - optional UI here for user to select the desired test. For now, just change in source code
            //as desired. ALTERNATIVELY: This project could be executed with different command line arguments indicating
            //the desired test, along with any arguments for the test (where applicable).
            TestBasicNetworkInteraction().Wait();

        }

        /**
         * Basic test to see if a given list of hosts can communicate both ways. Make sure all nodes have executed
         * the test before pressing the key to continue, which will begin the test
         */
        public static async Task TestNetworkConnectivityFromManualNodesList()
        {
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue (once all hosts running test)..");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
		
            bool nodesWorking = true;
            foreach (var node in nodes)
            {
                var networkMsg = new NetworkMessage(MessageTypeEnum.ECHO, "hello world");
                var resp = await Communication.communicateWithNode(networkMsg, node);
                if (resp is null || resp.messageTypeEnum != MessageTypeEnum.ECHO)
                {
                    nodesWorking = false;
                    Console.WriteLine($"Communication failed with {node.ToString()}");
                    break;
                }
                
                Console.WriteLine($"Communication succeeded with {node.ToString()}");
            }

            Console.WriteLine("Press key to end the program..");
            Console.ReadLine();
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
            Console.WriteLine("Attempting to establishing consensus chain from network..");
            NetworkingManager.synchronizeConsensusChainFromNetwork();
            Console.WriteLine($"Local chain set with length {Global.masterChain.getLength()} " +
                              $"and accumulative hashpower of" +
                              $" {Global.masterChain.calculateAccumulativeChainDifficulty()}");
            
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
            
            //SIMULATION BEGINS (end of setup)
            
            //begin mining as a new Task in the background
            Global.miningCancelToken = AsyncTasks.mineBlocksAsync();
            
            //begin node discovery & registration as a new Task in the background
            Global.nodeDiscoveryCancelToken = AsyncTasks.nodeDiscoveryAsync(Settings.nodeDiscoveryDelaySeconds);

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
            //todo this
        }
        
        
    }
	
	

}




