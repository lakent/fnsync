using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace FnSync
{
    class PcListener
    {
        public static readonly PcListener Singleton = new PcListener();

        public static readonly int FIRST_ACCEPT_TIMEOUT_MILLS = 20 * 1000;

        private String Code = null;

        private Socket Listener = null;
        public int Port { get; private set; } = 0;
        private HandShake HandShaker = null;

        private bool ListenOnPort(int p)
        {
            Port = p;

            try
            {
                Listener.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));
                Listener.Listen(6);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ReInit()
        {
            Listener?.Shutdown(SocketShutdown.Both);
            Listener?.Close();

            Listener = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                DualMode = true
            };

            int ConfigPort = MainConfig.Config.FixedListenPort;
            if (ConfigPort > 0)
            {
                while (!ListenOnPort((ConfigPort++) % 65536));
            }
            else
            {
                while (!ListenOnPort(Unirandom.Next(10000, 60000)));
            }

            // http://www.lybecker.com/blog/2018/08/23/supporting-ipv4-and-ipv6-dual-mode-network-with-one-socket/
            HandShaker = new HandShake();
            SelectTask.Singleton.AddOrUpdate(Listener, true, false, 0, OnAccpet, false);
        }

        private PcListener()
        {
            ReInit();
            NetworkChangedHandler.Init();
        }

        public void StopReach()
        {
            //WindowConnect.ControlCallback.OnDisconnected(null);
            HandShaker.Cancel();
        }

        public void SetCode(string code)
        {
            this.Code = code;
        }

        public void StartReachInitiatively(String code, bool OldConnection, SavedPhones.Phone[] targets)
        {
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

        private SelectTask.Result OnAccpet(Object target, bool read, bool write, bool error)
        {
            if (error) { ReInit(); return SelectTask.Result.RETREAT; }

            if (read)
            {
                Socket client = Listener.Accept();
                new PhoneClient(client, Code);
            }

            return SelectTask.Result.KEEP;
        }
    }
}
