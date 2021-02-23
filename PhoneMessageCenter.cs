using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        public const string MSG_TYPE_UNSUPPORTED_OPERATION = "unsupported_operation";

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

        public async Task<bool> OneShotGetBoolean(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, bool DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, ExpectedType, TimeoutMillseconds);
            return msg.OptBool(Key, DefVal);
        }

        public async Task<long> OneShotGetLong(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, long DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, ExpectedType, TimeoutMillseconds);
            return msg.OptLong(Key, DefVal);
        }

        public async Task<string> OneShotGetString(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, string DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, ExpectedType, TimeoutMillseconds);
            return msg.OptString(Key, DefVal);
        }

        public async Task<JObject> OneShotMsgPart(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds)
        {
            Object msgObj = await OneShot(Client, Msg, MsgType, ExpectedType, TimeoutMillseconds);
            if (msgObj is JObject msg)
            {
                return msg;
            }
            else if (msgObj is IMessageWithBinary mwb)
            {
                return mwb.Message;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public enum RequestStatus
        {
            SUCCESSFUL = 0,
            TIMEOUT,
            UNSUPPORTED_OPERATION,
            DISCONNECTED,
        }

        public class PhoneDisconnectedException : Exception { }

        public Task<Object> OneShot(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds)
        {
            TaskCompletionSource<Object> completionSource = new TaskCompletionSource<Object>();

            OneShot(Client, Msg, MsgType, ExpectedType, TimeoutMillseconds,
                delegate (JObject msg, byte[] bin, Object msgObj, RequestStatus Status)
            {
                switch (Status)
                {
                    case RequestStatus.SUCCESSFUL:
                        completionSource.SetResult(msgObj);
                        break;

                    case RequestStatus.UNSUPPORTED_OPERATION:
                        completionSource.SetException(new NotSupportedException(MsgType));
                        break;

                    case RequestStatus.TIMEOUT:
                        completionSource.SetException(new TimeoutException());
                        break;

                    case RequestStatus.DISCONNECTED:
                        completionSource.SetException(new PhoneDisconnectedException());
                        break;
                }
            }, false);

            return completionSource.Task;
        }

        public void OneShot(PhoneClient Client, JObject MsgTo, string MsgToType, string ExpectedType, int TimeoutMillseconds, System.Action<JObject, byte[], Object, RequestStatus> OnDone, bool OnMainThread)
        {
            if (OnDone == null)
            {
                throw new ArgumentNullException("OnDone");
            }

            string RequestToken = Guid.NewGuid().ToString();
            MsgTo["_request_token"] = RequestToken;

            AutoDisposableTimer CleanUp = null;

            JObject FinalMsg = null;
            byte[] FinalBinary = null;
            Object FinalObj = null;

            void CheckAndExecute(string _, string msgType, object MsgObjFrom, PhoneClient __)
            {
                if (MsgObjFrom is JObject)
                {
                    FinalMsg = MsgObjFrom as JObject;
                }
                else if (MsgObjFrom is IMessageWithBinary)
                {
                    FinalMsg = (MsgObjFrom as IMessageWithBinary).Message;
                    FinalBinary = (MsgObjFrom as IMessageWithBinary).Binary;
                }
                else
                {
                    return;
                }

                if (FinalMsg.OptString("_request_token", null) != RequestToken)
                {
                    return;
                }

                FinalObj = MsgObjFrom;
                CleanUp?.Dispose(msgType);
            }

            void OnDisconnected(string _, string __, object ___, PhoneClient ____)
            {
                CleanUp?.Dispose(MSG_FAKE_TYPE_ON_DISCONNECTED);
            }

            Register(Client.Id, ExpectedType, CheckAndExecute, OnMainThread);
            Register(Client.Id, MSG_FAKE_TYPE_ON_DISCONNECTED, OnDisconnected, OnMainThread);

            try
            {
                Client.SendMsg(MsgTo, MsgToType);
            }
            catch (SocketException e)
            {
                OnDone.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                return;
            }
            catch (ObjectDisposedException e)
            {
                OnDone.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                return;
            }

            CleanUp = new AutoDisposableTimer(null, TimeoutMillseconds, false);
            CleanUp.DisposedEvent += delegate (object _, Object State)
            {
                Unregister(Client.Id, ExpectedType, CheckAndExecute);
                Unregister(Client.Id, MSG_FAKE_TYPE_ON_DISCONNECTED, OnDisconnected);
                if (MSG_TYPE_UNSUPPORTED_OPERATION.Equals(State))
                    OnDone.Invoke(FinalMsg, FinalBinary, FinalObj, RequestStatus.UNSUPPORTED_OPERATION);
                else if (MSG_FAKE_TYPE_ON_DISCONNECTED.Equals(State))
                    OnDone.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                else if (State != null)
                    OnDone.Invoke(FinalMsg, FinalBinary, FinalObj, RequestStatus.SUCCESSFUL);
                else
                    OnDone.Invoke(null, null, null, RequestStatus.TIMEOUT);
            };

            CleanUp.Start();
        }

        private void InvockAll(IEnumerable<ActionDescriptor> set, string id, string msgType, object msg, PhoneClient client)
        {
            foreach (ActionDescriptor descriptor in set)
            {
                try
                {
                    if (descriptor.OnMainThread)
                    {
                        App.FakeDispatcher.Invoke(delegate
                        {
                            descriptor.action.Invoke(id, msgType, msg, client);
                            return null;
                        });
                    }
                    else
                    {
                        descriptor.action.Invoke(id, msgType, msg, client);
                    };
                }
                catch (Exception e)
                {
#if DEBUG
                    WindowUnhandledException.ShowException(e);
#endif
                    return;
                }
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
            string lookUpType;

            if (msgType == MSG_TYPE_UNSUPPORTED_OPERATION && msg is JObject jmsg)
            {
                if (!jmsg.ContainsKey("intention"))
                    return;

                lookUpType = (string)jmsg["intention"];
            }
            else
            {
                lookUpType = msgType;
            }

            List<ActionDescriptor> descriptors = new List<ActionDescriptor>();

            lock (this)
            {
                Condition specific = new Condition(lookUpType, id);
                if (Specifics.ContainsKey(specific))
                    descriptors.AddRange(Specifics[specific]);

                Condition allType = new Condition(null, id);
                if (AllTypes.ContainsKey(allType))
                    descriptors.AddRange(AllTypes[allType]);

                Condition allId = new Condition(lookUpType, null);
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
