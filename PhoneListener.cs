using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace FnSync
{
    class PhoneListener
    {
        public static readonly PhoneListener Singleton = new PhoneListener();

        public static readonly int FIRST_ACCEPT_TIMEOUT_MILLS = 20 * 1000;

        public String Code = null;

        private TcpListener Listener = null;
        public int Port { get; private set; } = 0;
        private HandShake HandShaker = null;

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
                return false;
            }
        }

        private void ReInit()
        {
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
            HandShaker = new HandShake();
            StartAccept();
        }

        private PhoneListener()
        {
            ReInit();
            NetworkChangedHandler.Init();
        }

        public void StopReach()
        {
            //WindowConnect.ControlCallback.OnDisconnected(null);
            HandShaker.Cancel();
        }

        public void StartReachInitiatively(String code, bool OldConnection, IEnumerable<SavedPhones.Phone> targets)
        {
            if( targets != null && !targets.Any())
            {
                return;
            }

            if (!OldConnection)
            {
                if (code == null)
                {
                    throw new ArgumentNullException();
                }
            }
            else
            {
                if (code == null)
                {
                    code = "*";
                }
            }

            this.Code = code;

            //WindowConnect.ControlCallback.Connecting();
            HandShaker.Reach(FIRST_ACCEPT_TIMEOUT_MILLS, OldConnection, targets);
        }

        private async void StartAccept()
        {
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
                catch (Exception e)
                {
                    ReInit();
                    return;
                }
            }
        }
    }
}
