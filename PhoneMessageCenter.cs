using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Windows;

namespace FnSync
{
    class PhoneMessageCenter
    {
        public const string MSG_FAKE_TYPE_ON_CONNECTED = "fake_type_on_connected";
        public const string MSG_FAKE_TYPE_ON_DISCONNECTED = "fake_type_on_disconnected";
        public const string MSG_FAKE_TYPE_ON_CONNECTION_FAILED = "fake_type_on_connection_failed";
        public const string MSG_FAKE_TYPE_ON_NAME_CHANGED = "fake_type_on_name_changed";
        public const string MSG_FAKE_TYPE_ON_REMOVED = "fake_type_on_removed";

        public const string MSG_TYPE_NEW_NOTIFICATION = "phone_notification_sync";
        public const string MSG_TYPE_SCREEN_LOCKED = "screen_locked";

        public static PhoneMessageCenter Singleton = new PhoneMessageCenter();

        private class Condition
        {
            public enum ConditionType
            {
                Universal,
                PerIDAllType,
                PerTypeAllID,
                Specific
            }

            public String MsgType { get; }
            public String Id { get; }

            public ConditionType Type
            {
                get
                {
                    if (MsgType == null && Id == null)
                    {
                        return ConditionType.Universal;
                    }
                    else if (MsgType == null)
                    {
                        return ConditionType.PerIDAllType;
                    }
                    else if (Id == null)
                    {
                        return ConditionType.PerTypeAllID;
                    }
                    else
                    {
                        return ConditionType.Specific;
                    }
                }
            }

            public Condition(string msgType, string id)
            {
                this.MsgType = msgType;
                this.Id = id;
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }
                else if (obj is Condition target)
                {
                    return String.Equals(this.MsgType, target.MsgType) && String.Equals(this.Id, target.Id);
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                if (MsgType == null && Id == null)
                {
                    return 0;
                }
                else if (MsgType == null)
                {
                    return Id.GetHashCode();
                }
                else if (Id == null)
                {
                    return MsgType.GetHashCode();
                }
                else
                {
                    return Id.GetHashCode() ^ MsgType.GetHashCode();
                }
            }
        }

        public delegate void Action(string id, string msgType, object msg, PhoneClient client);

        private class ActionDescriptor
        {
            public Action action;
            public bool OnMainThread;
            public int Order = 0;

            public override bool Equals(object obj)
            {
                return (obj is ActionDescriptor descriptor) && action.Equals(descriptor.action);
            }

            public override int GetHashCode()
            {
                return action.GetHashCode();
            }
        }

        private readonly HashSet<ActionDescriptor> Universals = new HashSet<ActionDescriptor>();
        private readonly Dictionary<Condition, HashSet<ActionDescriptor>> AllTypes = new Dictionary<Condition, HashSet<ActionDescriptor>>();
        private readonly Dictionary<Condition, HashSet<ActionDescriptor>> AllIds = new Dictionary<Condition, HashSet<ActionDescriptor>>();
        private readonly Dictionary<Condition, HashSet<ActionDescriptor>> Specifics = new Dictionary<Condition, HashSet<ActionDescriptor>>();

        private PhoneMessageCenter()
        {
        }

        public void Register(string id, string msgType, Action action, bool OnMainThread, int Order = 0)
        {
            if (action == null)
            {
                return;
            }

            Condition condition = new Condition(msgType, id);
            ActionDescriptor descriptor = new ActionDescriptor()
            {
                action = action,
                OnMainThread = OnMainThread,
                Order = Order
            };

            lock (this)
            {
                switch (condition.Type)
                {
                    case Condition.ConditionType.Universal:
                        Universals.Add(descriptor);
                        break;

                    case Condition.ConditionType.PerIDAllType:
                        if (!AllTypes.ContainsKey(condition))
                        {
                            AllTypes.Add(condition, new HashSet<ActionDescriptor>());
                        }

                        AllTypes[condition].Add(descriptor);
                        break;

                    case Condition.ConditionType.PerTypeAllID:
                        if (!AllIds.ContainsKey(condition))
                        {
                            AllIds.Add(condition, new HashSet<ActionDescriptor>());
                        }

                        AllIds[condition].Add(descriptor);
                        break;

                    case Condition.ConditionType.Specific:
                        if (!Specifics.ContainsKey(condition))
                        {
                            Specifics.Add(condition, new HashSet<ActionDescriptor>());
                        }

                        Specifics[condition].Add(descriptor);
                        break;
                }
            }
        }

        public void Unregister(string id, string msgType, Action action)
        {
            Condition condition = new Condition(msgType, id);
            ActionDescriptor descriptor = new ActionDescriptor()
            {
                action = action,
                OnMainThread = false
            };

            lock (this)
            {
                switch (condition.Type)
                {
                    case Condition.ConditionType.Universal:
                        Universals.Remove(descriptor);
                        break;

                    case Condition.ConditionType.PerIDAllType:
                        if (AllTypes.ContainsKey(condition)) AllTypes[condition].Remove(descriptor);
                        break;

                    case Condition.ConditionType.PerTypeAllID:
                        if (AllIds.ContainsKey(condition)) AllIds[condition].Remove(descriptor);
                        break;

                    case Condition.ConditionType.Specific:
                        if (Specifics.ContainsKey(condition)) Specifics[condition].Remove(descriptor);
                        break;
                }
            }
        }

        private void InvockAll(IEnumerable<ActionDescriptor> set, string id, string msgType, object msg, PhoneClient client)
        {
            try
            {
                foreach (ActionDescriptor descriptor in set)
                {
#if DEBUG
                    Application.Current.Dispatcher.InvokeIfNecessaryWithThrow(delegate
#else
                    Application.Current.Dispatcher.InvokeIfNecessaryNoThrow(delegate
#endif
                    {
                        descriptor.action.Invoke(id, msgType, msg, client);
                    }, descriptor.OnMainThread);
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        private class ActionDescriptorPriority : IComparer<ActionDescriptor>
        {
            public static readonly ActionDescriptorPriority Comparer = new ActionDescriptorPriority();
            public int Compare(ActionDescriptor x, ActionDescriptor y)
            {
                int OrderCompare = x.Order.CompareTo(y.Order);
                int ActionCompare = Math.Sign(x.action.GetHashCode() - y.action.GetHashCode());
                return OrderCompare != 0 ? OrderCompare : ActionCompare;
            }
        }

        public void Raise(string id, string msgType, object msg, PhoneClient client)
        {
            List<ActionDescriptor> descriptors = new List<ActionDescriptor>();

            lock (this)
            {
                Condition specific = new Condition(msgType, id);
                if (Specifics.ContainsKey(specific))
                    descriptors.AddRange(Specifics[specific]);

                Condition allType = new Condition(null, id);
                if (AllTypes.ContainsKey(allType))
                    descriptors.AddRange(AllTypes[allType]);

                Condition allId = new Condition(msgType, null);
                if (AllIds.ContainsKey(allId))
                    descriptors.AddRange(AllIds[allId]);

                if (Universals.Any())
                    descriptors.AddRange(Universals);
            }

            descriptors.Sort(ActionDescriptorPriority.Comparer);
            InvockAll(descriptors, id, msgType, msg, client);
        }

        public static void LockScreen(string id, string msgType, object msg, PhoneClient client)
        {
            Utils.LockWorkStation();
            client.SendMsgNoThrow(MSG_TYPE_SCREEN_LOCKED);
        }
    }
}
