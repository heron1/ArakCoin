//todo - ongoing refactoring of moving the manual tests from the TestSuite project into its own project

using System.Text;
using ArakCoin;
using ArakCoin.Networking;

namespace ManualTests
{
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
            
            TestBasicNetworkInteraction().Wait();

        }

        /**
         * Basic test to see if a given list of hosts can communicate both ways. Will remove the local node from
         * the connectivity test.
         */
        public static async Task TestNetworkConnectivityFromManualNodesList()
        {
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press key to continue..");
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
        
        public static async Task TestNetworkConnectivityFromHostsfile()
        {
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue..");
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

        public static async Task TestBasicNetworkInteraction()
        {
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
            
            //SIMULATION BEGINS (end of setup)
            //begin mining as a new Task in the background
            Global.miningCancelToken = AsyncTasks.mineBlocksAsync();

            while (true)
            {
                Utilities.sleep(10000);
            }
        }
        
        
    }
	
	

}




