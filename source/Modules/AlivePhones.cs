using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace FnSync
{
    class AlivePhones : IEnumerable<PhoneClient>
    {
        public static readonly AlivePhones Singleton = new AlivePhones();

        private readonly ConcurrentDictionary<string, PhoneClient> map = new ConcurrentDictionary<string, PhoneClient>();

        public PhoneClient? this[string? id]
        {
            get
            {
                return TryGet(id);
            }
        }

        public long AliveCount
        {
            get
            {
                int Count = 0;
                foreach(PhoneClient client in this)
                {
                    if (client.IsAlive)
                        ++Count;
                }

                return Count;
            }
        }

        public int Count => map.Count;

        private AlivePhones()
        {
        }

        public bool Contains(string id)
        {
            return map.ContainsKey(id);
        }

        public PhoneClient? AddOrUpdate(string id, PhoneClient phone)
        {
            PhoneClient? old = null;
            map.AddOrUpdate(id, phone, (k, v) => { old = v; return phone; });
            return old;
        }

        public PhoneClient? TryGet(string? id)
        {
            if (id == null)
            {
                return null;
            }

            if (map.TryGetValue(id, out PhoneClient? phone))
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
                kv.Value.Dispose();
            }
        }

        public void PushMsg(JObject msg, string msgType)
        {
            foreach (var kv in map)
            {
                if (kv.Value.IsAlive)
                {
                    kv.Value.SendMsg(msg, msgType);
                }
            }
        }

        public bool IsOnline(string id, [MaybeNullWhen(false)] out PhoneClient? Client)
        {
            Client = this[id];
            return Client?.IsAlive == true;
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
