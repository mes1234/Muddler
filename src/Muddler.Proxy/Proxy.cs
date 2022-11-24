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
    private const int Backlog = 5;
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
            var handler = await server.AcceptAsync(cancellationToken);

            var pc = new ProxyClient(handler, _address, _port);

            await pc.Start();
        }
    }
}

internal class ProxyClient
{
    private readonly Socket _client;
    private readonly IPAddress _address;
    private readonly int _port;

    public ProxyClient(Socket client, IPAddress address, int port)
    {
        _client = client;
        _address = address;
        _port = port;
    }

    public async Task Start()
    {
        using Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var ipEndpoint = new IPEndPoint(_address, _port);

        await client.ConnectAsync(ipEndpoint);

        while (true)
        { 

          var inbound =  Task.Run(async () =>
            {
                var buffer_in = new byte[1_024];
                while (_client.Connected)
                {
                  
                    var received = await _client.ReceiveAsync(buffer_in, SocketFlags.None);

                    await client.SendAsync(buffer_in, SocketFlags.None);
                }
            });

           var outbound = Task.Run(async () =>
            {
                var buffer_out = new byte[1_024];
                while (client.Connected)
                {
                  
                    var received = await client.ReceiveAsync(buffer_out, SocketFlags.None);

                    await _client.SendAsync(buffer_out, SocketFlags.None);
                }
            });

         await  Task.WhenAll(new Task[] { inbound, outbound });

         break;


        }

        
    }
}
