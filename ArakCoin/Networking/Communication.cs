using System.Net;
using System.Net.Sockets;
using System.Text;
using ArakCoin.Data;

namespace ArakCoin.Networking;

/**
 * Static wrapper class for conducting asynchronous TCP communication between hosts that adheres to the blockchain's
 * base communication protocol. Underlying implementation is done using native .NET sockets and TCP libraries
 */
public static class Communication
{
    /**
     * Send a message through the given network stream that adheres to this blockchain's base communication protocol.
     * Returns true if the message was successfully sent, false otherwise
     */
    public static async Task<bool> sendMessage(string message, NetworkStream stream)
    {
        //the input message cannot contain the newline char, as this signifies end of the message as per the protocol
        if (message.Contains('\n'))
        {
            Utilities.log($"Attempted to send message containing illegal newline char, message: {message}");
            return false;
        }

        //add the newline char to signify end of the message
        message += "\n";

        //start a timeout that once reached, the connection should terminate if the full send hasn't completed by then.
        var timeoutTask = Task.Delay(Settings.networkCommunicationTimeoutMs);
        
        try
        {
            //convert message into bytes and attempt to write it to the stream
            var messageInBytes = Encoding.UTF8.GetBytes(message);
            var asyncStreamWrite = stream.WriteAsync(messageInBytes, 0, messageInBytes.Length);
            
            //check whether the timeout task has completed. If it has, we return false. Otherwise the send succeeded
            await Task.WhenAny(asyncStreamWrite, timeoutTask);
            if (timeoutTask.IsCompleted && !asyncStreamWrite.IsCompleted)
                return false;
        }
        catch (Exception e) when (e.InnerException is SocketException or IOException) //stream is closed
        {
            return false;
        }
        catch (Exception e) //unknown exception, log for investigation
        {
            Utilities.exceptionLog(e.Message);
            return false;
        }
        
        return true;
    }

    /**
     * Receive a message through the given network stream that adheres to this blockchain's base communication
     * protocol. Returns the message if it was successfully received, null otherwise. Does not perform any validation
     * on the message content.
     */
    public static async Task<string?> receiveMessage(NetworkStream stream)
    {
        var receivedMsg = new StringBuilder();
        var buffer = new byte[1_024]; //buffer to keep track of byte chunks received through the stream
        int bytesReceived; //indicates the amount of bytes received into the buffer each transmission
        
        //start a timeout that once reached, the connection should terminate if the full receive hasn't completed by
        //then.
        var timeoutTask = Task.Delay(Settings.networkCommunicationTimeoutMs);

        try
        {
            while (true) //keep reading the stream until sender indicates end of message, or timeout is reached
            {
                //read stream into buffer asynchronously
                var asyncStreamRead = stream.ReadAsync(buffer, 0, buffer.Length);

                //check whether the timeout task has completed. If it has, we return null
                await Task.WhenAny(asyncStreamRead, timeoutTask);
                if (timeoutTask.IsCompleted && !asyncStreamRead.IsCompleted)
                    return null;

                //timeout not reached, so the completed task must be asyncStreamRead
                bytesReceived = asyncStreamRead.Result;
                if (bytesReceived == 0) //no bytes being received indicates an invalid read
                    return null;

                if (buffer[bytesReceived - 1] == 10) //test for newline end of protocol communication special character
                {
                    //don't include the newline char which signifies the end of the message
                    receivedMsg.Append(Encoding.UTF8.GetString(buffer, 0, bytesReceived - 1));
                    break;
                }
                else
                {
                    receivedMsg.Append(Encoding.UTF8.GetString(buffer, 0, bytesReceived));
                }
            }
        }
        catch (Exception e) when (e.InnerException is SocketException or IOException) //stream is closed
        {
            return null;
        } 
        catch (Exception e) //unknown exception, log for investigation
        {
            Utilities.exceptionLog(e.Message);
            return null;
        }
        
        return receivedMsg.ToString();
    }

    /*
     * A higher level communication function that will send a message to a given node as a client, and return
     * the response from the node.
     * Note that -
     *  1) The host is expected to be a node that is currently listening for a client connection
     *  2) The sent message is expected to adhere to the blockchain's Message protocol, however there's no validation
     *     to ensure this here (caller should validate the message before sending it)
     *  3) The received response from the node is expected to adhere to the blockchain's Message protocol, however
     *     there's no validation to ensure this here (caller should validate the received message)
     * If any part of the communication fails, or the underlying base communication protocol (but not message protocol)
     * is not followed (even if the communication was successful), null is returned.
     */
    public static async Task<string?> communicateWithNode(string? message, Host node)
    {
        if (message is null)
            return null;
        try
        {
            IPEndPoint ipEndPoint = IPEndPoint.Parse(node.ToString());

            using TcpClient client = new();
            
            //attempt async connection along with a timeout task. If timeout is reached, return null
            var timeoutTask = Task.Delay(Settings.networkCommunicationTimeoutMs);
            var connectTask = client.ConnectAsync(ipEndPoint);
            await Task.WhenAny(connectTask, timeoutTask);
            if (timeoutTask.IsCompleted && !connectTask.IsCompleted)
            {
                Utilities.log($"Timed out connecting with host {node.ToString()}");
                return null;
            }
            
            await using NetworkStream stream = client.GetStream();

            bool success = await Communication.sendMessage(message, stream);
            if (!success)
                return null;

            string? receivedMsg = await Communication.receiveMessage(stream);
            
            return receivedMsg;
        }
        catch
        {
            return null;
        }
    }

    /**
     * An overloaded version of communicateWithNode that performs message validation both in the send and receive,
     * and automatically handles all serialization and type checking. If any part of the operation fails, null is
     * returned
     */
    public static async Task<NetworkMessage?> communicateWithNode(NetworkMessage message, Host node)
    {
        var serializedNetworkMsg = Serialize.serializeNetworkMessageToJson(message);
        if (serializedNetworkMsg is null)
            return null;
        
        string? resp = await Communication.communicateWithNode(serializedNetworkMsg, node);
        if (resp is null)
            return null;
        
        return Serialize.deserializeJsonToNetworkMessage(resp);
    }
    
    /**
     * Attempts to asynchronously broadcast the given network message to all known nodes in the hosts file.
     * Does not check whether any of the sent messages were successfully sent.
     */
    public static void broadcastNetworkMessage(NetworkMessage message)
    {
        Host self = new Host(Settings.nodeIp, Settings.nodePort);
        foreach (var node in HostsManager.getNodes())
        {
            if (node == self)
                continue; //don't broadcast to self
            
            Communication.communicateWithNode(message, node);
        }
    }
    
}