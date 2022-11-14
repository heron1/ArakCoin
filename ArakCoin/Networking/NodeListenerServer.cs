﻿using System.Net;
using System.Net.Sockets;

namespace ArakCoin.Networking;

/**
 * This is a master class that nodes can use to automatically listen and respond to on-going client connection requests.
 * If a client correctly initiates a connection and sends a valid message in accordance with the Networking protocol,
 * then this node will respond with an appropriate message also adhering to the protocol.
 *
 * Usage: Instantiate this class, then call startListeningServer() to actively listen for client connections as a new
 * non-blocking background task from the calling thread. Call stopListeningServer() to stop listening for client
 * connections.
 *
 * Note this class implements the IDisposable interface to clean up any used networking and thread resources upon
 * destruction if used within a "using" statement, or if the "Dispose" method is manually called.
 */
public class NodeListenerServer : IDisposable
{
    private Task listeningEntryPointTask;
    private CancellationTokenSource cancellationTokenSource;
    private bool isRunning = false;

    public void startListeningServer()
    {
        if (isRunning)
            return;

        isRunning = true;
        
        //create a new cancellation token source and a new task to run the entry point function loop
        cancellationTokenSource = new CancellationTokenSource();
        listeningEntryPointTask = Task.Run(listeningEntryPoint);
    }

    public void stopListeningServer()
    {
        if (!isRunning)
            return;
        
        cancellationTokenSource.Cancel(); //trigger the cancellation token for the inner listenLoopTask
        listeningEntryPointTask.Wait(); //wait for the task to end

        isRunning = false;
    }
    
    private void listeningEntryPoint()
    {
        //create the tcp listener using this node's IP on its given port in the settings file
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, Settings.nodePort);
        TcpListener listener = new(ipEndPoint); 
        
        //create a cancellation token from the source which we can call to stop the listener at any time
        CancellationToken token = cancellationTokenSource.Token;
        token.Register(() => listener.Stop()); 
        
        listener.Start(); //start listening for connections
        Task listenLoopTask = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using TcpClient handler = await listener.AcceptTcpClientAsync(token);
                    await using NetworkStream stream = handler.GetStream();

                    string? receivedMsg = await Communication.receiveMessage(stream);
                    if (receivedMsg is not null)
                    {
                        NetworkMessage response = processResponseMsg(receivedMsg);
                        await Communication.sendMessage(response.ToString(), stream);
                    }
                    else
                    {
                        Utilities.log("timeout communicating with host..");
                    }
                }
            }
            catch (OperationCanceledException e) {} //connection failed - expected behaviour if host is down
            catch (Exception e) //unknown exception, log this to investigate
            {
                Utilities.exceptionLog($"Unexpected listener exception: {e}");
            }
            finally
            {
                listener.Stop();
            }
        });

        listenLoopTask.Wait();
    }

    /**
     * Processes a received message, which should be a json serialized NetworkMessage. If the message is invalid
     * in any way (including not being a serialized NetworkMessage, or not adhering to the Networking Protocol),
     * the response will indicate an error. This returns the appropriate NetworkMessage object for a response.
     */
    private NetworkMessage processResponseMsg(string receivedMsg)
    {
        NetworkMessage? networkMessage = Serialize.deserializeJsonToNetworkMessage(receivedMsg);
        if (networkMessage is null)
            return createErrorNetworkMessage("failed to receive valid serialized NetworkMessage object");

        switch (networkMessage.messageTypeEnum)
        {
            case MessageTypeEnum.ECHO:
                if (networkMessage.rawMessage.Length > Settings.echoCharLimit)
                    return createErrorNetworkMessage($"ECHO requests must be {Settings.echoCharLimit} chars");
                return new NetworkMessage(MessageTypeEnum.ECHO, networkMessage.rawMessage);
            case MessageTypeEnum.GETCHAIN:
                var serializedBlockchain = Serialize.serializeBlockchainToJson(Global.masterChain);
                if (serializedBlockchain is null)
                    return createErrorNetworkMessage("Error retrieving local blockchain");
                return new NetworkMessage(MessageTypeEnum.GETCHAIN, serializedBlockchain);
            case MessageTypeEnum.GETBLOCK:
                int blockIndex;
                if (!Int32.TryParse(networkMessage.rawMessage, out blockIndex))
                    return createErrorNetworkMessage("Invalid message content for block index");
                Block? requestedBlock = Global.masterChain.getBlockByIndex(blockIndex);
                if (requestedBlock is null)
                    return createErrorNetworkMessage($"Block with index {blockIndex} does not " +
                                                     $"exist in local blockchain");
                var serializedBlock = Serialize.serializeBlockToJson(requestedBlock);
                if (serializedBlock is null)
                    return createErrorNetworkMessage($"Local node error serializing block occurred");
                return new NetworkMessage(MessageTypeEnum.GETBLOCK, serializedBlock);
            case MessageTypeEnum.NEXTBLOCK:
                Block? candidateNextBlock = Serialize.deserializeJsonToBlock(networkMessage.rawMessage);
                if (candidateNextBlock is null || candidateNextBlock.calculateBlockHash() is null)
                    return createErrorNetworkMessage($"Invalid block received");
                if (Global.masterChain.addValidBlock(candidateNextBlock)) //block successfully added
                    return new NetworkMessage(MessageTypeEnum.NEXTBLOCK, "");
                if (candidateNextBlock.index > Global.masterChain.getLength() + 1) 
                    //Received block is a candidate ahead block. Execute new background task to handle whether
                    //another blockchain can be found to replace this one
                {
                    Task.Run(() => handleAheadCandidateNextBlock(networkMessage));
                    return new NetworkMessage(MessageTypeEnum.INFO,
                        $"This chain's last block has an index of {Global.masterChain.getLength()} but the" +
                        $"received block has a higher index of {candidateNextBlock.index}. Node will check for a " +
                        $"potential replacement chain");
                }
                return createErrorNetworkMessage($"Invalid next block received");

            default:
                return createErrorNetworkMessage();

        }

        //todo all processing for response
    }

    private NetworkMessage createErrorNetworkMessage(string msg = "")
    {
        return new NetworkMessage(MessageTypeEnum.ERROR, msg);
    }

    /**
     * If a block is received with an index 2 or higher than the current last block on this node's chain, then it's
     * unknown if the block can be legally appended if the missing block(s) in-between are found. Therefore,
     * this node will attempt to retrieve the corresponding chain from the sender and see if that chain is a candidate
     * to replace this chain
     */
    private async Task handleAheadCandidateNextBlock(NetworkMessage networkMessage)
    {
        //todo this - and functional testing. Attempt with another local node on a different port as well as a
        //remote node
        if (networkMessage.sendingNode is null || !networkMessage.sendingNode.validateHostFormatting())
            return; //if the sending client didn't provide a node we can check for a replacement blockchain,
                    //then we cannot proceed any further
        //send a message to the provided node in the networkMessage requesting the node's blockchain
        NetworkMessage sendMsg = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        string serializedMsg = Serialize.serializeNetworkMessageToJson(sendMsg);
        string? resp = await Communication.communicateWithNode(serializedMsg, networkMessage.sendingNode);
        if (resp is null) //node communication failure, we cannot proceed
            return;
        NetworkMessage? recvMsg = Serialize.deserializeJsonToNetworkMessage(resp);
        if (recvMsg is null)
            return;
        if (recvMsg.messageTypeEnum != MessageTypeEnum.GETCHAIN)
            return;
        Blockchain? candidateReplacementChain = Serialize.deserializeJsonToBlockchain(recvMsg.rawMessage);
        if (candidateReplacementChain is null)
            return;
        Blockchain? winningChain = Blockchain.establishWinningChain(new List<Blockchain>(2)
        {
            Global.masterChain,
            candidateReplacementChain
        });
        if (winningChain is null)
            return;
        if (Global.masterChain != winningChain)
        {
            //the retrieved chain is in fact ahead of our own local chain. We should now replace our chain with it
            Global.masterChain.replaceBlockchain(candidateReplacementChain);
        }
    }
    

    /**
     * Correctly close and dispose of the network connection and used threads when this class is disposed
     */
    public void Dispose()
    {
        if (isRunning)
            stopListeningServer();
        
        listeningEntryPointTask.Dispose();
        cancellationTokenSource.Dispose();
    }
}