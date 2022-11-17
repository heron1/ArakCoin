//todo - ongoing refactoring of moving the manual tests from the TestSuite project into its own project
using ArakCoin;
using ArakCoin.Networking;

namespace ManualTests
{
    internal class Program
    {
        static void Main(string[] args)
        {
            
            TestNetworkConnectivity().Wait();

        }

        /**
         * Basic test to see if a given list of hosts can communicate both ways. Will remove the local node from
         * the connectivity test.
         */
        public static async Task TestNetworkConnectivity()
        {
    
            var listener = new NodeListenerServer();
            listener.startListeningServer();

            Console.WriteLine("Press key to continue..");
            Console.ReadLine();
            Console.WriteLine("Continuing..");
		
            var nodes = new List<Host>()
            {
                new Host("192.168.1.7", 8000),
                new Host("192.168.1.19", 8000)
            };
            nodes.Remove(new Host(Settings.nodeIp, ArakCoin.Settings.nodePort));

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
    }
	
	

}




