using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    public class PhoneMessageCenter : QueuedAsyncTask<PhoneMessageCenter.ControlDescriptor, object>
    {
        public interface ClientWrapper
        {
            PhoneClient Client { get; set; }
        }

        public class ControlDescriptor
        {
            public struct AdditionDescriptor
            {
                public string id;
                public string msgType;
                public string[] msgTypes;
                public MsgAction action;
                public bool OnMainThread;
                public int Order;

                public AdditionDescriptor(string id, string msgType, string[] msgTypes, MsgAction action, bool OnMainThread, int Order = 0)
                {
                    this.id = id;
                    this.msgType = msgType;
                    this.msgTypes = msgTypes;
                    this.action = action;
                    this.OnMainThread = OnMainThread;
                    this.Order = Order;
                }
            }

            public struct DeletionDescriptor
            {
                public string id;
                public string msgType;
                public string[] msgTypes;
                public MsgAction action;

                public DeletionDescriptor(string id, string msgType, string[] msgTypes, MsgAction action)
                {
                    this.id = id;
                    this.msgType = msgType;
                    this.msgTypes = msgTypes;
                    this.action = action;
                }
            }

            public struct RaisingDescriptor
            {
                public string id;
                public string msgType;
                public object msg;
                public PhoneClient client;

                public RaisingDescriptor(string id, string msgType, object msg, PhoneClient client)
                {
                    this.id = id;
                    this.msgType = msgType;
                    this.msg = msg;
                    this.client = client;
                }
            }

            public readonly AdditionDescriptor? Addition;
            public readonly DeletionDescriptor? Deletion;
            public readonly RaisingDescriptor? Raising;

            public ControlDescriptor(AdditionDescriptor Addition)
            {
                this.Addition = Addition;
                this.Deletion = null;
                this.Raising = null;
            }

            public ControlDescriptor(DeletionDescriptor Deletion)
            {
                this.Addition = null;
                this.Deletion = Deletion;
                this.Raising = null;
            }

            public ControlDescriptor(RaisingDescriptor Raising)
            {
                this.Addition = null;
                this.Deletion = null;
                this.Raising = Raising;
            }
        }

        public const string MSG_FAKE_TYPE_ON_CONNECTED = "fake_type_on_connected";
        public const string MSG_FAKE_TYPE_ON_DISCONNECTED = "fake_type_on_disconnected";
        public const string MSG_FAKE_TYPE_ON_CONNECTION_FAILED = "fake_type_on_connection_failed";
        public const string MSG_FAKE_TYPE_ON_NAME_CHANGED = "fake_type_on_name_changed";
        public const string MSG_FAKE_TYPE_ON_REMOVED = "fake_type_on_removed";

        public const string MSG_TYPE_NEW_NOTIFICATION = "phone_notification_sync";
        public const string MSG_TYPE_SCREEN_LOCKED = "screen_locked";

        public const string MSG_TYPE_UNSUPPORTED_OPERATION = "unsupported_operation";

        public static PhoneMessageCenter Singleton = new PhoneMessageCenter();

        private static string IGNORED_MESSAGE = "IGNORED_MESSAGE";

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

        public delegate void MsgAction(string id, string msgType, object msg, PhoneClient client);

        private class ActionDescriptor
        {
            public MsgAction action;
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

        private PhoneMessageCenter() : base(true)
        {
            new Thread(

                () =>
                {
                    this.StartLoop();
                }

                ).Start();
        }

        public void Register(string id, string msgType, MsgAction action, bool OnMainThread, int Order = 0)
        {
            if (action == null)
            {
                return;
            }

            this.InputManually(
                new ControlDescriptor(new ControlDescriptor.AdditionDescriptor(id, msgType, null, action, OnMainThread, Order))
                );
        }

        public void Register(string id, string[] msgTypes, MsgAction action, bool OnMainThread, int Order = 0)
        {
            if (action == null)
            {
                return;
            }

            this.InputManually(
                new ControlDescriptor(new ControlDescriptor.AdditionDescriptor(id, IGNORED_MESSAGE, msgTypes, action, OnMainThread, Order))
                );
        }

        private void Register(string MsgType, ControlDescriptor.AdditionDescriptor Descriptor)
        {
            if (MsgType == IGNORED_MESSAGE)
            {
                return;
            }

            Condition condition = new Condition(MsgType, Descriptor.id);
            ActionDescriptor descriptor = new ActionDescriptor()
            {
                action = Descriptor.action,
                OnMainThread = Descriptor.OnMainThread,
                Order = Descriptor.Order
            };

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
        private void Register(ControlDescriptor.AdditionDescriptor Descriptor)
        {
            Register(Descriptor.msgType, Descriptor);
            if (Descriptor.msgTypes != null)
            {
                foreach (string MsgType in Descriptor.msgTypes)
                {
                    Register(MsgType, Descriptor);
                }
            }
        }

        public void Unregister(string id, string msgType, MsgAction action)
        {
            if (action == null)
            {
                return;
            }

            this.InputManually(
                new ControlDescriptor(new ControlDescriptor.DeletionDescriptor(id, msgType, null, action))
                );
        }

        public void Unregister(string id, string[] msgTypes, MsgAction action)
        {
            if (action == null)
            {
                return;
            }

            this.InputManually(
                new ControlDescriptor(new ControlDescriptor.DeletionDescriptor(id, IGNORED_MESSAGE, msgTypes, action))
                );
        }


        private void Unregister(string MsgType, ControlDescriptor.DeletionDescriptor Descriptor)
        {
            Condition condition = new Condition(MsgType, Descriptor.id);
            ActionDescriptor descriptor = new ActionDescriptor()
            {
                action = Descriptor.action,
                OnMainThread = false
            };

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
        private void Unregister(ControlDescriptor.DeletionDescriptor Descriptor)
        {
            Unregister(Descriptor.msgType, Descriptor);
            if (Descriptor.msgTypes != null)
            {
                foreach (string MsgType in Descriptor.msgTypes)
                {
                    Unregister(MsgType, Descriptor);
                }
            }
        }

        public async Task<bool> OneShotGetBoolean(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, bool DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, null, ExpectedType, TimeoutMillseconds);
            return msg.OptBool(Key, DefVal);
        }

        public async Task<long> OneShotGetLong(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, long DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, null, ExpectedType, TimeoutMillseconds);
            return msg.OptLong(Key, DefVal);
        }

        public async Task<string> OneShotGetString(PhoneClient Client, JObject Msg, string MsgType, string ExpectedType, int TimeoutMillseconds, string Key, string DefVal)
        {
            JObject msg = await OneShotMsgPart(Client, Msg, MsgType, null, ExpectedType, TimeoutMillseconds);
            return msg.OptString(Key, DefVal);
        }

        public async Task<JObject> OneShotMsgPart(PhoneClient Client, JObject Msg, string MsgType, byte[] binary, string ExpectedType, int TimeoutMillseconds)
        {
            Object msgObj = await OneShot(Client, Msg, MsgType, binary, ExpectedType, TimeoutMillseconds);
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
            OLD_CLIENT_HOLDER,
        }

        public class DisconnectedException : Exception { }
        public class OldClientHolderException : Exception
        {
            public readonly string ClientId;

            public PhoneClient Current => AlivePhones.Singleton[this.ClientId];

            public OldClientHolderException(string ClientId)
            {
                this.ClientId = ClientId;
            }

            public OldClientHolderException(PhoneClient Client) : this(Client.Id)
            {
            }
        }

        public Task<Object> OneShot(
            PhoneClient Client,
            JObject Msg,
            string MsgType,
            byte[] binary,
            string ExpectedType,
            int TimeoutMillseconds)
        {
            TaskCompletionSource<Object> completionSource = new TaskCompletionSource<Object>();

            OneShot(Client, Msg, MsgType, binary, ExpectedType, TimeoutMillseconds,
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
                        completionSource.SetException(new DisconnectedException());
                        break;

                    case RequestStatus.OLD_CLIENT_HOLDER:
                        completionSource.SetException(new OldClientHolderException(Client));
                        break;

                    default:
                        completionSource.SetException(new InvalidOperationException());
                        break;
                }
            }, false);

            return completionSource.Task;
        }

        public delegate void OneShotAction(JObject Msg, byte[] Binary, Object MsgObj, RequestStatus Status);

        private static void InvockOnMainThreadConditionly(bool On, OneShotAction Action, JObject Msg, byte[] Binary, Object MsgObj, RequestStatus Status)
        {
            if (On && !FakeDispatcher.IsOnUIThread())
            {
                App.FakeDispatcher.Invoke(delegate
                {
                    Action.Invoke(Msg, Binary, MsgObj, Status);
                    return null;
                });
            }
            else
            {
                Action.Invoke(Msg, Binary, MsgObj, Status);
            };
        }

        public void OneShot(
            PhoneClient Client, JObject DispatchedMsg,
            string DispatchedType, byte[] DispatchedBinary,
            string ExpectedType, int TimeoutMillseconds,
            OneShotAction OnDoneCallback, bool OnMainThread
            )
        {
            if (OnDoneCallback == null)
            {
                throw new ArgumentNullException("OnDone");
            }

            if (DispatchedMsg == null)
            {
                DispatchedMsg = new JObject();
            }

            string RequestToken = Guid.NewGuid().ToString();
            DispatchedMsg["_request_token"] = RequestToken;

            AutoDisposableTimer FireAndCleanUp = new AutoDisposableTimer(null, TimeoutMillseconds, null, false);

            JObject FinalMsg = null;
            byte[] FinalBinary = null;
            object FinalObj = null;

            void MessageCallbackWrapper(string _, string ReceivedType, object ReceivedMsgObj, PhoneClient __)
            {
                if (ReceivedType == MSG_FAKE_TYPE_ON_DISCONNECTED)
                {
                    FireAndCleanUp?.Dispose(MSG_FAKE_TYPE_ON_DISCONNECTED);
                    return;
                }

                if (ReceivedMsgObj is JObject)
                {
                    FinalMsg = ReceivedMsgObj as JObject;
                }
                else if (ReceivedMsgObj is IMessageWithBinary)
                {
                    FinalMsg = (ReceivedMsgObj as IMessageWithBinary).Message;
                    FinalBinary = (ReceivedMsgObj as IMessageWithBinary).Binary;
                }
                else
                {
                    return;
                }

                if (FinalMsg.OptString("_request_token", null) != RequestToken)
                {
                    return;
                }

                FinalObj = ReceivedMsgObj;
                FireAndCleanUp?.Dispose(ReceivedType);
            }

            string[] Types = new[] { ExpectedType, MSG_FAKE_TYPE_ON_DISCONNECTED };

            Register(Client.Id, Types, MessageCallbackWrapper, OnMainThread);

            // This event binding must be performed after the above two registrations, or memory leaks may happen.
            FireAndCleanUp.DisposedEvent += delegate (object _, Object State)
            {
                Unregister(Client.Id, Types, MessageCallbackWrapper);

                if (MSG_TYPE_UNSUPPORTED_OPERATION.Equals(State))
                {
                    //OnDoneCallback.Invoke(FinalMsg, FinalBinary, FinalObj, RequestStatus.UNSUPPORTED_OPERATION);
                    InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, FinalMsg, FinalBinary, FinalObj, RequestStatus.UNSUPPORTED_OPERATION);
                }
                else if (MSG_FAKE_TYPE_ON_DISCONNECTED.Equals(State))
                {
                    //OnDoneCallback.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                    InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, null, null, null, RequestStatus.DISCONNECTED);
                }
                else if (State != null)
                {
                    //OnDoneCallback.Invoke(FinalMsg, FinalBinary, FinalObj, RequestStatus.SUCCESSFUL);
                    InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, FinalMsg, FinalBinary, FinalObj, RequestStatus.SUCCESSFUL);
                }
                else // State == null
                {
                    //OnDoneCallback.Invoke(null, null, null, RequestStatus.TIMEOUT);
                    InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, null, null, null, RequestStatus.TIMEOUT);
                }
            };

            try
            {
                Client.SendMsg(DispatchedMsg, DispatchedType, DispatchedBinary);
            }
            catch (SocketException e)
            {
                //OnDoneCallback.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, null, null, null, RequestStatus.OLD_CLIENT_HOLDER);
                return;
            }
            catch (ObjectDisposedException e)
            {
                //OnDoneCallback.Invoke(null, null, null, RequestStatus.DISCONNECTED);
                InvockOnMainThreadConditionly(OnMainThread, OnDoneCallback, null, null, null, RequestStatus.OLD_CLIENT_HOLDER);
                return;
            }

            FireAndCleanUp.Start();
        }

        public Task<object> WaitForMessage(string id, string MessageType, int TimeoutMillseconds, Func<object, bool> Callback = null, int Count = 1, CancellationToken? Cancellation = null)
        {
            if (!AlivePhones.Singleton.IsOnline(id, out PhoneClient _))
            {
                // Check it priorly to prevent unnecessary event registration.
                return Task.FromException<object>(new DisconnectedException());
            }

            TaskCompletionSource<Object> completionSource = new TaskCompletionSource<object>();

            AutoDisposableTimer FireAndCleanUp = new AutoDisposableTimer(null, TimeoutMillseconds, Cancellation, false);

            int LeftCount = Count;

            void MessageCallback(string _, string ReceivedType, object ReceivedMsgObj, PhoneClient Client)
            {
                if (ReceivedType == MSG_FAKE_TYPE_ON_DISCONNECTED)
                {
                    FireAndCleanUp.Dispose(new DisconnectedException());
                }
                else
                {
                    try
                    {
                        bool? ret = Callback?.Invoke(ReceivedMsgObj);
                        if (ret == false)
                        {
                            FireAndCleanUp.Dispose(ReceivedMsgObj);
                            return;
                        }
                    }
                    catch (Exception E)
                    {
                        FireAndCleanUp.Dispose(E);
                        return;
                    }

                    LeftCount--;

                    if (LeftCount == 0)
                    {
                        FireAndCleanUp.Dispose(ReceivedMsgObj);
                    }
                }
            }

            string[] Types = new[] { MessageType, MSG_FAKE_TYPE_ON_DISCONNECTED };

            Register(id, Types, MessageCallback, false);

            FireAndCleanUp.DisposedEvent += delegate (object _, Object State)
            {
                Unregister(id, Types, MessageCallback);

                if (State == AutoDisposableTimer.STATE_TIMER_CANCELLED)
                {
                    completionSource.SetException(new OperationCanceledException());
                }
                else if (State == null)
                {
                    completionSource.SetException(new TimeoutException());
                }
                else if (State is Exception E)
                {
                    completionSource.SetException(E);
                }
                else
                {
                    completionSource.SetResult(State);
                }
            };

            FireAndCleanUp.Start();

            if (!AlivePhones.Singleton.IsOnline(id, out PhoneClient _))
            {
                // Check it priorly to prevent unnecessary event registration.
                Exception E = new DisconnectedException();
                FireAndCleanUp.Dispose(E);
                return Task.FromException<object>(E);
            }

            return completionSource.Task;
        }

        public Task<PhoneClient> WaitOnline(string id, int TimeoutMillseconds, CancellationToken? Cancellation = null)
        {
            if (AlivePhones.Singleton.IsOnline(id, out PhoneClient Client1))
            {
                // Check it priorly to prevent unnecessary event registration.
                return Task.FromResult(Client1);
            }

            TaskCompletionSource<PhoneClient> completionSource = new TaskCompletionSource<PhoneClient>();

            AutoDisposableTimer FireAndCleanUp = new AutoDisposableTimer(null, TimeoutMillseconds, Cancellation, false);

            FireAndCleanUp.DisposedEvent += delegate (object _, Object State)
            {
                Unregister(id, MSG_FAKE_TYPE_ON_CONNECTED, MessageCallback);
                if (State == AutoDisposableTimer.STATE_TIMER_CANCELLED)
                {
                    completionSource.SetException(new OperationCanceledException());
                }
                else if (State == null)
                {
                    completionSource.SetException(new TimeoutException());
                }
                else if (State is PhoneClient Client)
                {
                    completionSource.SetResult(Client);
                }
            };

            void MessageCallback(string _, string ReceivedType, object ReceivedMsgObj, PhoneClient Client)
            {
                FireAndCleanUp.Dispose(Client);
            }

            Register(id, MSG_FAKE_TYPE_ON_CONNECTED, MessageCallback, false);

            if (AlivePhones.Singleton.IsOnline(id, out PhoneClient Client2))
            {
                // Check it after event registration regrading that events may out of order on a small scale.
                FireAndCleanUp.Dispose(Client2);
                return Task.FromResult(Client2);
            }
            else
            {
                FireAndCleanUp.Start();
                return completionSource.Task;
            }
        }

        private static void InvockOnMainThreadConditionly(bool On, MsgAction Action, string id, string msgType, object msg, PhoneClient client)
        {
            if (On && !FakeDispatcher.IsOnUIThread())
            {
                Task t = App.FakeDispatcher.InvokeAwaitable(delegate
                {
                    Action.Invoke(id, msgType, msg, client);
                    return null;
                });

                t.Wait();
            }
            else
            {
                Action.Invoke(id, msgType, msg, client);
            };
        }

        private void InvockAll(IEnumerable<ActionDescriptor> set, string id, string msgType, object msg, PhoneClient client)
        {
            foreach (ActionDescriptor descriptor in set)
            {
                try
                {
                    InvockOnMainThreadConditionly(descriptor.OnMainThread, descriptor.action, id, msgType, msg, client);
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
            if (id == null || msgType == null)
            {
                return;
            }

            this.InputManually(
                new ControlDescriptor(new ControlDescriptor.RaisingDescriptor(id, msgType, msg, client))
                );
        }

        private void Raise(ControlDescriptor.RaisingDescriptor Descriptor)
        {
            string lookUpType;

            if (Descriptor.msgType == MSG_TYPE_UNSUPPORTED_OPERATION && Descriptor.msg is JObject jmsg)
            {
                if (!jmsg.ContainsKey("intention"))
                    return;

                lookUpType = (string)jmsg["intention"];
            }
            else
            {
                lookUpType = Descriptor.msgType;
            }

            List<ActionDescriptor> descriptors = new List<ActionDescriptor>();

            Condition specific = new Condition(lookUpType, Descriptor.id);
            if (Specifics.ContainsKey(specific))
                descriptors.AddRange(Specifics[specific]);

            Condition allType = new Condition(null, Descriptor.id);
            if (AllTypes.ContainsKey(allType))
                descriptors.AddRange(AllTypes[allType]);

            Condition allId = new Condition(lookUpType, null);
            if (AllIds.ContainsKey(allId))
                descriptors.AddRange(AllIds[allId]);

            if (Universals.Any())
                descriptors.AddRange(Universals);

            descriptors.Sort(ActionDescriptorPriority.Comparer);
            InvockAll(descriptors, Descriptor.id, Descriptor.msgType, Descriptor.msg, Descriptor.client);
        }

        protected override Task<ControlDescriptor> InputSource()
        {
            throw new WontOperateThis();
        }

        protected override object TaskBody(ControlDescriptor input)
        {
            return null;
        }

        protected override void OnTaskDone(ControlDescriptor Descriptor, object _)
        {
            if (Descriptor.Addition != null)
            {
                ControlDescriptor.AdditionDescriptor d = Descriptor.Addition.Value;
                Register(d);
            }

            if (Descriptor.Deletion != null)
            {
                ControlDescriptor.DeletionDescriptor d = Descriptor.Deletion.Value;
                Unregister(d);
            }

            if (Descriptor.Raising != null)
            {
                ControlDescriptor.RaisingDescriptor d = Descriptor.Raising.Value;
                Raise(d);
            }
        }

        public static void LockScreen(string id, string msgType, object msg, PhoneClient client)
        {
            Utils.LockWorkStation();
            client.SendMsgNoThrow(MSG_TYPE_SCREEN_LOCKED);
        }
    }

    public static class PhoneMessageCenterExtension
    {
        public static Task<object> OneShot(
            this PhoneClient client,
            JObject Msg,
            string MsgType,
            byte[] binary,
            string ExpectedType,
            int TimeoutMillseconds
            )
        {
            return PhoneMessageCenter.Singleton.OneShot(
                client, Msg, MsgType, binary, ExpectedType, TimeoutMillseconds
                );
        }
    }
}

