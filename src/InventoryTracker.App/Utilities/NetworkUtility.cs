using System.Net;
using System.Net.Sockets;

namespace InventoryTracker.App.Utilities;

internal static class NetworkUtility
{
    internal static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch { return "localhost"; }
    }
}
