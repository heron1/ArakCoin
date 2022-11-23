using System.Net;
using System.Net.Sockets;
using System.Text;
using ArakCoin;
using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture] [NonParallelizable] 
[Category("IntegrationTests")]
public class NetworkingIntegrationTests
{
    /**
     * Note these integration tests only test networking on the local machine (both client and node reside locally).
     * The ManualTests project will allow manual testing & observation of the network in an actual distributed
     * environment.
     *
     * Note these tests cannot be run in parallel, and also the nodeListener must be
     * stopped at the end of each test. This is to ensure a networking socket is available for the next test. 
     */
    private Host host;
    
    [SetUp]
    public void Setup()
    {
        Settings.nodePublicKey  = testPublicKey;
        Protocol.BLOCK_REWARD = 20;
        Settings.minMinerFee  = 0;
        Settings.echoCharLimit  = 1000;
        host = new Host(Settings.nodeIp, Settings.nodePort);

        ArakCoin.Global.masterChain = new Blockchain();
    }
    
    [Test]
    public void TestExcessConnectionsWithLocalNodeListener()
    {
        //todo - fails locally. See if this works from different remote clients connecting to a single node
    }

    [Test]
    public async Task TestLocalNodeListenerErroneously()
    {
        Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == 0);

        //these tests will attempt to break the node listener in various ways using the lower level methods in the
        //Communication class
        LogTestMsg("Testing TestNodeListenerLocallyErroneously..");
        
        //first create and start the node listener
        NodeListenerServer listener = new NodeListenerServer();
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
        LogTestMsg("Testing TestLocalNodeListenerResponses..");
        
        //for these tests, we remove the blacklisted nodes that we'll be creating
        Settings.manuallyBlacklistedNodes  = new List<Host>(); //set empty
        HostsManager.removeNodeFromBlacklist(new Host("1.1.1.1", 9000)); //test node 1
        HostsManager.removeNodeFromBlacklist(new Host("2.2.2.2", 9000)); //test node 2
        
        Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == 0);
        
        //create a listener as the node, and start it
        NodeListenerServer listener = new NodeListenerServer();
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
        var sendNetworkMessage = new NetworkMessage(MessageTypeEnum.ECHO, sentMsg);
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        var receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsNotNull(receivedNetworkMessage);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.ERROR);
        
        //GETCHAIN test 1 (retrieve an empty blockchain)
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETCHAIN);
        Blockchain? receivedChain = Serialize.deserializeJsonToBlockchain(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedChain);
        Assert.IsTrue(receivedChain.getLength() == 0);
        Assert.IsTrue(receivedChain.isBlockchainValid());
        
        //GETCHAIN test 2 (have node mine some blocks, then the client can request this chain)
        Blockchain bchain = new Blockchain();
        for (int i = 0; i < 10; i++) //mine 10 blocks
        {
            BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        }
        ArakCoin.Global.masterChain = bchain; //set this as the node's main chain
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host); //request the chain
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETCHAIN);
        receivedChain = Serialize.deserializeJsonToBlockchain(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedChain);
        Assert.IsTrue(receivedChain.getLength() == 10);
        Assert.IsTrue(receivedChain.isBlockchainValid());
        
        //GETBLOCK test (client retrieves the 7th block from node, assert it's the right one retrieved)
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETBLOCK, "7");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETBLOCK);
        Block receivedBlock = Serialize.deserializeJsonToBlock(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedBlock.calculateBlockHash()); //ensure block is valid
        Assert.IsTrue(receivedBlock.index == 7);

        //GETBLOCK test fail (client passes an invalid message for the block request)
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETBLOCK, "7a");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.ERROR);
        
        //Note: The below two tests will be missing a sendingNode origin, which is useful to be included by a client if
        //it is also a node, and sending a next valid block. Remote node communication will have this field populated
        //however in a functional test, not here in these local integration tests.
        
        //NEXTBLOCK test - simulate client mining a next valid block and sending it to the node.
        //                 After this is done, the client should request the blockchain from the node and assert
        //                 that this block was indeed added.
        Block nextValidBlock = BlockFactory.createAndMineNewBlock(ArakCoin.Global.masterChain);
        string serializedValidBlock = Serialize.serializeBlockToJson(nextValidBlock);
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.NEXTBLOCK, serializedValidBlock);
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.NEXTBLOCK);
        //now request the chain, and assert it includes the sent valid block
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETCHAIN);
        receivedChain = Serialize.deserializeJsonToBlockchain(receivedNetworkMessage.rawMessage);
        Assert.IsTrue(receivedChain.getLastBlock() == nextValidBlock);
        Assert.IsTrue(receivedChain.isBlockchainValid());
        
        //NEXTBLOCK test fail - same as above test except the sent block is invalid. Assert it wasn't added
        int blockChainLength = ArakCoin.Global.masterChain.getLength();
        Block invalidNextBlock = BlockFactory.createAndMineNewBlock(ArakCoin.Global.masterChain);
        invalidNextBlock.timestamp = 1; //invalidate the block
        string serializedInvalidBlock = Serialize.serializeBlockToJson(invalidNextBlock);
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.NEXTBLOCK, serializedInvalidBlock);
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.ERROR);
        //now request the chain, and assert it hasn't been changed
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETCHAIN);
        receivedChain = Serialize.deserializeJsonToBlockchain(receivedNetworkMessage.rawMessage);
        Assert.IsFalse(receivedChain.getLastBlock() == invalidNextBlock); //invalid block shouldn't have been added
        Assert.IsTrue(blockChainLength == ArakCoin.Global.masterChain.getLength());
        Assert.IsTrue(receivedChain.isBlockchainValid());
        
        //NEXTBLOCK test - ahead by 2, but no node sent in the message. So this node cannot do anything to verify
        //                  whether that block is valid or not
        //receivedChain is a value copy of the node's blockchain. We will mine it to get ahead
        BlockFactory.mineNextBlockAndAddToBlockchain(receivedChain);
        BlockFactory.mineNextBlockAndAddToBlockchain(receivedChain); //now 2 blocks ahead
        Block skippedBlock = receivedChain.getLastBlock();
        string serializedskippedBlock = Serialize.serializeBlockToJson(skippedBlock);
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.NEXTBLOCK, serializedskippedBlock);
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.INFO);
        
        //GETMEMPOOL test (empty mempool)
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETMEMPOOL, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETMEMPOOL);
        var receivedMempool = Serialize.deserializeJsonToMempool(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedMempool);
        Assert.IsTrue(Blockchain.validateMemPool(receivedMempool, ArakCoin.Global.masterChain.uTxOuts));
        
        //GETMEMPOOL test (mempool with a valid tx)
        //add a valid tx to the node's mempool
        var tx = TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(Global.testPublicKey, 10)
        }, Global.testPrivateKey, ArakCoin.Global.masterChain);
        //now retrieve the mempool from the node as a client, and assert its tx is the same one as the one created
        //in addition to asserting the mempool is valid
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETMEMPOOL, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETMEMPOOL);
        receivedMempool = Serialize.deserializeJsonToMempool(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedMempool);
        Assert.IsTrue(receivedMempool.Count == 1);
        Assert.IsTrue(Blockchain.validateMemPool(receivedMempool, ArakCoin.Global.masterChain.uTxOuts));
        //this is our main assertion for this part:
        Assert.IsTrue(tx == receivedMempool[0]);
        Assert.IsTrue(ArakCoin.Global.masterChain.mempool.SequenceEqual(receivedMempool));
        
        //GETNODES test - retrieve nodes list from this node as a client, assert operation succeeds
        //add at least 1 P2P node to this node if it doesn't already exist
        HostsManager.addNode(new Host("1.1.1.1", 9000)); //some random but correctly formatted host details
        Assert.IsTrue(HostsManager.getNodes().Contains(new Host("1.1.1.1", 9000)));
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETNODES, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETNODES);
        var receivedNodes = Serialize.deserializeJsonToHosts(receivedNetworkMessage.rawMessage);
        Assert.IsNotNull(receivedNodes);
        Assert.IsTrue(HostsManager.getNodes().SequenceEqual(receivedNodes));

        //REGISTERNODE test - test to register a valid node. Assert node added from GETNODES
        Host registeredNode = new Host("2.2.2.2", 9000); //some random but correctly formatted host details
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.REGISTERNODE, "", registeredNode);
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        Assert.IsNotNull(resp);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.REGISTERNODE);
        //now retrieve nodes list, assert it was added
        sendNetworkMessage = new NetworkMessage(MessageTypeEnum.GETNODES, "");
        resp = await Communication.communicateWithNode(sendNetworkMessage.ToString(), host);
        receivedNetworkMessage = Serialize.deserializeJsonToNetworkMessage(resp);
        Assert.IsTrue(receivedNetworkMessage.messageTypeEnum == MessageTypeEnum.GETNODES);
        receivedNodes = Serialize.deserializeJsonToHosts(receivedNetworkMessage.rawMessage);
        Assert.Contains(registeredNode, receivedNodes);

        //each test must stop the listening server
        listener.stopListeningServer();
        
        Utilities.sleep(5000);
    }

    //Test async mining on a single separate task
    [Test]
    public async Task TestMineBlocksAsync()
    {
        Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == 0);
        
        //perform this same test twice, so it's known async mining can be started & stopped repeatedly
        for (int i = 0; i < 2; i++)
        {
            //begin async mining, retrievie cancellation token
            var miningCancellationToken = AsyncTasks.mineBlocksAsync();

            //sleep this thread to allow some async mining
            while (ArakCoin.Global.masterChain.getLength() == 0)
                Utilities.sleep(100);

            //cancel the mining and retrieve the immediate chain length
            AsyncTasks.cancelMineBlocksAsync(miningCancellationToken);
            int chainLength = ArakCoin.Global.masterChain.getLength();

            //sleep this thread for 1 whole second, no mining should have taken place due to the cancellation
            Utilities.sleep(1000);
            Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == chainLength);
            
            //clear the chain and perform this test again
            ArakCoin.Global.masterChain = new Blockchain();
        }
    }

    //Test async mining on two separate tasks - this shouldn't result in mining parallelism due to the async mining
    //lock
    [Test]
    public async Task TestConflictingMiningTasksAsync()
    {
        Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == 0);
        
        //begin async mining on one Task, retrievie cancellation token
        var miningCancellationToken1 = AsyncTasks.mineBlocksAsync();
        
        //begin async mining on another Task, retrievie cancellation token
        var miningCancellationToken2 = AsyncTasks.mineBlocksAsync();
        
        //sleep this thread to allow some async mining, then cancel both tasks with the global mining
        //lock. Retrieve immediate chain length. The two competing mining threads should respect the global mining lock
        //and not override one another, or this thread
        while (ArakCoin.Global.masterChain.getLength() == 0)
            Utilities.sleep(100);
        int chainLength;
        lock (ArakCoin.Global.asyncMiningLock)
        {
            //cancel both Tasks atomically (since these two tasks & this thread all respect the same lock)
            AsyncTasks.cancelMineBlocksAsync(miningCancellationToken1);
            AsyncTasks.cancelMineBlocksAsync(miningCancellationToken2);
            
            chainLength = ArakCoin.Global.masterChain.getLength();
        }

        //sleep this thread for 1 whole second, no mining should have taken place due to the cancellation
        Utilities.sleep(1000);
        Assert.IsTrue(ArakCoin.Global.masterChain.getLength() == chainLength);
        
        //repeat the above test, but this time leave one Task mining - async mining should continue
        ArakCoin.Global.masterChain = new Blockchain();
        miningCancellationToken1 = AsyncTasks.mineBlocksAsync();
        miningCancellationToken2 = AsyncTasks.mineBlocksAsync();
        while (ArakCoin.Global.masterChain.getLength() == 0)
            Utilities.sleep(100);
        lock (ArakCoin.Global.asyncMiningLock)
        {
            //this time cancel only one task
            AsyncTasks.cancelMineBlocksAsync(miningCancellationToken1);
            chainLength = ArakCoin.Global.masterChain.getLength();
        }
        //we should be able to wait for a block mine from the other task
        while (ArakCoin.Global.masterChain.getLength() == chainLength)
            Utilities.sleep(100);

        Assert.IsFalse(ArakCoin.Global.masterChain.getLength() == chainLength);
        //cleanup remaining task
        AsyncTasks.cancelMineBlocksAsync(miningCancellationToken2);
    }
    
    //todo - test client/server mining and sending each other same chain,and different chains. Test both converge
    //to the consensus chain (do this on distributed nodes in functional testing)

    [Test]
    public async Task Temp()
    {
        var nodes = HostsManager.getNodes();
        int b = 3;
    }
}