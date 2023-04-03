using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FnSync
{
    class PhoneListener
    {
        public static readonly PhoneListener Singleton = new();

        public const int FIRST_ACCEPT_TIMEOUT_MILLS = 20 * 1000;

        public string Code { get; set; } = "*";

        private TcpListener? Listener = null!;
        public int Port { get; private set; } = 0;

        private readonly Lazy<HandShake> HandShaker = new(() =>
        {
            return new HandShake();
        });

        private bool ListenOnPort(int p)
        {
            Port = p;

            try
            {
                Listener = new TcpListener(new IPEndPoint(IPAddress.IPv6Any, Port));
                Listener.Server.DualMode = true;
                Listener.Start(10);
                return true;
            }
            catch (Exception)
            {
                Listener?.Stop();
                return false;
            }
        }

        private void ReInit()
        {
            StopReach();
            Listener?.Stop();

            int ConfigPort = MainConfig.Config.FixedListenPort;
            if (ConfigPort > 0)
            {
                while (!ListenOnPort((ConfigPort++) % 65536)) ;
            }
            else
            {
                while (!ListenOnPort(Unirandom.Next(10000, 60000))) ;
            }

            // http://www.lybecker.com/blog/2018/08/23/supporting-ipv4-and-ipv6-dual-mode-network-with-one-socket/
            StartAccepting();
        }

        private PhoneListener()
        {
            ReInit();
            NetworkChangedHandler.Init();

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                AutoReconnectCallback,
                false
                );
        }

        private void AutoReconnectCallback(string id, string msgType, object? msgObject, PhoneClient? client)
        {
            SavedPhones.Phone? saved = SavedPhones.Singleton[id];
            if (saved == null)
            {
                return;
            }

            Task.Run(() =>
            {
                Task.Delay(1000);
                StartReachInitiatively(null, true, new SavedPhones.Phone[] { saved }, 1000);
            });
        }

        public void StopReach()
        {
            if (this.HandShaker.IsValueCreated)
            {
                HandShaker.Value.Cancel();
            }
        }

        public void StartReachInitiatively(
            string? Code,
            bool OldConnection,
            IEnumerable<SavedPhones.Phone>? Targets,
            int timeout = FIRST_ACCEPT_TIMEOUT_MILLS
            )
        {
            if (Targets != null && !Targets.Any())
            {
                return;
            }

            if (!OldConnection)
            {
                if (Code == null)
                {
                    throw new ArgumentNullException(nameof(Code));
                }
            }
            else
            {
                Code ??= "*";
                this.Code = Code;
            }

            StopReach();
            HandShaker.Value.Reach(timeout, OldConnection, Targets);
        }

        private async void StartAccepting()
        {
            if (Listener == null)
            {
                throw new Exception("Listener not inited");
            }

            while (true)
            {
                try
                {
                    TcpClient Client = await Listener.AcceptTcpClientAsync();

                    if (Client != null)
                    {
                        PhoneClient.CreateClient(Client, Code);
                    }
                }
                catch (Exception)
                {
                    ReInit();
                    return;
                }
            }
        }
    }
}

