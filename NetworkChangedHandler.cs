using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Windows.ApplicationModel.Preview.Notes;

namespace FnSync
{
    static class NetworkChangedHandler
    {
        private static AutoDisposableTimer Timer = null;
        private static readonly object Locker = new object();

        public static void Init()
        {
            NetworkChange.NetworkAddressChanged += new
                         NetworkAddressChangedEventHandler(AddressChangedCallback);
        }

        private static void AddressChangedCallback(object sender, EventArgs e)
        {
            lock (Locker)
            {
                Timer?.Dispose();

                if (AlivePhones.Singleton.Count > 0)
                {
                    Timer = new AutoDisposableTimer(
                        delegate (object state)
                        {
                            ClientListener.Singleton.StartReachInitiatively(null, true, SavedPhones.Singleton.PhoneList.ToArray());
                        }
                        , 5000, false
                    );

                    Timer.DisposedEvent += delegate(object _, object __) 
                    {
                        lock (Locker)
                        {
                            Timer = null;
                        }
                    };

                    Timer.Start();
                }
            }
        }
    }
}
