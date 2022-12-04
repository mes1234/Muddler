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
    private const int _backlog = 500;
    private readonly Config _config;
    private readonly IPAddress _address;
    private readonly int _port;
    private Socket? _server;

    public ProxyService(Config config, IPAddress address, int port)
    {
        // Define configuration of Proxy Service
        _config = config;
        _address = address;
        _port = port;


        TriggerNewListner += OnNewConnectionEstablieshed;
    }

    public void Serve()
    {
        _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var endpoint = new IPEndPoint(_config.IpAddress, _config.Port);

        _server.Bind(endpoint);

        _server.Listen(_backlog);

        TriggerNewListner.Invoke(this, new EventArgs());

        Console.WriteLine("Server started");

        Console.ReadKey();
    }

    protected virtual void OnNewConnectionEstablieshed(object? sender, EventArgs? e)
    {
        Console.WriteLine("Begin listening for new connections"); 

        //preallocate proxy client
        var pc = new ProxyClient(_address, _port, TriggerNewListner );

        //create transfer object
        var acceptEventArg = new SocketAsyncEventArgs();

        acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(pc.AcceptEventArg_Completed);

        if (_server == null) throw new NotSupportedException("Server not initialized cannot proceed");

        var willRaiseEvent = _server.AcceptAsync(acceptEventArg);

        if (!willRaiseEvent)
        {
            _ = pc.Start(acceptEventArg);
        }
    }

    event EventHandler TriggerNewListner;
}