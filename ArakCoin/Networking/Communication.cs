using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ArakCoin.Networking;

/**
 * Static wrapper class for conducting asynchronous TCP communication between hosts that adheres to the blockchain's
 * base communication protocol. Underlying implementation is done using native .NET sockets and TCP libraries
 */
public static class Communication
{
    /**
     * It's recommended the other methods are used to communicate with a host, however this function
     * will allow the creation of a network stream given a host. Unlike the other methods in this class, the
     * NetworkStream must be manually disposed of once the communication is finished.
     */
    public static NetworkStream createNetworkStreamWithHost(Host host)
    {
        var ipEndPoint = IPEndPoint.Parse(host.ToString());
        using TcpClient client = new();
        client.Connect(ipEndPoint);
        NetworkStream stream = client.GetStream();

        return stream;
    }
    
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
     * the response from the host.
     * Note that -
     *  1) The host is expected to be a node that is currently listening for a client connection
     *  2) The sent message is expected to adhere to the blockchain's Message protocol, however there's no validation
     *     to ensure this here (caller should validate the message before sending it)
     *  3) The received response from the node is expected to adhere to the blockchain's Message protocol, however
     *     there's no validation to ensure this here (caller should validate the received message)
     * If any part of the communication fails, or the underlying base communication protocol (but not message protocol)
     * is not followed (even if the communication was successful), null is returned.
     */
    public static async Task<string?> communicateWithNode(string message, Host node)
    {
        try
        {
            IPEndPoint ipEndPoint = IPEndPoint.Parse(node.ToString());

            using TcpClient client = new();
            await client.ConnectAsync(ipEndPoint);
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
    
}