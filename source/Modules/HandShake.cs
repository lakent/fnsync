using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    sealed class HandShake : IDisposable
    {
        private static readonly int DEFAULT_PORT = 21365;
        private UdpClient? udpClient = null, udpClient6 = null;

        private volatile int Round = 0;

        private void CreateSocket()
        {
            udpClient?.Close();
            udpClient6?.Close();

            udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient6 = new UdpClient(AddressFamily.InterNetworkV6);
            udpClient.Client.EnableBroadcast = true;
            udpClient6.Client.EnableBroadcast = true;
        }

        public HandShake()
        {

        }

        public void Dispose()
        {
            Cancel();
            udpClient?.Close();
            udpClient6?.Close();
        }

        public static String GetMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch (Exception)
            {
                return "(Unknown)";
            }
        }

        public static JObject MakeHelloJson(bool OldConnection, string? token, IEnumerable<string>? target)
        {
            JObject json = new()
            {
                ["peerid"] = MainConfig.Config.ThisId,
                ["peername"] = GetMachineName(),
                ["peerport"] = PhoneListener.Singleton.Port
            };

            if (OldConnection)
            {
                json["oldconnection"] = true;
            }

            if (target != null && target.Any())
            {
                json.Add("target", JArray.FromObject(target));
            }

            json["token"] = token ?? Guid.NewGuid().ToString();

            return json;
        }

        private static byte[] MakePackage(bool OldConnection, string? token, IEnumerable<SavedPhones.Phone>? target)
        {
            List<string>? TargetIds = null;
            if (target != null)
            {
                TargetIds = new List<string>();

                foreach(SavedPhones.Phone t in target)
                {
                    TargetIds.Add(t.Id);
                }
            }

            return Encoding.UTF8.GetBytes(
                MakeHelloJson(OldConnection, token, TargetIds).ToString(Newtonsoft.Json.Formatting.None)
                );
        }

        private string[] Ipv4BroadcastAddresses = null!;
        private string[] Ipv6BroadcastAddresses = null!;

        private void RefreshBroadcastAddresses()
        {
            Ipv4BroadcastAddresses = Utils.GetIpv4BroadcastAddress();
            Ipv6BroadcastAddresses = Utils.GetIpv6BroadcastAddress();
        }

        private void ReachLocalNetwork(byte[] data, int portIncrement)
        {
            foreach (string ip in Ipv6BroadcastAddresses)
                try
                {
                    udpClient6?.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
                }
                catch (Exception)
                {
                    continue;
                }

            foreach (string ip in Ipv4BroadcastAddresses)
                try
                {
                    udpClient?.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
                }
                catch (Exception)
                {
                    continue;
                }
        }

        private void ReachTargets(byte[] data, IEnumerable<SavedPhones.Phone> targets, int portIncrement)
        {
            foreach (SavedPhones.Phone phone in targets)
            {
                try
                {
                    if (phone?.LastIp == null)
                    {
                        return;
                    }
                    else if (phone.LastIp.Contains(':'))
                    {
                        udpClient6?.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                    else if (phone.LastIp.Contains('.'))
                    {
                        udpClient?.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                }
                catch (Exception) { }
            }
        }

        public void Reach(int timeout, bool OldConnection, IEnumerable<SavedPhones.Phone>? targets)
        {
            int ThisRound = Interlocked.Increment(ref Round);

            CreateSocket();

            byte[] data = MakePackage(OldConnection, null, targets);

            _ = Task.Run(() =>
             {
                 RefreshBroadcastAddresses();

                 int remain = timeout;
                 int portIncrement = 0;

                 while (remain > 0 && ThisRound == Round)
                 {
                     if (targets != null)
                     {
                         ReachTargets(data, targets, portIncrement / 2);
                     }

                     ReachLocalNetwork(data, portIncrement);

                     remain -= 2500;
                     ++portIncrement;
                     Thread.Sleep(2500);
                 }
             });
        }

        public void Cancel()
        {
            Interlocked.Increment(ref Round);
        }
    }
}
