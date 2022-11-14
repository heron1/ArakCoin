using System.Net;
using System.Net.Sockets;
using System.Text;
using ArakCoin;
using ArakCoin.Networking;

namespace TestSuite.IntegrationTests;

[TestFixture] [NonParallelizable] 
[Category("IntegrationTests")]
public class NetworkingIntegrationTests
{
    /**
     * Note these integration tests only test networking on the local machine. Functional tests testing external
     * host-to-host communication exist in the FunctionalTests section, and require that a list of external hosts
     * (at least 1) is provided. * //todo this - or rather make it as a manual test
     *
     * Note these tests cannot be run in parallel, and also the nodeListener must be
     * stopped at the end of each test. This is to ensure a networking socket is available for the next test. 
     */
    private Host host;
    
    [SetUp]
    public void Setup()
    {
        Settings.nodePublicKey = testPublicKey;
        Settings.networkCommunicationTimeoutMs = 500;
        Settings.echoCharLimit = 1000;
        host = new Host(Settings.nodeIp, Settings.nodePort);
    }
    
    [Test]
    public void TestExcessConnectionsWithLocalNodeListener()
    {
        //todo - fails locally. See if this works from different remote clients connecting to a single node
    }

    [Test]
    public async Task TestLocalNodeListenerErroneously()
    {
        //these tests will attempt to break the node listener in various ways using the lower level methods in the
        //Communication class
        LogTestMsg("Testing TestNodeListenerLocallyErroneously..");
        
        //first create and start the node listener
        NodeListener listener = new NodeListener();
        listener.startListeningServer();
        
        //Test 1) test client can send a simple message to the node that doesn't adhere to the Message Protocol,
        //and an error response is received. The communication should succeed on the Communication Protocol level
    
        //first create the stream
        var ipEndPoint = IPEndPoint.Parse(host.ToString());
        using TcpClient client = new();
        client.Connect(ipEndPoint);
        NetworkStream stream = client.GetStream();
        //send a message, assert successfully sent
        var success = await Communication.sendMessage("some invalid message", stream);
        Assert.IsTrue(success);
        //now process the response, assert it's a valid NetworkMessage with an ERROR enum
        var resp = await Communication.receiveMessage(stream);
        Assert.IsNotNull(resp);
        var networkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsNotNull(networkMessage);
        Assert.IsTrue(networkMessage.messageTypeEnum == MessageTypeEnum.ERROR);
        
        //Test 2)
        //the node should be cleaning up the connection. Attempt to send a message and get a response back again
        //within the same connection. This should fail.
        
        //the send may or may not succeed depending upon whether the node has cleaned up the stream yet or not
        await Communication.sendMessage("some invalid message", stream); 
        //however the receive must fail, as per the protocol, since only a single receive and then a send is
        //allowed per connection
        resp = await Communication.receiveMessage(stream); //this should fail
        Assert.IsNull(resp);
        
        //Test 3) Create a new stream with the node and test the nodeListener successfully drops client connection if
        //client takes too long to send its message
        string sendMsg = "sending from client-end1";
        using TcpClient client2 = new();
        client2.Connect(ipEndPoint);
        stream = client2.GetStream(); //connection has been established with a valid stream
        //client will now sleep for 2x the timeout duration. This should cause the listener to drop connection
        Utilities.sleep(Settings.networkCommunicationTimeoutMs * 2);
        await Communication.sendMessage(sendMsg, stream); //now client will send its message
        resp = await Communication.receiveMessage(stream); //the receive should fail
        Assert.IsNull(resp); //connection should have timed out by the node listener
        
        //Test 4) Stop the listening server and assert that a new client connection fails
        listener.stopListeningServer();
        using TcpClient client3 = new();
        Assert.Catch(() =>
        {
            client3.Connect(ipEndPoint);
        });
        //start the listener again
        listener.startListeningServer();
        
        //Final: Create a new connection again and conduct a valid send/receive. Assert this works. This is a
        //sanity check to make sure the prior failing tests only failed because of the reasons stated
        using TcpClient client4 = new();
        client4.Connect(ipEndPoint);
        stream = client4.GetStream();
        networkMessage = new NetworkMessage(MessageTypeEnum.ECHO, sendMsg);
        await Communication.sendMessage(networkMessage.ToString(), stream); 
        resp = await Communication.receiveMessage(stream);
        Assert.IsNotNull(resp);
        networkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsNotNull(networkMessage);
        Assert.IsTrue(networkMessage.messageTypeEnum == MessageTypeEnum.ECHO);
        Assert.IsTrue(networkMessage.rawMessage == sendMsg);
        
        
        
        //each test must stop the listening server
        listener.stopListeningServer();
    }
    
    [Test]
    public async Task TestLocalNodeListenerResponses()
    {
        //these tests will validate the responses received from the nodeListener are correct using the higher level
        //communicateWithnode function in the Communication class
        LogTestMsg("Testing TestNodeListenerLocally..");
        
        //create a listener as the node, and start it
        NodeListener listener = new NodeListener();
        listener.startListeningServer();
        
        //attempt to send a simple message as the client to the nodeListener. Assert same message received back
        var networkMessage = new NetworkMessage(MessageTypeEnum.ECHO, "sending from client-end1");
        string? resp = await Communication.communicateWithNode(networkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        Assert.IsTrue(networkMessage.ToString() == resp); //both send & receive json should be identical
        
        //stop the listener server and assert the communication now fails
        listener.stopListeningServer();
        resp = await Communication.communicateWithNode(networkMessage.ToString(), host);
        Assert.IsNull(resp);
        
        //ERROR test (from invalid ECHO)
        //start the listener again, now attempt to send a very long message as an ECHO. The send should be succssful,
        //however an error should be returned instead of an echo due to the message exceeding the node's local
        //ECHO char limit
        listener.startListeningServer();
        var sentMsg = $"sending from client 1";
        for (int i = 0; i < 1000; i++)
            sentMsg += "asdaskljd aksldj lakds ";
        sentMsg += "end";
        networkMessage = new NetworkMessage(MessageTypeEnum.ECHO, sentMsg);
        resp = await Communication.communicateWithNode(networkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        networkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsNotNull(networkMessage);
        Assert.IsTrue(networkMessage.messageTypeEnum == MessageTypeEnum.ERROR);

        
        //each test must stop the listening server
        listener.stopListeningServer();

    }

    [Test]
    public void Temp()
    {
        
    }
    
}