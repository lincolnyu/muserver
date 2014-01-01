using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MuServer
{
    /// <summary>
    ///  A class that provides subroutines that deal with domain names and ip addresses
    /// </summary>
    public static class DnsUtility
    {
        #region Methods

        public static IPAddress[] GetLocalAddresses()
        {
            // Getting Ip address of local machine...
            // First get the host name of local machine.
            var strHostName = Dns.GetHostName();

            // Then using host name, get the IP address list..
            var ipEntry = Dns.GetHostEntry(strHostName);
            var addr = ipEntry.AddressList;

            return addr;
        }

        public static IPAddress GetFirstNetworkInterfaceAddress()
        {
            var cards = NetworkInterface.GetAllNetworkInterfaces();
            var card = cards.FirstOrDefault();
            if (card == null) return null;
            var ips = card.GetIPProperties().UnicastAddresses;
            foreach (var ip in ips)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork) // ipv4 address
                {
                    return ip.Address;
                }
            }
            return null;
        }

        #endregion
    }
}
