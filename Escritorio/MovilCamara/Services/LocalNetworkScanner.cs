using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MovilCamara.Services;

public static class LocalNetworkScanner
{
    public static uint ToNumber(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }
    public static IPAddress FromNumber(uint ip) => new(new[] { (byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip });

    // Respect the actual subnet for small LANs. Large LANs are bounded to the local /24.
    public static IEnumerable<IPAddress> Candidates(IPAddress address, IPAddress mask)
    {
        var local = ToNumber(address);
        var subnetMask = ToNumber(mask);
        var network = local & subnetMask;
        var broadcast = network | ~subnetMask;
        if ((ulong)broadcast - network > 1023)
        {
            network = local & 0xffffff00;
            broadcast = network | 255;
        }
        for (ulong ip = (ulong)network + 1; ip < broadcast; ip++)
            if (ip != local) yield return FromNumber((uint)ip);
    }
    public static IPAddress[] ActiveCandidates()
    {
        var local = ConnectionHub.LocalAddresses().ToHashSet();
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && a.IPv4Mask != null)
            .SelectMany(a => Candidates(a.Address, a.IPv4Mask))
            .Where(ip => !local.Contains(ip.ToString())).Distinct().Take(2048).ToArray();
    }
}
