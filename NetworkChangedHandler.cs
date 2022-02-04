using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Preview.Notes;

namespace FnSync
{
    static class NetworkChangedHandler
    {
        private static readonly object Lock = new object();

        public static void Init()
        {
            NetworkChange.NetworkAddressChanged += new
                         NetworkAddressChangedEventHandler(AddressChangedCallback);
        }

        private static async void AddressChangedCallback(object sender, EventArgs e)
        {
            const int DELAY_MILLS = 5000;

            if (!Monitor.TryEnter(Lock, 0))
            {
                return;
            }

            Task FinalDelay = Task.Delay(DELAY_MILLS);

            try
            {
                List<KeyValuePair<PhoneClient, Task<bool>>> Results = new List<KeyValuePair<PhoneClient, Task<bool>>>();

                foreach (PhoneClient c in AlivePhones.Singleton)
                {
                    Results.Add(
                        new KeyValuePair<PhoneClient, Task<bool>>(c, c.ProbeAlive(DELAY_MILLS))
                        );
                }

                List<SavedPhones.Phone> Targets = new List<SavedPhones.Phone>();

                foreach (KeyValuePair<PhoneClient, Task<bool>> p in Results)
                {
                    bool r = await p.Value;
                    if (!r)
                    {
                        Targets.Add(SavedPhones.Singleton[p.Key.Id]);
                    }
                }

                PhoneListener.Singleton.StartReachInitiatively(null, true, Targets);

                await FinalDelay;
            }
            finally
            {
                Monitor.Exit(Lock);
            }
        }
    }
}
