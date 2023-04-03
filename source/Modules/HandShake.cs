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

        private readonly Lazy<UdpClient> UdpClient = new(() =>
        {
            return new UdpClient(AddressFamily.InterNetwork);
        });

        private readonly Lazy<UdpClient> UdpClient6 = new(() =>
        {
            return new UdpClient(AddressFamily.InterNetworkV6);
        });

        private volatile int Round = 0;

        public HandShake() { }

        public void Dispose()
        {
            Cancel();

            if (this.UdpClient.IsValueCreated)
            {
                UdpClient.Value.Close();
            }

            if (this.UdpClient6.IsValueCreated)
            {
                UdpClient6.Value.Close();
            }
        }

        public static string GetMachineName()
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

        private static byte[] MakePackage(bool OldConnection, string? token, IEnumerable<SavedPhones.Phone>? targets)
        {
            List<string>? TargetIds = null;
            if (targets != null)
            {
                TargetIds = new List<string>();

                foreach (SavedPhones.Phone t in targets)
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
                    UdpClient6.Value.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
                }
                catch (Exception)
                {
                    continue;
                }

            foreach (string ip in Ipv4BroadcastAddresses)
                try
                {
                    UdpClient.Value.Send(data, data.Length, ip, DEFAULT_PORT + portIncrement);
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
                        UdpClient6.Value.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                    else if (phone.LastIp.Contains('.'))
                    {
                        UdpClient.Value.Send(data, data.Length, phone.LastIp, DEFAULT_PORT + portIncrement);
                    }
                }
                catch (Exception) { }
            }
        }

        public async void Reach(int timeout, bool OldConnection, IEnumerable<SavedPhones.Phone>? targets)
        {
            int ThisRound = Interlocked.Increment(ref Round);

            byte[] data = MakePackage(OldConnection, null, targets);

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
                await Task.Delay(2500);
            }
        }

        public void Cancel()
        {
            Interlocked.Increment(ref Round);
        }
    }
}
