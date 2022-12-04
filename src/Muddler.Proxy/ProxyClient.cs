using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Muddler.Proxy.ProxyService;

namespace Muddler.Proxy;

/// <summary>
/// Proxy client shovels data from 
/// In* - Client of Muddler
/// to
/// Out* - Proxied server
/// </summary>
internal class ProxyClient
{
    private readonly bool _logEnabled;
    private readonly IPAddress _address;
    private readonly int _port;
    private readonly EventHandler _startNewHandler;
    private readonly int _context;

    public ProxyClient(IPAddress address, int port, EventHandler startNewHandler, bool LogEnabled = false)
    {
        _address = address;
        _port = port;
        _startNewHandler = startNewHandler;
        _context = (new Random()).Next(0, 100);
        _logEnabled = LogEnabled;
    }

    public void AcceptEventArg_Completed(object? sender, SocketAsyncEventArgs? e)
    {
        _ = Task.Run(async () => await Start(e));
    }

    public async Task Start(SocketAsyncEventArgs? e)
    {
        Console.WriteLine($"CTX:{_context}: Begin handling new request");

        var inSocket = e!.AcceptSocket!;

        var outSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var ipEndpoint = new IPEndPoint(_address, _port);

        await outSocket.ConnectAsync(ipEndpoint);

        _startNewHandler.Invoke(this, EventArgs.Empty);

        var fromProxidedToClient = Task.Run(async() => await Shuffle("Proxied to Client", outSocket, inSocket));

        var fromClientToProxied = Task.Run(async () => await Shuffle("Client to Proxied", inSocket, outSocket));

        Task.WaitAll(new[] { fromClientToProxied, fromProxidedToClient });

        CloseAllSockets(outSocket, inSocket);

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
            using var memPool_in = MemoryPool<byte>.Shared.Rent(1_024);

            var buffer = memPool_in.Memory;

            Console.WriteLine($"CTX:{_context}: Begin streaming from {direction}");

            while (true)
            {
                if (!outSocket.Connected) break;

                int size = await outSocket.ReceiveAsync(buffer, SocketFlags.None);

                if (size == 0)
                    break;

                if (_logEnabled)
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
