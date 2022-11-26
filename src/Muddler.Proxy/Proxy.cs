using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Muddler.Proxy;

public class ProxyService
{
    private const int Backlog = 50;
    private readonly Config _config;
    private readonly IPAddress _address;
    private readonly int _port;

    public ProxyService(Config config, IPAddress address, int port)
    {
        _config = config;
        _address = address ?? throw new ArgumentNullException(nameof(address));
        _port = port;
    }

    public async Task Serve(CancellationToken cancellationToken)
    {
        var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var endpoint = new IPEndPoint(_config.IpAddress, _config.Port);

        server.Bind(endpoint);

        server.Listen(Backlog);

        Console.WriteLine("Server started");

        Console.WriteLine("Attached handler");


        while (true)
        {
            Console.WriteLine("Begin listening for new connections");
            var handler = await server.AcceptAsync(cancellationToken);

            var pc = new ProxyClient(handler, _address, _port);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await pc.Start();

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }, cancellationToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}

/// <summary>
/// Proxy client shovels data from 
/// In* - Client of Muddler
/// to
/// Out* - Proxied server
/// </summary>
internal class ProxyClient
{
    private readonly Socket _inSocket;
    private readonly IPAddress _address;
    private readonly int _port;
    private readonly int _context;

    public ProxyClient(Socket inSocket, IPAddress address, int port)
    {
        _inSocket = inSocket;
        _address = address;
        _port = port;
        _context = (new Random()).Next(0, 100);
    }

    public async Task Start()
    {
        Console.WriteLine($"CTX:{_context}: Begin handling new request");

        var outSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var cts = new CancellationTokenSource();

        var ipEndpoint = new IPEndPoint(_address, _port);

        await outSocket.ConnectAsync(ipEndpoint);

        var fromClientToProxied = Task.Run(async () => await Shuffle("Proxied to Client", outSocket, _inSocket, cts, cts.Token));


        var fromProxiedToClient = Task.Run(async () => await Shuffle("Client to Proxied", _inSocket, outSocket, cts, cts.Token));

        Task.WaitAll(new[] { fromClientToProxied, fromProxiedToClient });

        CloseAllSockets(outSocket, _inSocket);

        Console.WriteLine($"CTX:{_context}: Both streaming ended");
    }
    private void CloseAllSockets(Socket outSocket, Socket inSocket)
    {
        if (outSocket.Connected)
        {
            outSocket.Shutdown(SocketShutdown.Both);
            outSocket.Close();

            Console.WriteLine($"CTX:{_context}: Closed socket from Muddler to proxied");
        }

        if (inSocket.Connected)
        {
            inSocket.Shutdown(SocketShutdown.Both);
            inSocket.Close();

            Console.WriteLine($"CTX:{_context}: Closed socket from client to Muddler");
        }
    }

    private async Task Shuffle(string direction, Socket outSocket, Socket inSocket, CancellationTokenSource cts, CancellationToken ct)
    {
        Console.WriteLine($"CTX:{_context}: Begin streaming from {direction}");

        var buffer = new byte[1_024];

        while (inSocket.Connected && outSocket.Connected && !ct.IsCancellationRequested)
        {

            try
            {
                int size = await outSocket.ReceiveAsync(buffer, SocketFlags.None, ct);


                var deserialized = Encoding.UTF8.GetString(buffer, 0, size);

                var corrected = deserialized.Replace("8080", "5555");

                var correctedBytes = Encoding.UTF8.GetBytes(corrected);

                Console.WriteLine($"CTX:{_context}: DIRECTION {direction}: CONTENT : {corrected}");

                if (size == 0)
                    break;

                await inSocket.SendAsync(correctedBytes, SocketFlags.None, ct);
            }
            catch (Exception ex)
            {

            }
         

        }

        if (!ct.IsCancellationRequested)
            cts.Cancel();
    }
}
