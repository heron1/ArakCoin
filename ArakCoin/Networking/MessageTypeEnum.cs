namespace ArakCoin.Networking;

/**
 * Enum specifying the type of message being sent through the network
 */
public enum MessageTypeEnum
{
    ERROR, //error occurred or message not recognized as adhering to networking protocol
    ECHO //node to send back the exact same received message, provided its <= Settings.echoCharLimit
    
}