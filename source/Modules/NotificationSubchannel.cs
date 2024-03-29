﻿using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;

namespace FnSync
{
    class NotificationSubchannel
    {
        public static readonly NotificationSubchannel Singleton = new();

        public static int _Force = -1;

        private readonly BufferBlock<ToastContentBuilder> Queue = new();

        private NotificationSubchannel()
        {
            Task.Run(ThreadJob);
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
            ToastContentBuilder? Last = null;
            int LastTag = 0;

            while (true)
            {
                ToastContentBuilder one = await Queue.ReceiveAsync();

                one.AddCustomTimeStamp(DateTime.Now);

                string Tag = LastTag.ToString();
                one.Show(toast =>
                {
                    toast.Tag = Tag;
                });

                if (Last != null)
                {
                    ToastNotificationManagerCompat.History.Remove((LastTag - 1).ToString());
                    Last.Show(toast =>
                    {
                        toast.SuppressPopup = true;
                    });
                }

                LastTag++;
                Last = one;
                await Task.Delay(1000);
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

        private static void OnNotificationReceived_Preprocess(string id, string msgType, object? msgObject, PhoneClient? client)
        {
            if (msgObject is not JObject msg) return;

            Preprocess(msg);
        }

        private static void OnNotificationReceived(string id, string msgType, object? msgObject, PhoneClient? client)
        {
            if (msgObject is not JObject msg || client == null)
            {
                return;
            }

            ToastPhoneNotification(client.Id, client.Name, msg);

            RecordedPkgMapping.Singleton.Record(msg);
        }

        public static readonly string COPY_TEXT = (string)Application.Current.FindResource("CopyText");
        private static readonly string MARK_AS_IMPORTANT = (string)Application.Current.FindResource("MarkAsImportant");
        private static readonly string DEVICE_MANAGER = (string)Application.Current.FindResource("DeviceManager");

        public static string[]? GetCopyableSeries(string Text, int MaxNumber)
        {
            string pattern = @"((https?:\/\/)?([0-9a-zA-Z][.0-9a-zA-Z]+\.([a-zA-Z]+|xn\-\-[0-9a-zA-Z]+)|([0-9]{1,3}\.){3}[0-9]{1,3})(:[0-9]{1,5})?(\/[0-9A-Za-z-\/\\.@:%_\+~#=?]*)?)|((?<=[^.\d])\d{4,}(?!\.)(?!\d))|((?<=[^.\d])\+?\d{1,3}[-\d]{4,})";

            MatchCollection matches = Regex.Matches(Text, pattern);

            if (matches.Count == 0)
            {
                return null;
            }

            HashSet<string> ret = new();
            int Saved = 0;

            foreach (Match match in matches.Cast<Match>())
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
            string title = msg.OptString("title", "")!;
            string text = msg.OptString("text", "")!;
            string? pkgname = msg.OptString("pkgname");
            string? appName = msg.OptString("appname");
            // long? time = (long?)msg["time"];

            string? icon = pkgname != null ?
                SavedPhones.Singleton[clientId]?.SmallFiles.GetOrPutFromBase64(
                    pkgname, msg.OptString("icon", null), "png", false
                    )
                : null;

            ToastContentBuilder Builder = new ToastContentBuilder()
                .AddHeader(clientId, clientName, "")
                .AddText(title)
                .AddText(text.Truncate(120))
                ;

            if (!string.IsNullOrWhiteSpace(text))
            {
                Builder.AddButton(new ToastButton()
                    .SetContent(COPY_TEXT)
                    .AddArgument("Copy", text)
                    .SetBackgroundActivation());
            }

            if (appName != null)
            {
                Builder.AddAttributionText(appName);
            }

            if (icon != null)
            {
                Builder.AddAppLogoOverride(new Uri(icon));
            }

            string[]? copyables = GetCopyableSeries(text, 4);
            if (copyables != null)
            {
                foreach (string copyable in copyables)
                {
                    Builder.AddButton(new ToastButton()
                        .SetContent(copyable)
                        .AddArgument("Copy", copyable)
                        .SetBackgroundActivation()
                    );
                }
            }

            Singleton.Push(Builder);
        }

        public void Push(ToastContentBuilder Builder)
        {
            Queue.Post(Builder);
        }

        public static void OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            string InvokedArgs = e.Argument;

            if (string.IsNullOrWhiteSpace(InvokedArgs))
            {
                return;
            }

            if (InvokedArgs.Equals("OpenMainWindow"))
            {
                WindowMain.NewOne();
            }

            ToastArguments args = ToastArguments.Parse(InvokedArgs);

            if (args.Contains("Copy"))
            {
                App.FakeDispatcher.Invoke(() =>
                {
                    ClipboardManager.Singleton.SetClipboardText(args["Copy"], false);
                    return null;
                });
            }

            if (args.Contains("FileReceive_SaveAs"))
            {
                FileReceive.ParseQueryString(args);
            }
        }

    }
}
