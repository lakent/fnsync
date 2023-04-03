using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    static class NetworkChangedHandler
    {
        private static readonly object Lock = new();

        public static void Init()
        {
            NetworkChange.NetworkAddressChanged += new
                         NetworkAddressChangedEventHandler(AddressChangedCallback);
        }

        private static void AddressChangedCallback(object? sender, EventArgs e)
        {
            Task.Run(AddressChangedAction);
        }

        private static void AddressChangedAction() // DO NOT use async func
        {
            const int DELAY_MILLS = 5000;

            if (AlivePhones.Singleton.Count == 0)
            {
                return;
            }

            if (!Monitor.TryEnter(Lock, 0))
            {
                return;
            }

            Task FinalDelay = Task.Delay(DELAY_MILLS);

            try
            {
                List<KeyValuePair<PhoneClient, Task<bool>>> Results = new();

                foreach (PhoneClient c in AlivePhones.Singleton)
                {
                    Results.Add(
                        new KeyValuePair<PhoneClient, Task<bool>>(c, c.ProbeAlive(DELAY_MILLS))
                        );
                }

                List<SavedPhones.Phone> Targets = new();

                foreach (KeyValuePair<PhoneClient, Task<bool>> p in Results)
                {
                    p.Value.Wait();
                    if (!p.Value.Result)
                    {
                        Targets.Add(SavedPhones.Singleton[p.Key.Id]!);
                    }
                }

                PhoneListener.Singleton.StartReachInitiatively(null, true, Targets);

                FinalDelay.Wait();
            }
            finally
            {
                Monitor.Exit(Lock);
            }
        }
    }
}
