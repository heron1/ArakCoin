namespace ArakCoin.Networking;

/**
 * All network messages will be communicated in the serialized json version of this class. It will specify the type
 * of message via the enum, so the receiver knows how to process the associated raw message.
 *
 * The client may optionally include a Host indicating how it can be reached as a node, however this is only useful
 * if the client is sending a next valid block, or is requesting to be added as a recognized node to the receiving
 * node's hostfile (otherwise the receiver will usually ignore this field). This field can be spoofed with a different
 * node as there is no authentication, however a client that abuses this ability for malicious purposes can be
 * quickly blacklisted by its IP. Furthermore, bad nodes can be identified and removed from the
 * hostsfile if they don't perform reliably. Note it makes no sense for a receiver to populate the sendingNode field
 * in its response message, as the client must already have known its value in order to initiate the connection.
 *
 * The .ToString method will automatically serialize a class instance into a json format ready for communication
 */
public class NetworkMessage
{
    public MessageTypeEnum messageTypeEnum; //type of the message
    public string rawMessage; //the raw message
    public Host? sendingNode; //the client's host details if it is a node, otherwise null (optional)

    public NetworkMessage(MessageTypeEnum messageTypeEnum, string rawMessage, Host? sendingNode = null)
    {
        this.messageTypeEnum = messageTypeEnum;
        this.rawMessage = rawMessage;
        this.sendingNode = sendingNode;
    }
    
    public override string ToString()
    {
        return Serialize.serializeNetworkMessageToJson(this);
    }

}