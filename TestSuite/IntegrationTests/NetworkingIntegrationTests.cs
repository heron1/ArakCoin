using System.Net;
using System.Net.Sockets;
using System.Text;
using ArakCoin;
using ArakCoin.Networking;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class NetworkingIntegrationTests
{
    private Host host;
    
    [SetUp]
    public void Setup()
    {
        Settings.nodePublicKey = testPublicKey;
        host = new Host(Settings.nodeIp, Settings.nodePort);
    }
    
    [Test]
    public async Task TestLocalTcpClient1()
    {
        LogTestMsg("Testing Client");
        
        var sentMsg = $"sending from client 1";
        for (int i = 0; i < 1000; i++)
            sentMsg += "asdaskljd aksldj lakds ";
        sentMsg += "end";

        string resp = await Communication.communicateWithNode(sentMsg, host);

        LogTestMsg($"Message received: \"{resp}\"");
    }
    
    [Test]
    public async Task TestLocalTcpClient2()
    {
        LogTestMsg("Testing Client");
        
        var sentMsg = $"sending from client 2";

        string resp = await Communication.communicateWithNode(sentMsg, host);
        Assert.IsNotNull(resp);

        LogTestMsg($"Message received: \"{resp}\"");
    }
    
    [Test]
    public async Task TestLocalTcpClient3()
    {
        LogTestMsg("Testing Client");

        var sentMsg = $"sending from client 3";
        
        IPEndPoint ipEndPoint = IPEndPoint.Parse(host.ToString());

        using TcpClient client = new();
        await client.ConnectAsync(ipEndPoint);
        await using NetworkStream stream = client.GetStream();
        while (true)
            Utilities.sleep(10);

        // string? resp = await Communication.communicateWithNode(sentMsg, a);
        // Assert.IsNotNull(resp);

        // LogTestMsg($"Message received: \"{resp}\"");
    }
    
    [Test]
    public void TaskTest()
    {
        NodeListener listener = new NodeListener();

        int a = 3;
        while (true)
            a++;

    }
    
    //todo combine both these tests into a single integration test on two threads
    [Test]
    public async Task Temp_TestLocalTcpListener()
    {
        LogTestMsg("Testing Listener");

        //create the tcp listener using this node's IP
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 8000);
        TcpListener listener = new(ipEndPoint); 
        
        //create a cancellation token which we can call to stop the listener at any time
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token; ;
        cancellationToken.Register(() => listener.Stop()); 
        
        listener.Start(); //start listening for connections
        Task nodeListenerTask = Task.Run(async () => //handle incoming connections on a separate thread/task
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using TcpClient handler = await listener.AcceptTcpClientAsync(cancellationToken);
                    await using NetworkStream stream = handler.GetStream();

                    string? receivedMsg = await Communication.receiveMessage(stream);
                    if (receivedMsg is not null)
                    {
                        LogTestMsg($"received: {receivedMsg}");
                        await Communication.sendMessage($"received: {receivedMsg}", stream);
                    }
                    else
                    {
                        LogTestMsg("timeout..");
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

        // while (!cancellationToken.IsCancellationRequested)
        //     Utilities.sleep(10);
        //     
        // cancellationTokenSource.Cancel();

        nodeListenerTask.Wait();

        int b = 3;


    }
}