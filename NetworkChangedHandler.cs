using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Windows.ApplicationModel.Preview.Notes;

namespace FnSync
{
    static class NetworkChangedHandler
    {
        private static long Last = 0;

        public static void Init()
        {
            NetworkChange.NetworkAddressChanged += new
                         NetworkAddressChangedEventHandler(AddressChangedCallback);
        }

        private static void AddressChangedCallback(object sender, EventArgs e)
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (AlivePhones.Singleton.Count > 0 && now - Interlocked.Read(ref Last) > PcListener.FIRST_ACCEPT_TIMEOUT_MILLS)
            {
                Interlocked.Exchange(ref Last, now);
                PcListener.Singleton.StartReachInitiatively(null, true, SavedPhones.Singleton.PhoneList.ToArray());
            }
        }
    }
}
