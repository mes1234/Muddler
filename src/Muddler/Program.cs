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
        try
        {
            var proxy = new ProxyService(new Config(), IPAddress.Parse("127.0.0.1"), 5500);

            var cts = new CancellationTokenSource();

            await proxy.Serve(cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Catched error {ex.Message}");
        }
    }
}