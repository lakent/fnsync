using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FnSync
{
    public class PhoneClient : LengthPrefixedStreamBuffer, IDisposable
    {
        public const string MSG_ID_KEY = "msgid";
        public const string MSG_TYPE_KEY = "msgtype";
        public const string MSG_TYPE_DISCONNECT_BY_PEER = "disconnect_by_peer";
        public const string MSG_TYPE_LOCK_SCREEN = "lock_screen";
        public const string MSG_TYPE_CONNECTION_ACCEPTED = "connection_accepted";
        public const string MSG_TYPE_HELLO = "hello";
        public const string MSG_TYPE_NONCE = "nonce";

        private static readonly BufferBlock<PhoneClient> ThreadQueue = new BufferBlock<PhoneClient>();


        private static Thread NetworkThread = new Thread(async () =>
        {
            while (true)
            {
                (await ThreadQueue.ReceiveAsync()).StartHandShake();
            }
        });

        //private static Dispatcher NetworkThreadDispatcher = null;

        static PhoneClient()
        {
            NetworkThread.Start();

            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_LOCK_SCREEN,
                PhoneMessageCenter.LockScreen,
                false
                );

            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_HELLO,
                HelloBack,
                false
                );
        }

        public static void CreateClient(TcpClient Client, String code)
        {
            ThreadQueue.Post(new PhoneClient(Client, code));
        }

        public static void HelloBack(string id, string msgType, object msg, PhoneClient client)
        {
            client.SendMsg(MSG_TYPE_NONCE);
        }


        /////////////////////////////////////////////////////////////////////////////////

        public class IllegalPhoneException : Exception { }
        public TcpClient ClientObject { get; protected set; }
        public NetworkStream ClientStream { get; protected set; }
        public string Id { get; protected set; } = null;
        public string Name { get; private set; } = null;

        private bool OldConnection = false;
        private String TempotaryCodeHolder = null;

        public bool IsAlive { get; protected set; } = false;

        private class SendQueueClass : QueuedAsyncTask<object, byte[]>
        {
            public readonly PhoneClient ClientObject;
            public SendQueueClass(PhoneClient ClientObject)
            {
                this.ClientObject = ClientObject;
            }

            protected override Task<object> InputSource()
            {
                throw new WontOperateThis();
            }

            protected override void OnDone(object input, byte[] output)
            {
                ClientObject.WriteHard(output);
            }

            protected override byte[] TaskBody(object Input)
            {
                byte[] RawBytes;
                if (Input is byte[] Bytes)
                {
                    RawBytes = Bytes;
                }
                else if (Input is string str)
                {
                    RawBytes = EncryptionManager.ConvertToBytes(str);
                }
                else if (Input is JObject json)
                {
                    RawBytes = EncryptionManager.ConvertToBytes(json);
                }
                else
                {
                    throw new Exception();
                }

                return ClientObject.encryptionManager.Encrypt(RawBytes);
            }

            protected override void OnException(QueuedAsyncTask<object, byte[]> sender, Exception e)
            {
                ClientObject.Dispose();
            }
        }


        protected readonly QueuedAsyncTask<object, byte[]> SendQueue;

        private void Init(TcpClient Client)
        {
            this.ClientObject = Client;
            this.ClientStream = Client.GetStream();
        }

        private PhoneClient(TcpClient Client, String code) : base(Client.GetStream(), code)
        {
            this.TempotaryCodeHolder = code;
            Init(Client);

            SendQueue = new SendQueueClass(this);
        }

        public override void Dispose()
        {
            lock (this)
            {
                base.Dispose();
                SendQueue.Dispose();
                IsAlive = false;

                if (ClientObject != null)
                {
                    SendMsgNoThrow(MSG_TYPE_DISCONNECT_BY_PEER);

                    ClientObject?.Dispose();
                    ClientObject?.Close();
                    ClientObject = null;

                    if (this.Id != null)
                    {
                        PhoneMessageCenter.Singleton.Unregister(
                            Id,
                            PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                            OnRemoved
                            );

                        PhoneMessageCenter.Singleton.Unregister(
                            Id,
                            PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                            OnNameChanged
                        );

                        PhoneClient CurrentClient = AlivePhones.Singleton[Id];
                        if (CurrentClient == null)
                        {
                            PhoneMessageCenter.Singleton.Raise(
                                Id,
                                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTION_FAILED,
                                null,
                                this
                            );
                        }
                        else if (CurrentClient == this)
                        {
                            PhoneMessageCenter.Singleton.Raise(
                                Id,
                                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                                null,
                                this
                            );
                        }
                    }
                }
            }
        }

        private void OnNameChanged(string id, string msgType, object msg, PhoneClient client)
        {
            if (!(msg is string newName)) return;

            this.Name = newName;
        }

        private void OnRemoved(string _, string __, object ___, PhoneClient ____)
        {
            AlivePhones.Singleton.Remove(Id);
            Dispose();
        }

        private void WriteLength(int v)
        {
            int h = IPAddress.HostToNetworkOrder(v);
            byte[] b = BitConverter.GetBytes(h);
            ClientStream?.Write(b);
        }

        private void WriteHard(byte[] raw)
        {
            WriteLength(raw.Length);
            ClientStream?.Write(raw);
        }

        public void WriteHard(JObject json)
        {
            byte[] b = Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            WriteHard(b);
        }

        public void WriteQueued(string v)
        {
            SendQueue.InputFrom(v);
        }

        public void WriteQueued(JObject json)
        {
            SendQueue.InputFrom(json);
        }

        public void SendMsg(JObject src, string type)
        {
            src[MSG_TYPE_KEY] = type;
            WriteQueued(src);
        }

        public void SendMsg(string type)
        {
            SendMsg(JObject.Parse("{}"), type);
        }

        public void SendMsgNoThrow(string type)
        {
            try
            {
                SendMsg(type);
            }
            catch (IOException e)
            {

            }
            catch (SocketException e)
            {

            }
        }

        private void SetCode(string newCode)
        {
            encryptionManager = new EncryptionManager(newCode);

        }

        private async void ReplyBack()
        {
            JObject re = new JObject
            {
                ["peerid"] = MainConfig.Config.ThisId,
            };
            WriteQueued(re);
            SendQueue.PerformOnDoneOnce(await SendQueue.FetchOutputTaskOnce());
        }

        private async void StartHandShake()
        {
            try
            {
                ClientObject.ReceiveTimeout = 5000;
                await HandShakeStep1_GetPhoneId();
                await HandShakeStep2_Authenticate();
                ClientObject.ReceiveTimeout = 0;
                await HandShakeStep3_WaitingForAccepted();
                EnterLoop();
            }
            catch (Exception e)
            {
                Dispose();
            }
        }

        private async Task HandShakeStep1_GetPhoneId()
        {
            /*
             {  // json uncrypted
                phoneid:"some_id",
             }
            */
            JObject accept = await ReadUncryptedJSON();
            Id = (string)accept["phoneid"];

            // Intialize cipher key
            OldConnection = accept.OptBool("oldconnection", false);
            if (OldConnection)
            {
                // Replace constructor-inited EncryptionManager
                SetCode(SavedPhones.Singleton[Id].Code);
            }
            else
            {
                string partialKey = accept.OptString("key", null);
                if (!String.IsNullOrEmpty(partialKey))
                {
                    // If the phone offers a partial key, use it
                    TempotaryCodeHolder += partialKey;
                    SetCode(TempotaryCodeHolder);
                }
            }
        }

        private async Task HandShakeStep2_Authenticate()
        {
            /*
             {  // json ENCRYPTED
                phoneid:"some_id",
                wait_accept: true
             }
            */
            JObject reply = await ReadJSON();
            string id = (string)reply["phoneid"];

            if (!Id.Equals(id))
            {
                throw new IllegalPhoneException();
            }

            ReplyBack();

            // Get phone's literal name
            if (!SavedPhones.Singleton.ContainsKey(Id))
            {
                Name = reply.OptString("phonename", "(Unknown)");
            }
            else
            {
                Name = SavedPhones.Singleton[Id].Name;
            }
        }

        private async Task HandShakeStep3_WaitingForAccepted()
        {
            /*
             {  // json ENCRYPTED
                phoneid:"some_id",
             }
             */

            JObject reply = await ReadJSON();
            if (!MSG_TYPE_CONNECTION_ACCEPTED.Equals((string)reply[MSG_TYPE_KEY]))
            {
                throw new IllegalPhoneException();
            }
        }

        private void EnterLoop()
        {
            PhoneMessageCenter.Singleton.Register(
                Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                OnNameChanged,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                OnRemoved,
                false
                );

            {
                // Save phone states
                if (OldConnection)
                {
                    // Only Ip needs to be updated
                    SavedPhones.Singleton[Id].LastIp = ClientObject.Client.RemoteEndPoint.ConvertToString();
                }
                else
                {
                    SavedPhones.Singleton.AddOrUpdate(
                        Id,
                        TempotaryCodeHolder,
                        Name,
                        ClientObject.Client.RemoteEndPoint.ConvertToString()
                    );
                }
            }

            TempotaryCodeHolder = null;

            SavedPhones.Singleton[Id].Apply(it =>
            {
                it.InitNotificationLogger();
                it.InitSmallFileCache();
            });

            AlivePhones.Singleton.AddOrUpdate(Id, this)?.Dispose();
            IsAlive = true;

            PhoneMessageCenter.Singleton.Raise(
                Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                null,
                this
            );

            SendQueue.StartLoop();
            ReceiveQueue.StartLoop();
        }

        private MessageWithBinary messageWithBinary = null;
        protected override void PackageProcessing(byte[] raw, byte[] decrypted)
        {
            if (messageWithBinary != null)
            {
                messageWithBinary.Binary = decrypted;

                PhoneMessageCenter.Singleton.Raise(
                    Id,
                    (string)messageWithBinary.Message[MSG_TYPE_KEY],
                    messageWithBinary,
                    this
                );

                messageWithBinary = null;
            }
            else
            {
                JObject msg = EncryptionManager.ExtractJSON(decrypted);
                if(msg == null)
                {

                }
                else if (msg.OptBool("withbinary", false))
                {
                    messageWithBinary = new MessageWithBinary().Reset();
                    messageWithBinary.Message = msg;
                }
                else
                {
                    PhoneMessageCenter.Singleton.Raise(
                        Id,
                        (string)msg[MSG_TYPE_KEY],
                        msg,
                        this
                    );
                }
            }
        }

        public async Task<bool> ProbeAlive(int DelayMills)
        {
            try
            {
                await PhoneMessageCenter.Singleton.OneShot(
                   this,
                   new JObject(),
                   MSG_TYPE_HELLO,
                   MSG_TYPE_NONCE,
                   DelayMills
                   );

                return true;
            } catch (Exception e)
            {
                Dispose();
                return false;
            }
        }

        public async Task<bool> ProbeAlive(int DelayMills, Action<bool> action)
        {
            bool result = await ProbeAlive(DelayMills);
            action.Invoke(result);
            return result;
        }
    }
}
