using System;
using System.Data;
using System.Net.Sockets;
using System.Net;
using Muddler.Proxy;

namespace Muddler;


class Program
{
    static async Task Main(string[] args)
    {
        var proxy = new ProxyService(new Config(), IPAddress.Parse("127.0.0.1"), 3000);

        var cts = new CancellationTokenSource();

        await proxy.Serve(cts.Token);
    }
}