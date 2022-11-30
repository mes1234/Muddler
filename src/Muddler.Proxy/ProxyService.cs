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
    private const int Backlog = 50;
    private readonly Config _config;
    private readonly IPAddress _address;
    private readonly int _port;
    private Socket _server;

    public ProxyService(Config config, IPAddress address, int port)
    {
        _config = config;
        _address = address ?? throw new ArgumentNullException(nameof(address));
        _port = port;

        TriggerNewListner += OnNewConnectionEstablieshed;
    }

    public void Serve()
    {
        _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var endpoint = new IPEndPoint(_config.IpAddress, _config.Port);

        _server.Bind(endpoint);

        _server.Listen(Backlog);

        TriggerNewListner.Invoke(this, new EventArgs());

        Console.WriteLine("Server started");

        Console.ReadKey();
    }

    protected virtual void OnNewConnectionEstablieshed(Object? sender, EventArgs? e)
    {
        Console.WriteLine("Begin listening for new connections");

         var memPool_in = MemoryPool<byte>.Shared.Rent(1_024);
         var memPool_out = MemoryPool<byte>.Shared.Rent(1_024); 

        //preallocate proxy client
        var pc = new ProxyClient(_address, _port, TriggerNewListner, memPool_in, memPool_out);

        //create transfer object
        var acceptEventArg = new SocketAsyncEventArgs();

        acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(pc.AcceptEventArg_Completed);

        var willRaiseEvent = _server.AcceptAsync(acceptEventArg);

        if (!willRaiseEvent)
        {
            pc.Start(acceptEventArg);
        }
    }

    public event EventHandler TriggerNewListner;
}