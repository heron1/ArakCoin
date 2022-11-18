namespace ArakCoin.Networking;

public static class NetworkingManager
{
    /**
     * Asynchronously register this node with another node
     */
    public static async Task<Task<string?>> registerThisNodeWithAnotherNode(Host otherNode)
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
     * Synchronously communicate with every node in the local hosts file and request their hosts file.
     * Update this nodes hosts file with nodes from all the received hosts files
     */
    public static void updateHostsFileFromKnownNodes()
    {
        //todo - async version? necessary for large amount of hosts -> difficult with lock but do it
        //(ensure tests are written first)
        lock (HostsManager.hostsLock)
        {
            var newNodes = new List<Host>();
            foreach (var node in HostsManager.getNodes())
            {
                var nodeListRequestMsg = new NetworkMessage(MessageTypeEnum.GETNODES, "");
                var serializedNetworkMsg = Serialize.serializeNetworkMessageToJson(nodeListRequestMsg);
                string? resp = Communication.communicateWithNode(serializedNetworkMsg, node).Result;
                if (resp is null)
                    continue;

                var receivedNetworkMsg = Serialize.deserializeJsonToNetworkMessage(resp);
                if (receivedNetworkMsg is null)
                    continue;
                
                if (receivedNetworkMsg.messageTypeEnum != MessageTypeEnum.GETNODES)
                    continue;

                var receivedHostsFile = Serialize.deserializeJsonToHosts(receivedNetworkMsg.rawMessage);
                if (receivedHostsFile is null)
                    continue;

                foreach (var receivedNode in receivedHostsFile)
                {
                    if (!HostsManager.getNodes().Contains(receivedNode))
                        newNodes.Add(receivedNode);
                }
            }
            foreach (var node in newNodes)
            {
                HostsManager.addNode(node);
            }
        }
    }

    
}