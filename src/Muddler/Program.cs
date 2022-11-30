using System;
using System.Data;
using System.Net.Sockets;
using System.Net;
using Muddler.Proxy;

namespace Muddler;


class Program
{
    static void Main(string[] args)
    {
        try
        {
            var proxy = new ProxyService(new Config(), IPAddress.Parse("127.0.0.1"), 5500);

            proxy.Serve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Catched error {ex.Message}");
        }
    }
}