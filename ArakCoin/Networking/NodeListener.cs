using System.Net;
using System.Net.Sockets;

namespace ArakCoin.Networking;

/**
 * 
 */
public class NodeListener
{
    private Task listeningEntryPointTask;
    private CancellationTokenSource cancellationTokenSource;

    public void startListeningServer()
    {
        cancellationTokenSource = new CancellationTokenSource();
        listeningEntryPointTask = Task.Run(listeningEntryPoint);
    }

    public void stopListeningServer()
    {
        cancellationTokenSource.Cancel(); //trigger the cancellation token for the inner listenLoopTask
        listeningEntryPointTask.Wait(); //wait for the task to end
    }
    
    private void listeningEntryPoint()
    {
        //create the tcp listener using this node's IP
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 8000);
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
                        Utilities.log($"received: {receivedMsg}");
                        await Communication.sendMessage($"received: {receivedMsg}", stream);
                    }
                    else
                    {
                        Utilities.log("timeout..");
                    }
                }
            }
            catch (Exception e)
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
}