using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using Windows.Media.Core;
using Windows.UI.Xaml;

namespace FnSync
{
    sealed class HandShake : IDisposable
    {
        private static readonly int DEFAULT_PORT = 21365;
        private UdpClient udpClient = null, udpClient6 = null;

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

        public static JObject MakeHelloJson(bool OldConnection, string token, string[] target)
        {
            JObject json = new JObject();
            json["peerid"] = MainConfig.Config.ThisId;
            json["peername"] = GetMachineName();
            json["peerport"] = PcListener.Singleton.Port;
            if (OldConnection) json["oldconnection"] = true;

            if (target != null && target.Length > 0)
            {
                json.Add("target", JArray.FromObject(target));
            }

            json["token"] = token ?? Guid.NewGuid().ToString();

            return json;
        }

        private static byte[] MakePackage(bool OldConnection, string token, SavedPhones.Phone[] target)
        {
            string[] t = null;
            if (target != null)
            {
                t = new string[target.Length];

                for (int i = 0; i < target.Length; ++i)
                {
                    t[i] = target[i].Id;
                }
            }

            return Encoding.UTF8.GetBytes(
                MakeHelloJson(OldConnection, token, t).ToString(Newtonsoft.Json.Formatting.None)
                );
        }

        private string[] Ipv4BroadcastAddresses = null;
        private string[] Ipv6BroadcastAddresses = null;

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
                    udpClient6.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
                }
                catch (Exception e)
                {
                    continue;
                }

            foreach (string ip in Ipv4BroadcastAddresses)
                try
                {
                    udpClient.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
                }
                catch (Exception e)
                {
                    continue;
                }
        }

        private void ReachTargets(byte[] data, SavedPhones.Phone[] targets, int portIncrement)
        {
            foreach (SavedPhones.Phone phone in targets)
            {
                try
                {
                    if (phone?.LastIp == null)
                    {
                        return;
                    }
                    else if (phone.LastIp.Contains(":"))
                    {
                        udpClient6.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                    else if (phone.LastIp.Contains("."))
                    {
                        udpClient.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                }
                catch (Exception e) { }
            }
        }

        public void Reach(int timeout, bool OldConnection, SavedPhones.Phone[] targets)
        {
            int ThisRound = Interlocked.Increment(ref Round);

            CreateSocket();

            byte[] data = MakePackage(OldConnection, null, targets);

            new AutoDisposableTimer((state) =>
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

            }, 0);
        }

        public void Cancel()
        {
            Interlocked.Increment(ref Round);
        }
    }
}
