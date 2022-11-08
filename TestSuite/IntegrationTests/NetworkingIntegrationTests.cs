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
    [SetUp]
    public void Setup()
    {
        Settings.nodePublicKey = testPublicKey;
    }
    
    [Test]
    public async Task TestLocalTcpClient()
    {
        LogTestMsg("Testing Client");
        Host a = new Host("192.168.1.19", 8000);

        var ipEndPoint = IPEndPoint.Parse(a.ToString());

        using TcpClient client = new();
        await client.ConnectAsync(ipEndPoint);
        await using NetworkStream stream = client.GetStream();

        var buffer = new byte[1_024];
        int received = await stream.ReadAsync(buffer);

        var message = Encoding.UTF8.GetString(buffer, 0, received);
        LogTestMsg($"Message received: \"{message}\"");

    }
    
    //todo combine both these tests into a single integration test on two threads
    [Test]
    public async Task Temp_TestLocalTcpListener()
    {
        // Host a = new Host("192.16.1.1", 33);
        // int b = 3;
        
        
        var ipEndPoint = new IPEndPoint(IPAddress.Any, 8000);
        TcpListener listener = new(ipEndPoint);

        try
        {    
            listener.Start();

            using TcpClient handler = await listener.AcceptTcpClientAsync();
            await using NetworkStream stream = handler.GetStream();

            var message = $"📅 {DateTime.Now} 🕛";
            var dateTimeBytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(dateTimeBytes);

            LogTestMsg($"Sent message: \"{message}\"");
            // Sample output:
            //     Sent message: "📅 8/22/2022 9:07:17 AM 🕛"
        }
        finally
        {
            listener.Stop();
        }

    }
}