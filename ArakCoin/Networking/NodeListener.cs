using System.Net;
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
public class NodeListener : IDisposable
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
            return createErrorNetworkMessage("failed to receive valid NetworkMessage object");

        switch (networkMessage.messageTypeEnum)
        {
            case MessageTypeEnum.ECHO:
                if (networkMessage.rawMessage.Length > Settings.echoCharLimit)
                    return createErrorNetworkMessage($"ECHO requests must be {Settings.echoCharLimit} chars");
                return new NetworkMessage(MessageTypeEnum.ECHO, networkMessage.rawMessage);
            
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