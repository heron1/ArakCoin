namespace ArakCoin.Networking;

/**
 * Enum specifying the type of message being sent through the network
 */
public enum MessageTypeEnum
{
    ERROR, //error occurred or message not recognized as adhering to networking protocol
    INFO, //an enum indicating special information within the raw message
    ECHO, //node to send back the exact same received message, provided its <= Settings.echoCharLimit
    GETCHAIN , //client request to retrieve this node's blockchain. Client message content is ignored
    GETBLOCK, //client requests a specific block by ID belonging to this node's chain. Client message to contain the ID
    NEXTBLOCK, //client sends a block to this node. If it's a valid next block, this node will append it to its chain,
               //and send the NEXTBLOCK enum back again. If this didn't happen, this node will send an ERROR instead.
               //If the next block is more than 1 index ahead of the local chain, then it's unknown if the block will
               //be appended by the node, as it may need to request the full chain separately to see if the block is
               //ahead or not on another valid chain. This will be indicated in an INFO message instead
    GETMEMPOOL, //client request to retrieve this node's mempool. Client message content is ignored
    SENDMEMPOOL, //client sends a mempool to this node. This node may update its own mempool based upon
                 //new valid transactions in the received mempool
    GETNODES, //client request to retrieve this node's hostsfile (list of P2P nodes). Client message content is ignored
    REGISTERNODE, //client request for the receiver to add the included sendingNode in the sent NetworkMessage to the
                  //receivers hosts file. Client message content is ignored
    GETUTXOUTS, //client requests the latest utxouts for the blockchain so they can create and sign a transaction.
                //This is what will enable true clients to create transactions without needing a local copy of the
                //blockchain
    GETBALANCE, //client requests the balance for the given address in its message content. Node responds with a
                //serialized integer that represents the balance, which will be 0 if none is found, or the address
                //is invalid
    
}