using System.Collections.Concurrent;
using ArakCoin.Data;
using ArakCoin.Transactions;

namespace ArakCoin.Networking;

public static class NetworkingManager
{
    /**
     * Asynchronously register this node with another node
     */
    public static Task<string?> registerThisNodeWithAnotherNode(Host otherNode)
    {
        var sendMsg = new NetworkMessage(MessageTypeEnum.REGISTERNODE, "",
            new Host(Settings.nodeIp, Settings.nodePort));
        var serializedNetworkMsg = Serialize.serializeNetworkMessageToJson(sendMsg);
        return Communication.communicateWithNode(serializedNetworkMsg, otherNode);
    }

    /**
     * Synchronously get the blockchain residing at the specified node
     */
    public static Blockchain? getBlockchainFromOtherNode(Host otherNode)
    {
        var sendMsg = new NetworkMessage(MessageTypeEnum.GETCHAIN, "");
        var recvMsg = Communication.communicateWithNode(sendMsg, otherNode).Result;
        if (recvMsg is null)
            return null;

        if (recvMsg.messageTypeEnum != MessageTypeEnum.GETCHAIN)
            return null;

        return Serialize.deserializeJsonToBlockchain(recvMsg.rawMessage);
    }

    /**
     * Attempts to retrieve the consensus blockchain from the network and synchronize this node with it.
     * 
     * This function requests the hosts file from every known node, and updates the local hosts file with any
     * discovered new nodes. It then requests the local chain from all these nodes, and does a chain comparison
     * with every received response chain, and the local chain. The winning chain is stored as the new local chain,
     * which should represent the consensus network chain.
     */
    public static void synchronizeConsensusChainFromNetwork()
    {
        NetworkingManager.updateHostsFileFromKnownNodes(); //store all known nodes from the network in the hosts file

        ConcurrentBag<Blockchain> candidateChains = new(); //thread safe container
        candidateChains.Add(Globals.masterChain); //the local chain is always added first, to win any tiebreakers
        List<Task> getChainTasks = new();
            
        //now add every local chain that exists at every known node to the candidate chains, in parallel
        foreach (var node in HostsManager.getNodes())
        {
            getChainTasks.Add(Task.Run(() =>
            {
                var receivedChain = getBlockchainFromOtherNode(node);
                if (receivedChain is not null)
                {
                    Utilities.log($"candidate chain received from {node} with height {receivedChain.getLength()}");
                    candidateChains.Add(receivedChain);
                }
                else
                {
                    Utilities.log($"Failed to receive valid chain from {node}");
                }
            }));
        }
        Task.WaitAll(getChainTasks.ToArray());

        //establish the winning blockchain from the network, and set this local chain to it.
        //If null is returned, do nothing (keep the local chain)
        var winningChain = Blockchain.establishWinningChain(candidateChains.ToList());
        if (winningChain is not null)
            Globals.masterChain.replaceBlockchain(winningChain);
    } 

    /**
     * Broadcasts the given block to the P2P network as the next valid block in the consensus chain. Returns false if
     * the local serialization of the block fails, otherwise true. Does not check whether or not nodes accept the
     * block or not, or even if the broadcast was successful.
     *
     * Note that the broadcast message will include the identifier for this host (as set in the Settings file) if
     * this host is set to be a node. This is so that if this host is running as a node, and the sent block is ahead
     * of chains in the other nodes, then those nodes can request the chain from this block to replace their own.
     * If this host isn't set to be a node, then this information won't be included.
     */
    public static bool broadcastNextValidBlock(Block nextValidBlock)
    {
        var serializedBlock = Serialize.serializeBlockToJson(nextValidBlock);
        if (serializedBlock is null)
            return false;
        
        var sendMsg = new NetworkMessage(MessageTypeEnum.NEXTBLOCK, serializedBlock, 
            Settings.isNode ? new Host(Settings.nodeIp, Settings.nodePort) : null);
        Communication.broadcastNetworkMessage(sendMsg);

        return true;
    }

    /**
     * Broadcasts the given mempool to the P2P network. Returns false if the local serialization of the mempool fails,
     * otherwise true. Does not check node responses.
     */
    public static bool broadcastMempool(List<Transaction> mempool)
    {
        var serializedMempool = Serialize.serializeMempoolToJson(mempool);
        if (serializedMempool is null)
            return false;

        var sendMsg = new NetworkMessage(MessageTypeEnum.SENDMEMPOOL, serializedMempool);
        Communication.broadcastNetworkMessage(sendMsg);

        return true;
    }

    /**
     * Synchronously communicate with every node in the local hosts file as separate Tasks in parallel, and request
     * their hosts file. Update this nodes hosts file with nodes from all the received hosts files
     */
    public static void updateHostsFileFromKnownNodes()
    {
        var newNodes = new ConcurrentBag<Host>(); //thread safe container for adding new nodes in parallel
        var nodes = HostsManager.getNodes(); //non-mutating nodes copy
        var tasks = new Task[nodes.Count]; //parallel tasks array
        
        Host self = new Host(Settings.nodeIp, Settings.nodePort);
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            tasks[i] = Task.Run(() =>
            {
                if (node == self)
                    return; //don't communicate with self
                
                var nodeListRequestMsg = new NetworkMessage(MessageTypeEnum.GETNODES, "");
                var serializedNetworkMsg = Serialize.serializeNetworkMessageToJson(nodeListRequestMsg);
                string? resp = Communication.communicateWithNode(serializedNetworkMsg, node).Result;
                if (resp is null)
                    return;

                var receivedNetworkMsg = Serialize.deserializeJsonToNetworkMessage(resp);
                if (receivedNetworkMsg is null)
                    return;

                if (receivedNetworkMsg.messageTypeEnum != MessageTypeEnum.GETNODES)
                    return;

                var receivedHostsFile = Serialize.deserializeJsonToHosts(receivedNetworkMsg.rawMessage);
                if (receivedHostsFile is null)
                    return;

                foreach (var receivedNode in receivedHostsFile)
                {
                    if (!nodes.Contains(receivedNode))
                        newNodes.Add(receivedNode);
                }
            });
        }

        Task.WaitAll(tasks);
        foreach (var node in newNodes)
        {
            HostsManager.addNode(node);
        }
    }
}