using System.Net;

namespace Muddler.Proxy;
public class Config
{
    public int Port { get; set; } = 8080;

    public IPAddress IpAddress { get; set; } = IPAddress.Any;
}
