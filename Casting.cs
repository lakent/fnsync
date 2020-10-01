using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FnSync
{
    class Casting
    {
        public const string MSG_TYPE_TEXT_CAST = "text_cast";
        public static int _Force = -1;

        static Casting()
        {
            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_TEXT_CAST,
                OnCastReceived,
                false
            );
        }

        private static void OnCastReceived(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (msgObject is JObject msg)
            {
                if (msgType == MSG_TYPE_TEXT_CAST)
                {
                    string text = (string)msg["text"];
                    if (text.Length < 500)
                    {
                        ToastTextCastShort(id, client.Name, text);
                    }
                    else
                    {
                        ToastTextCastLong(id, client.Name, text);
                        Application.Current.Dispatcher.InvokeAsyncCatchable(delegate
                        {
                            Clipboard.SetText(text);
                        });
                    }
                }
            }
        }

        public static readonly string TEXT_RECEIVED = (string)Application.Current.FindResource("TextCastReceived");
        private static void ToastTextCastShort(string clientId, string clientName, string text)
        {
            ToastActionsCustom Actions = new ToastActionsCustom()
            {
                ContextMenuItems = { },
                Buttons = {
                    new ToastButton(
                        NotificationSubchannel.COPY_TEXT,
                        new QueryString(){{ "Copy", text}}.ToString())
                        {
                            ActivationType = ToastActivationType.Foreground
                        }
                    }
            };

            string[] copyables = NotificationSubchannel.GetCopyableSeries(text, 5 - Actions.Buttons.Count);
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
                                Text = TEXT_RECEIVED,
                                HintMaxLines = 1
                            },
                            new AdaptiveText()
                            {
                                Text = text,
                            },
                        },
                    }
                },

                Actions = Actions
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

        public static readonly string TEXT_TOO_LONG_ALREADY_COPIED = (string)Application.Current.FindResource("TextTooLongAlreadyCopied");
        private static void ToastTextCastLong(string clientId, string clientName, string FullText)
        {
            FullText = FullText.Substring(0, 50) + " ...";

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
                                Text = TEXT_RECEIVED,
                                HintMaxLines = 1
                            },
                            new AdaptiveText()
                            {
                                Text = TEXT_TOO_LONG_ALREADY_COPIED,
                            },
                            new AdaptiveText()
                            {
                                Text = FullText,
                            },
                        },
                    }
                },

                Actions = new ToastActionsCustom()
                {
                }
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
    }
}
