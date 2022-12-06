using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Muddler.Proxy;

public class ProxyService
{
    private const int Backlog = 100;
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
    private const bool LogEnabled = false;
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

        var ipEndpoint = new IPEndPoint(_address, _port);

        await outSocket.ConnectAsync(ipEndpoint);

        var fromClientToProxied = Task.Run(async () => await Shuffle("Proxied to Client", outSocket, _inSocket));

        var fromProxiedToClient = Task.Run(async () => await Shuffle("Client to Proxied", _inSocket, outSocket));

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

    private async Task Shuffle(string direction, Socket outSocket, Socket inSocket)
    {
        try
        {
            using var memPool = MemoryPool<byte>.Shared.Rent(1_024);

            var buffer = memPool.Memory;

            Console.WriteLine($"CTX:{_context}: Begin streaming from {direction}");

            while (true)
            {
                if (!outSocket.Connected) break;

                int size = await outSocket.ReceiveAsync(buffer, SocketFlags.None);

                if (size == 0)
                    break;

                if (LogEnabled)
                    LogContent(direction, buffer, size);

                if (!inSocket.Connected) break;
                await inSocket.SendAsync(buffer[..size], SocketFlags.None);

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CTX: {_context} encountered error {ex.Message} ");
        }
    }

    private void LogContent(string direction, Memory<byte> buffer, int size)
    {
        var deserialized = Encoding.UTF8.GetString(buffer.ToArray(), 0, size);

        Console.WriteLine($"CTX:{_context}: SENDING DIRECTION {direction}: CONTENT  : \n ---------------- \n\n {deserialized} \n\n ---------------- \n\n");
    }
}
