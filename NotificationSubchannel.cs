using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Documents;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FnSync
{
    class NotificationSubchannel
    {
        public static readonly NotificationSubchannel Singleton = new NotificationSubchannel();

        private readonly BufferBlock<ToastNotification> Queue = new BufferBlock<ToastNotification>();

        private NotificationSubchannel()
        {
            Thread thread = new Thread(() => ThreadJob());
            thread.Start();
            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION,
                OnNotificationReceived,
                false
                );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION,
                OnNotificationReceived_Preprocess,
                false,
                -1
                );

            PhoneMessageCenter.Singleton.Register(
                null,
                Casting.MSG_TYPE_TEXT_CAST,
                OnNotificationReceived_Preprocess,
                false,
                -1
                );

        }

        private async void ThreadJob()
        {
            ToastNotifier Notifier = ToastNotificationManager.CreateToastNotifier("holmium.FnSync.A7F49234CADC422229142EDC7D8932E");

            ToastNotification Last = null;
            ToastNotification LastDup = null;

            while (true)
            {
                ToastNotification one = await Queue.ReceiveAsync();

#if !DEBUG
                try
                {
#endif
                    Notifier.Show(one);
#if !DEBUG
                }
                catch (Exception e) { }
#endif

                if (Last != null)
                {
                    Notifier.Hide(Last);

                    if (LastDup != null)
                    {
                        LastDup.SuppressPopup = true;
                        Notifier.Show(LastDup);
                    }
                }

                await Task.Delay(1000);
                ToastNotification dup = await Queue.ReceiveAsync();
                LastDup = dup;
                Last = one;
            }
        }

        private static void Preprocess(JObject notification)
        {
            if (!notification.ContainsKey("uuid"))
            {
                notification["uuid"] = Guid.NewGuid().ToString();
            }

            if (!notification.ContainsKey("time"))
            {
                notification["time"] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
        }

        private static void OnNotificationReceived_Preprocess(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg)) return;

            Preprocess(msg);
        }

        private static void OnNotificationReceived(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg)) return;

            ToastPhoneNotification(client.Id, client.Name, msg);

            RecordedPkgMapping.Singleton.Record(msg);
        }

        public static readonly string COPY_TEXT = (string)Application.Current.FindResource("CopyText");
        private static readonly string MARK_AS_IMPORTANT = (string)Application.Current.FindResource("MarkAsImportant");
        private static readonly string DEVICE_MANAGER = (string)Application.Current.FindResource("DeviceManager");

        public static string[] GetCopyableSeries(string Text, int MaxNumber)
        {
            string pattern = @"((https?:\/\/)?([0-9a-zA-Z][.0-9a-zA-Z]+\.([a-zA-Z]+|xn\-\-[0-9a-zA-Z]+)|([0-9]{1,3}\.){3}[0-9]{1,3})(:[0-9]{1,5})?(\/[0-9A-Za-z-\/\\.@:%_\+~#=?]*)?)|((?<=[^.\d])\d{4,}(?!\.)(?!\d))|((?<=[^.\d])\+?\d{1,3}[-\d]{4,})";

            var matches = Regex.Matches(Text, pattern);

            if (matches.Count == 0)
            {
                return null;
            }

            HashSet<string> ret = new HashSet<string>();
            int Saved = 0;

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 0)
                {
                    var text = match.Groups[0].Value;
                    if (ret.Add(text)) ++Saved;
                }

                if (Saved == MaxNumber)
                {
                    break;
                }
            }

            return ret.ToArray();
        }

        private static void ToastPhoneNotification(string clientId, string clientName, JObject msg)
        {
            string title = (String)(msg["title"]);
            string text = msg.OptString("text", "");
            string pkgname = (String)(msg["pkgname"]);
            string appName = msg.OptString("appname", null);
            long time = (long)msg["time"];
            string icon = SavedPhones.Singleton[clientId].SmallFiles.GetOrPutFromBase64(pkgname, msg.OptString("icon", null), "png", false);

            ToastActionsCustom Actions = new ToastActionsCustom()
            {
                ContextMenuItems =
                    {
                        new ToastContextMenuItem(COPY_TEXT,
                            new QueryString()
                            {
                                { "Copy", text },
                            }.ToString()
                        ),
                    },
                Buttons = { }
            };

            string[] copyables = GetCopyableSeries(text, 5 - Actions.ContextMenuItems.Count);
            if (copyables != null)
            {
                foreach (string copyable in copyables)
                {
                    Actions.Buttons.Add(
                        new ToastButton(copyable, new QueryString() { { "Copy", copyable } }.ToString())
                        {
                            ActivationType = ToastActivationType.Foreground
                        }
                    );
                }
            }

            ToastContent toastContent = new ToastContent()
            {
                //Launch = "action=viewConversation&conversationId=5",

                Header = new ToastHeader(clientId, clientName, ""),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title,
                                HintMaxLines = 1
                            },
                            new AdaptiveText()
                            {
                                Text = text,
                            },
                        },
                        AppLogoOverride = icon == null ? null : new ToastGenericAppLogo()
                        {
                            Source = icon,
                            HintCrop = ToastGenericAppLogoCrop.Default,
                            AlternateText = pkgname
                        },
                        Attribution = appName != null ? new ToastGenericAttributionText()
                        {
                            Text = appName
                        } : null,
                    }
                },

                Actions = Actions,
                DisplayTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime
            };

            // Create the XML document (BE SURE TO REFERENCE WINDOWS.DATA.XML.DOM)
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the Toast notification
            var Toast = new ToastNotification(doc);
            var ToastDup = new ToastNotification(doc);

            // And then show it
            Singleton.Push(Toast, ToastDup);
        }

        public void Push(ToastNotification notification, ToastNotification dup)
        {
            Queue.Post(notification);
            Queue.Post(dup);
        }
    }

    //////////////////////////////////////////////////////////////////////////////////

    // The GUID CLSID must be unique to your app. Create a new GUID if copying this code.
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("8C025F15-F051-427B-AF16-9F43DB9ED3EA"), ComVisible(true)]
    public class FnSyncNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
            if (invokedArgs == "")
            {
                return;
            }

            if (invokedArgs.Equals("ConnectOther"))
            {
                WindowConnect.NewOne();
            }
            else if (invokedArgs.Equals("DeviceManager"))
            {
                WindowDeviceMananger.NewOne(null);
            }

            QueryString queries = QueryString.Parse(invokedArgs);
            if (queries.Contains("Copy"))
            {
                App.FakeDispatcher.Invoke(() =>
                {
                    ClipboardManager.Singleton.SetClipboardText(queries["Copy"], false);
                    return null;
                });
            }

            if (queries.Contains("FileReceive_SaveAs"))
            {
                FileTransmission.ParseQueryString(queries);
            }
        }
    }
}
