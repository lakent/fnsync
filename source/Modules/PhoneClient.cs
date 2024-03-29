﻿using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using Windows.Storage.Pickers;

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

        static PhoneClient()
        {
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

        public static void CreateClient(TcpClient Client, string code)
        {
            new PhoneClient(Client, code).StartHandShake();
        }

        public static void HelloBack(string id, string msgType, object? msg, PhoneClient? client)
        {
            client?.SendMsg(MSG_TYPE_NONCE);
        }

        /////////////////////////////////////////////////////////////////////////////////

        public class IllegalPhoneException : Exception { }
        public TcpClient? ClientObject { get; protected set; } = null;
        public NetworkStream ClientStream { get; protected set; } = null!;
        public string Id { get; protected set; } = null!;
        public string Name { get; private set; } = null!;

        private bool OldConnection = false;
        private string? TempotaryCodeHolder = null;

        public bool IsAlive { get; protected set; } = false;

        public long SeenAt { get; private set; } = 0L;

        private class SendQueueClass : QueuedAsyncTask<object, byte[]>
        {
            public readonly PhoneClient ClientObject;
            public SendQueueClass(PhoneClient ClientObject) : base()
            {
                this.ClientObject = ClientObject;
            }

            protected override Task<object> InputSource()
            {
                throw new WontOperateThis();
            }

            protected override void OnTaskDone(object input, byte[]? output)
            {
                if (output != null)
                {
                    ClientObject.WriteHard(output);
                }
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
                    throw new NotSupportedException();
                }

                return ClientObject.encryptionManager.Encrypt(RawBytes);
            }

            protected override void OnException(QueuedAsyncTask<object, byte[]> sender, Exception e)
            {
                sender.Dispose();
                ClientObject.Dispose();
            }
        }


        protected readonly QueuedAsyncTask<object, byte[]> SendQueue;

        private void Init(TcpClient Client)
        {
            this.ClientObject = Client;
            this.ClientStream = Client.GetStream();
        }

        private PhoneClient(TcpClient Client, string code) : base(Client.GetStream(), code)
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

                        PhoneClient? CurrentClient = AlivePhones.Singleton[Id];
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

        private void OnNameChanged(string id, string msgType, object? msg, PhoneClient? client)
        {
            if (msg is not string newName) return;

            this.Name = newName;
        }

        private void OnRemoved(string _, string __, object? ___, PhoneClient? ____)
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
            try
            {
                SendQueue.InputManually(v);
            }
            catch (SendQueueClass.Exited)
            {
                throw new PhoneMessageCenter.DisconnectedException();
            }
        }

        public void WriteQueued(JObject json)
        {
            try
            {
                SendQueue.InputManually(json);
            }
            catch (SendQueueClass.Exited)
            {
                throw new PhoneMessageCenter.DisconnectedException();
            }
        }

        public void WriteQueued(byte[] binary)
        {
            try
            {
                SendQueue.InputManually(binary);
            }
            catch (SendQueueClass.Exited)
            {
                throw new PhoneMessageCenter.DisconnectedException();
            }
        }

        public void SendMsg(JObject src, string type)
        {
            SendMsg(src, type, (byte[]?)null);
        }

        public void SendMsg(JObject src, string type, byte[]? binary)
        {
            src[MSG_TYPE_KEY] = type;
            if (binary != null)
            {
                src["withbinary"] = true;
            }

            WriteQueued(src);

            if (binary != null)
            {
                WriteQueued(binary);
            }
        }

        public void SendMsg(string type)
        {
            SendMsg(new JObject(), type);
        }

        public void SendMsgNoThrow(string type)
        {
            try
            {
                SendMsg(type);
            }
            catch (IOException)
            {

            }
            catch (SocketException)
            {

            }
            catch (SendQueueClass.Exited)
            {

            }
            catch (PhoneMessageCenter.DisconnectedException)
            {

            }
        }

        public void SendMsgNoThrow(JObject msg, string type)
        {
            try
            {
                SendMsg(msg, type);
            }
            catch (IOException)
            {

            }
            catch (SocketException)
            {

            }
            catch (SendQueueClass.Exited)
            {

            }
            catch (PhoneMessageCenter.DisconnectedException)
            {

            }
        }

        private void SetCode(string newCode)
        {
            encryptionManager = new EncryptionManager(newCode);
        }

        private async void ReplyBack()
        {
            JObject re = new()
            {
                ["peerid"] = MainConfig.Config.ThisId,
            };
            WriteQueued(re);
            _ = SendQueue.PerformOnDoneOnce(await SendQueue.FetchOutputTaskOnce());
        }

        private async void StartHandShake()
        {
            if (ClientObject == null)
            {
                throw new Exception("Cannot start hand shake on a already disposed object");
            }

            try
            {
                ClientObject.ReceiveTimeout = 10000;
                await HandShakeStep1_GetPhoneId();
                await HandShakeStep2_Authenticate();
                ClientObject.ReceiveTimeout = 0;
                await HandShakeStep3_WaitingForAcceptance();
                EnterLoop();
            }
            catch (Exception)
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
            Id = (string?)accept["phoneid"] ?? throw new ArgumentException("", "phoneid");

            // Intialize cipher key
            OldConnection = accept.OptBool("oldconnection", false);
            if (OldConnection)
            {
                SavedPhones.Phone? Saved = SavedPhones.Singleton[Id];
                if (Saved == null)
                {
                    throw new IllegalPhoneException();
                }

                // Replace constructor-inited EncryptionManager
                SetCode(Saved.Code);
            }
            else
            {
                string? partialKey = accept.OptString("key", null);
                if (string.IsNullOrEmpty(partialKey))
                {
                    throw new ArgumentException("Phone didn't provide the other partial key");
                }

                // If the phone offers a partial key, use it
                TempotaryCodeHolder += partialKey;
                SetCode(TempotaryCodeHolder);
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
            JObject? reply = await ReadJSON();
            if (reply == null)
            {
                _ = MessageBox.Show(
                    (string)Application.Current.FindResource("DecryptionErrorNote"),
                    (string)Application.Current.FindResource("DecryptionError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                    );

                throw new IllegalPhoneException();
            }

            string? id = (string?)reply["phoneid"];

            if (!Id.Equals(id))
            {
                throw new IllegalPhoneException();
            }

            ReplyBack();

            // Get phone's literal name
            Name = SavedPhones.Singleton[Id]?.Name ?? reply.OptString("phonename") ?? "(Unknown)";
        }

        private async Task HandShakeStep3_WaitingForAcceptance()
        {
            /*
             {  // json ENCRYPTED
                phoneid:"some_id",
             }
             */

            JObject? reply = await ReadJSON();
            if (!MSG_TYPE_CONNECTION_ACCEPTED.Equals((string?)reply?[MSG_TYPE_KEY]))
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
                string LastIp = ClientObject?.Client.RemoteEndPoint?.ConvertToString() ?? "";
                // Save phone states
                if (OldConnection)
                {
                    // Only Ip needs to be updated
                    SavedPhones.Singleton[Id]?.Apply((self) =>
                    {
                        self.LastIp = LastIp;
                    });
                }
                else
                {
                    SavedPhones.Singleton.AddOrUpdate(
                        Id,
                        TempotaryCodeHolder!,
                        Name,
                        LastIp
                    );
                }
            }

            TempotaryCodeHolder = null;

            AlivePhones.Singleton.AddOrUpdate(Id, this)?.Dispose();
            IsAlive = true;

            SeenAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            PhoneMessageCenter.Singleton.Raise(
                Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                null,
                this
            );

            SendQueue.StartLoop();
            ReceiveQueue.StartLoop();
        }

        private MessageWithBinary? messageWithBinary = null;
        protected override void ConsumePackage(byte[] raw, byte[]? decrypted)
        {
            SeenAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (messageWithBinary != null)
            {
                string MsgType = (string?)messageWithBinary.Message?[MSG_TYPE_KEY] ??
                    throw new ArgumentException("", MSG_TYPE_KEY)
                    ;

                messageWithBinary.Binary = decrypted;

                PhoneMessageCenter.Singleton.Raise(
                    Id,
                    MsgType,
                    messageWithBinary,
                    this
                );

                messageWithBinary = null;
            }
            else
            {
                JObject? msg = decrypted != null ? EncryptionManager.ExtractJSON(decrypted) : null;
                if (msg == null)
                {
                    return;
                }

                string MsgType = (string?)msg?[MSG_TYPE_KEY] ??
                    throw new ArgumentException("", MSG_TYPE_KEY)
                    ;

                if (msg!.OptBool("withbinary", false))
                {
                    messageWithBinary = new MessageWithBinary().Reset();
                    messageWithBinary.Message = msg;
                }
                else
                {
                    PhoneMessageCenter.Singleton.Raise(
                        Id,
                        MsgType,
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
                   null,
                   MSG_TYPE_NONCE,
                   DelayMills
                   );

                return true;
            }
            catch (Exception)
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
