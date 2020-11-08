using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using Windows.Devices.PointOfService;

namespace FnSync
{
    class AlivePhones : IEnumerable<PhoneClient>
    {
        public static readonly AlivePhones Singleton = new AlivePhones();

        private ConcurrentDictionary<string, PhoneClient> map = new ConcurrentDictionary<string, PhoneClient>();

        public PhoneClient this[string id]
        {
            get
            {
                return TryGet(id);
            }
        }

        private long aliveCount = 0;
        public long AliveCount
        {
            get
            {
                return Interlocked.Read(ref aliveCount);
            }
        }

        public void IncrementAlive()
        {
            Interlocked.Increment(ref aliveCount);
        }

        public void DecrementAlive()
        {
            Interlocked.Decrement(ref aliveCount);
        }

        public int Count => map.Count;

        private AlivePhones()
        {
        }

        public bool Contains(string id)
        {
            return map.ContainsKey(id);
        }

        public PhoneClient AddOrUpdate(string id, PhoneClient phone)
        {
            PhoneClient old = null;
            map.AddOrUpdate(id, phone, (k, v) => { old = v; return phone; });
            return old;
        }

        public PhoneClient TryGet(string id)
        {
            PhoneClient phone;
            if (map.TryGetValue(id, out phone))
            {
                return phone;
            }
            else
            {
                return null;
            }
        }

        public void Remove(string id)
        {
            map.TryRemove(id, out PhoneClient _);
        }

        public void DisconnectAll()
        {
            foreach (var kv in map)
            {
                kv.Value.RetreatAndDispose();
            }
        }

        public void PushMsg(JObject msg, String msgType)
        {
            foreach (var kv in map)
            {
                if (kv.Value.IsAlive)
                {
                    kv.Value.SendMsg(msg, msgType);
                }
            }
        }

        public IEnumerator<PhoneClient> GetEnumerator()
        {
            return map.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
