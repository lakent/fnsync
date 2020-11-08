using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.Security.Cryptography.Core;
using Windows.UI.Notifications;

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

        static PhoneClient()
        {
            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_LOCK_SCREEN,
                PhoneMessageCenter.LockScreen,
                false
                );
        }

        public class IllegalPhoneException : Exception { }
        public class IllegalMessageIdException : Exception { }
        public Socket Client { get; protected set; }
        public string Id { get; protected set; } = null;
        public string Name { get; private set; } = null;

        private bool OldConnection = false;
        private String TempotaryCodeHolder = null;

        private bool PendingReplaced = true;

#if DEBUG
        private string ErrorReason = null;
#endif

        public bool IsAlive => Client != null;

        private void Init(Socket Client)
        {
            this.Client = Client;

            if (Client != null)
            {
                SelectTask.Singleton.AddOrUpdate(
                    this,
                    true,
                    false,
                    PcListener.FIRST_ACCEPT_TIMEOUT_MILLS,
                    HandShakeStep1_GetPhoneId,
                    true
                );
            }
        }

        public PhoneClient(Socket Client, String code) : base(512, code)
        {
            this.TempotaryCodeHolder = code;
            Init(Client);
        }


        public void Dispose()
        {
            if (Client != null)
            {
                try
                {
                    SendMsg(MSG_TYPE_DISCONNECT_BY_PEER);
                }
                catch (Exception) { }

                Client?.Shutdown(SocketShutdown.Both);
                Client?.Close();
                Client = null;

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

                if (!PendingReplaced)
                {
                    AlivePhones.Singleton.DecrementAlive();

                    PhoneMessageCenter.Singleton.Raise(
                        Id,
                        PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                        null,
                        this
                    );
                }
                else
                {
                    PhoneMessageCenter.Singleton.Raise(
                        Id,
                        PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTION_FAILED,
                        null,
                        this
                    );
                }
            }

            //ToastLostConnection();
        }

        public void RetreatAndDispose()
        {
            SelectTask.Singleton.RetreatAndDispose(this);
        }

        private void OnNameChanged(string id, string msgType, object msg, PhoneClient client)
        {
            if (!(msg is string newName)) return;

            this.Name = newName;
        }

        private void OnRemoved(string _, string __, object ___, PhoneClient ____)
        {
#if DEBUG
            ErrorReason = "Removed by user";
#endif
            RetreatAndDispose();
            AlivePhones.Singleton.Remove(Id);
        }

        public void ToBeReplaced()
        {
            PendingReplaced = true;
            SelectTask.Singleton.RetreatAndDispose(this);
        }

        protected override void Load()
        {
            if (BufferUsed < 4)
            {
                int LenRemain = 4 - BufferUsed;
                int PendingRecv = Math.Min(LenRemain, Client.Available);
                BufferUsed += Client.Receive(buffer, BufferUsed, PendingRecv, SocketFlags.None);
            }

            if (BufferUsed >= 4)
            {
                int packageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));
                int PendingRecv = Math.Min(packageLength, Client.Available);

                if (PendingRecv == 0)
                {
                    return;
                }

                if (BufferUsed + PendingRecv > buffer.Length)
                {
                    Array.Resize(ref buffer, BufferUsed + PendingRecv);
                }

                BufferUsed += Client.Receive(buffer, BufferUsed, PendingRecv, SocketFlags.None);
            }
        }

        private void WriteLength(int v)
        {
            int h = IPAddress.HostToNetworkOrder(v);
            byte[] b = BitConverter.GetBytes(h);
            Client?.Send(b);
        }

        public void WriteString(string v)
        {
            byte[] e = encryptionManager.EncryptString(v);
            WriteLength(e.Length);
            Client?.Send(e);
        }

        public void WriteJSONUnencrypted(JObject json)
        {
            byte[] e = Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            WriteLength(e.Length);
            Client?.Send(e);
        }

        public void WriteJSON(JObject json)
        {
            byte[] e = encryptionManager.EncryptString(json.ToString(Newtonsoft.Json.Formatting.None));
            WriteLength(e.Length);
            Client?.Send(e);
        }

        public void SendMsg(JObject src, string type)
        {
            src[MSG_TYPE_KEY] = type;
            WriteJSON(src);
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
                RetreatAndDispose();
            }
            catch (SocketException e)
            {
                RetreatAndDispose();
            }
        }

        private void SetCode(string newCode)
        {
            encryptionManager = new EncryptionManager(newCode);
        }

        private void ReplyBack()
        {
            JObject re = new JObject
            {
                ["peerid"] = MainConfig.Config.ThisId,
            };
            WriteJSON(re);
        }

        private static SelectTask.Result HandShakeStep1_GetPhoneId(Object target, bool read, bool write, bool error)
        {
            PhoneClient client = target as PhoneClient;

            /*
             {  // json uncrypted
                phoneid:"some_id",
             }
            */
            try
            {
                JObject accept = client.ReadUncryptedJSON();
                string id = (string)accept["phoneid"];
                client.Id = id;

                {
                    // Intialize cipher key
                    client.OldConnection = accept.OptBool("oldconnection", false);
                    if (client.OldConnection)
                    {
                        // Replace constructor-inited EncryptionManager
                        client.SetCode(SavedPhones.Singleton[id].Code);
                    }
                    else
                    {
                        string partialKey = accept.OptString("key", null);
                        if (!String.IsNullOrEmpty(partialKey))
                        {
                            // If the phone offers a partial key, use it
                            client.TempotaryCodeHolder += partialKey;
                            client.SetCode(client.TempotaryCodeHolder);
                        }
                    }
                }

                SelectTask.Singleton.AddOrUpdate(client, true, false, PcListener.FIRST_ACCEPT_TIMEOUT_MILLS, HandShakeStep2_Authenticate, true);
            }
            catch (UnfinishedStreamException)
            {

            }
            catch (Exception e)
            {
#if DEBUG
                client.ErrorReason = e.Message + "\n" + e.StackTrace;
#endif
                return SelectTask.Result.DISPOSE;
            }

            return SelectTask.Result.KEEP;
        }

        private static void EnterLoop(PhoneClient client)
        {
            /* Main Thread */

            PhoneMessageCenter.Singleton.Register(
                client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                client.OnNameChanged,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                client.OnRemoved,
                false
                );

            {
                // Save phone states
                if (client.OldConnection)
                {
                    // Only Ip needs to be updated
                    SavedPhones.Singleton[client.Id].LastIp = client.Client.RemoteEndPoint.ConvertToString();
                }
                else
                {
                    SavedPhones.Singleton.AddOrUpdate(
                        client.Id,
                        client.TempotaryCodeHolder,
                        client.Name,
                        client.Client.RemoteEndPoint.ConvertToString()
                    );
                }
            }

            client.TempotaryCodeHolder = null;

            SavedPhones.Singleton[client.Id].Apply(it =>
            {
                it.InitNotificationLogger();
                it.InitSmallFileCache();
            });

            AlivePhones.Singleton.AddOrUpdate(client.Id, client)?.ToBeReplaced();
            AlivePhones.Singleton.IncrementAlive();

            SelectTask.Singleton.AddOrUpdate(client, true, false, 0, MessageLoop, false); // Asynchronously

            // No longer being automatically replaced
            client.PendingReplaced = false;

            PhoneMessageCenter.Singleton.Raise(
                client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                null,
                client
            );
        }

        private static SelectTask.Result HandShakeStep2_Authenticate(Object target, bool read, bool write, bool error)
        {
            /* Main Thread */

            PhoneClient client = target as PhoneClient;

            /*
             {  // json ENCRYPTED
                phoneid:"some_id",
                wait_accept: true
             }
            */
            try
            {
                JObject reply = client.ReadJSON();
                string id = (string)reply["phoneid"];

                if (!client.Id.Equals(id))
                {
                    throw new IllegalPhoneException();
                }

                client.ReplyBack();

                {
                    // Get phone's literal name
                    if (!SavedPhones.Singleton.ContainsKey(client.Id))
                    {
                        client.Name = reply.OptString("phonename", "(Unknown)");
                    }
                    else
                    {
                        client.Name = SavedPhones.Singleton[client.Id].Name;
                    }
                }

                if (reply.OptBool("wait_accept", false))
                {
                    SelectTask.Singleton.AddOrUpdate(client, true, false, PcListener.FIRST_ACCEPT_TIMEOUT_MILLS, HandShakeStep3_WaitingForAccepted, true);
                }
                else
                {
                    EnterLoop(client);
                }
            }
            catch (UnfinishedStreamException)
            {
            }
            catch (Exception e)
            {
#if DEBUG
                client.ErrorReason = e.Message + "\n" + e.StackTrace;
#endif
                // Org.BouncyCastle.Crypto.InvalidCipherTextException
                return SelectTask.Result.DISPOSE;
            }

            return SelectTask.Result.KEEP;
        }

        private static SelectTask.Result HandShakeStep3_WaitingForAccepted(Object target, bool read, bool write, bool error)
        {
            /* Main Thread */
            PhoneClient client = target as PhoneClient;

            /*
             {  // json ENCRYPTED
                phoneid:"some_id",
             }
             */
            try
            {
                JObject reply = client.ReadJSON();
                if (MSG_TYPE_CONNECTION_ACCEPTED.Equals((string)reply[MSG_TYPE_KEY]))
                {
                    EnterLoop(client);
                }
                else
                {
                    throw new IllegalPhoneException();
                }
            }
            catch (UnfinishedStreamException)
            {

            }
            catch (Exception e)
            {
#if DEBUG
                client.ErrorReason = e.Message + "\n" + e.StackTrace;
#endif
                // Org.BouncyCastle.Crypto.InvalidCipherTextException
                return SelectTask.Result.DISPOSE;
            }

            return SelectTask.Result.KEEP;
        }

        private MessageWithBinary messageWithBinary = new MessageWithBinary().Reset();

        private static SelectTask.Result MessageLoop(Object target, bool read, bool write, bool error)
        {
            PhoneClient client = target as PhoneClient;

            try
            {
                if (client.messageWithBinary.Message != null)
                {
                    if ("none" == client.messageWithBinary.Message.OptString("encryption", null))
                    {
                        client.messageWithBinary.Binary = client.ReadUncryptedBytes();
                    }
                    else
                    {
                        client.messageWithBinary.Binary = client.ReadBytes();
                    }

                    PhoneMessageCenter.Singleton.Raise(
                        client.Id,
                        (string)client.messageWithBinary.Message[MSG_TYPE_KEY],
                        client.messageWithBinary.CloneTo(),
                        client
                    );

                    client.messageWithBinary.Reset();
                }
                else
                {
                    JObject msg = client.ReadJSON();
                    if (msg.OptBool("withbinary", false))
                    {
                        client.messageWithBinary.Message = msg;
                        return SelectTask.Result.KEEP;
                    }

                    PhoneMessageCenter.Singleton.Raise(
                        client.Id,
                        (string)msg[MSG_TYPE_KEY],
                        msg,
                        client
                    );
                }
            }
            catch (UnfinishedStreamException e)
            {

            }
            catch (IOException)
            {
                return SelectTask.Result.DISPOSE;
            }
            catch (InvalidCipherTextException)
            {
                return SelectTask.Result.DISPOSE;
            }
            catch (Exception e)
            {
                client.messageWithBinary.Reset();
                // Other type of exceptions, ignore
            }

            return SelectTask.Result.KEEP;
        }

        /*
        private static readonly string LOST_CONNECTION = (string)Application.Current.FindResource("LostConnection");
        private void ToastLostConnection()
        {
            ToastContent toastContent = new ToastContent()
            {
                //Launch = "action=viewConversation&conversationId=5",

                Header = new ToastHeader(this.Id, this.Name, ""),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = LOST_CONNECTION,
                                HintMaxLines = 1
                            },
                            new AdaptiveText()
                            {
                                Text = Id,
                            },
                        }
                    }
                },

                DisplayTimestamp = DateTime.Now
            };

            // Create the XML document (BE SURE TO REFERENCE WINDOWS.DATA.XML.DOM)
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the Toast notification
            var Toast = new ToastNotification(doc);
            var ToastDup = new ToastNotification(doc);

            // And then show it
            NotificationSubchannel.Singleton.Push(Toast, ToastDup);
        }
        */
    }
}
