namespace ArakCoin.Networking;

/**
 * All network messages will be communicated in the serialized json version of this class. It will specify the type
 * of message via the enum, so the receiver knows how to process the raw message. The .ToString method will
 * automatically serialize a class instance
 */
public class NetworkMessage
{
    public MessageTypeEnum messageTypeEnum;
    public string rawMessage;

    public NetworkMessage(MessageTypeEnum messageTypeEnum, string rawMessage)
    {
        this.messageTypeEnum = messageTypeEnum;
        this.rawMessage = rawMessage;
    }
    
    public override string ToString()
    {
        return Serialize.serializeNetworkMessageToJson(this)!;
    }

}