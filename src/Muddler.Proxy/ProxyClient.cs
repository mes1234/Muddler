using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text; 

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

        var buffer_in = ArrayPool<byte>.Shared.Rent(1_024);
        var buffer_out = ArrayPool<byte>.Shared.Rent(1_024);

        var fromProxidedToClient = Task.Run(async () => await Shuffle("Proxied to Client", outSocket, inSocket, _context, _logEnabled, buffer_in));

        var fromClientToProxied = Task.Run(async () => await Shuffle("Client to Proxied", inSocket, outSocket, _context, _logEnabled, buffer_out));

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

    private static async Task Shuffle(string direction, Socket outSocket, Socket inSocket, int context, bool logEnabled, Memory<byte> buffer)
    {
        try
        {
            Console.WriteLine($"CTX:{ context}: Begin streaming from {direction}");

            while (true)
            {
                if (!outSocket.Connected) break;

                int size = await outSocket.ReceiveAsync(buffer, SocketFlags.None);

                if (size == 0)
                    break;

                if (logEnabled)
                    LogContent(context, direction, buffer, size);

                if (!inSocket.Connected) break;

                await inSocket.SendAsync(buffer[..size], SocketFlags.None);

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CTX: { context} encountered error {ex.Message} ");
        }
    }

    private static void LogContent(int context, string direction, Memory<byte> buffer, int size)
    {
        var deserialized = Encoding.UTF8.GetString(buffer.ToArray(), 0, size);

        Console.WriteLine($"CTX:{context}: SENDING DIRECTION {direction}: CONTENT  : \n ---------------- \n\n {deserialized} \n\n ---------------- \n\n");
    }
}
