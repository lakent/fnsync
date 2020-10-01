using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Application = System.Windows.Application;

namespace FnSync
{
    public class FileTransmission : IDisposable
    {
        public static int _Force = -1;

        public const string MSG_TYPE_FILE_TRANSFER_REQUEST = "file_transfer_request";
        public const string MSG_TYPE_FILE_TRANSFER_META = "file_transfer_meta";
        public const string MSG_TYPE_FILE_TRANSFER_REWIND = "file_transfer_rewind";
        public const string MSG_TYPE_FILE_TRANSFER_DATA = "file_transfer_data";
        public const string MSG_TYPE_FILE_TRANSFER_END = "file_transfer_end";

        private static readonly PendingsClass Pendings = new PendingsClass();

        public class PendingsClass
        {
            public class Entry
            {
                public string mime;
                public string name;
                public long length;
                public string key;
            }

            private Dictionary<string, Entry[]> map = new Dictionary<string, Entry[]>();

            public PendingsClass()
            {

            }

            public string Add(JArray files)
            {
                List<Entry> entries = files.ToObject<List<Entry>>();

                if (entries == null || entries.Count == 0)
                    return null;

                string pendingKey = Guid.NewGuid().ToString();

                map.Add(pendingKey, entries.ToArray());

                return pendingKey;
            }

            public bool Contains(string PendingKey)
            {
                return map.ContainsKey(PendingKey);
            }

            public Entry[] Get(string PendingKey)
            {
                return map.ContainsKey(PendingKey) ? map[PendingKey] : null;
            }

            public void Remove(string PendingKey)
            {
                map.Remove(PendingKey);
            }

            public Entry[] GetAndRemove(string PendingKey)
            {
                Entry[] entries = Get(PendingKey);
                Remove(PendingKey);
                return entries;
            }

            public static long SizeOfAllFiles(IEnumerable<Entry> entries)
            {
                long ret = 0;

                foreach (Entry entry in entries)
                {
                    ret += entry.length;
                }

                return ret;
            }

            public static string First5Names(IEnumerable<Entry> entries)
            {
                StringBuilder sb = new StringBuilder();
                int count = 0;

                foreach (Entry entry in entries)
                {
                    if (count >= 5)
                        break;

                    sb.AppendLine(entry.name);
                }

                if (entries.Count() > 5)
                    sb.Append("……");

                return sb.ToString();

            }
        }

        private class RequestCacheClass
        {
            public class Entry
            {
                public int Count;
                public int Length;

                public override int GetHashCode()
                {
                    return (Count << 24) ^ Length;
                }

                public override bool Equals(object obj)
                {
                    return (obj is Entry e) && Count == e.Count && Length == e.Length;
                }
            }

            private string key = null;
            public string Key
            {
                get
                {
                    return key;
                }
                set
                {
                    if (value != key)
                    {
                        key = value;
                        Cache.Clear();
                    }
                }
            }

            private readonly Dictionary<Entry, string> Cache = new Dictionary<Entry, string>(2);

            public RequestCacheClass()
            {

            }

            public string Get(int Count, int Length)
            {
                Entry entry = new Entry { Count = Count, Length = Length };
                if (Cache.ContainsKey(entry))
                {
                    return Cache[entry];
                }
                else
                {
                    string msg = new JObject
                    {
                        ["key"] = Key,
                        ["count"] = Count,
                        ["length"] = Length,
                        [PhoneClient.MSG_TYPE_KEY] = MSG_TYPE_FILE_TRANSFER_META
                    }.ToString(Newtonsoft.Json.Formatting.None);

                    Cache[entry] = msg;

                    return msg;
                }
            }
        }

        static FileTransmission()
        {
            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_FILE_TRANSFER_REQUEST,
                OnRequestReceived,
                false
            );
        }

        public static readonly string ONE_FILE_RECEIVED = (string)Application.Current.FindResource("YouReceivedAFile");
        public static readonly string FILES_RECEIVED = (string)Application.Current.FindResource("YouReceivedFiles");
        public static readonly string SAVE_AS = (string)Application.Current.FindResource("SaveAs");
        public static readonly string CANCEL = (string)Application.Current.FindResource("Cancel");
        private static void OnRequestReceived(string id, string msgType, object msgObj, PhoneClient client)
        {
            if (!(msgObj is JObject msg)) return;

            string PendingKey = Pendings.Add((JArray)msg["files"]);

            if (PendingKey == null)
            {
                return;
            }

            PendingsClass.Entry[] entries = Pendings.Get(PendingKey);
            long total = PendingsClass.SizeOfAllFiles(entries);

            string header = string.Format(
                entries.Length > 1 ? FILES_RECEIVED : ONE_FILE_RECEIVED,
                entries.Length,
                Utils.ToHumanReadableSize(total)
                );

            string body = PendingsClass.First5Names(entries);

            ToastContent toastContent = new ToastContent()
            {
                //Launch = "action=viewConversation&conversationId=5",

                Header = new ToastHeader(id, client.Name, ""),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = header,
                                HintMaxLines = 1
                            },
                            new AdaptiveText()
                            {
                                Text = body,
                            },
                        },
                    }
                },

                Actions = new ToastActionsCustom()
                {
                    ContextMenuItems = { },
                    Buttons = {
                        new ToastButton(
                            SAVE_AS,
                            new QueryString(){
                                { "FileReceive_SaveAs"},
                                { "pendingkey", PendingKey},
                                { "totalsize", total.ToString()},
                                { "clientid", id},
                            }.ToString())
                            {
                                ActivationType = ToastActivationType.Foreground
                            },
                        new ToastButton(CANCEL,
                            new QueryString(){
                                { "FileReceive_SaveAs"},
                                { "cancelpending", PendingKey},
                            }.ToString())
                    }
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

        public static void ParseQueryString(QueryString queries)
        {
            if (queries.Contains("cancelpending"))
            {
                Pendings.Remove(queries["cancelpending"]);
                return;
            }

            string PendingKey = queries["pendingkey"];
            long total = Convert.ToInt64(queries["totalsize"]);
            PhoneClient client = AlivePhones.Singleton[queries["clientid"]];

            if (client == null || !Pendings.Contains(PendingKey))
            {
                return;
            }

            Application.Current.Dispatcher.InvokeAsyncCatchable(delegate
            {
                new WindowFileReceive(
                    new FileTransmission(client, Pendings.GetAndRemove(PendingKey), total)
                    ).Show();
            });
        }

        ////////////////////////////////////////////////////////////////////////////////


        private readonly PendingsClass.Entry[] Entries;
        private PhoneClient Client;

        public int FileCount => Entries.Length;
        public string FirstName => Entries[0].name;

        private FileStream file = null;
        private long CurrnetReceived = 0;
        private string CurrnetPath = null;
        private long TotalReceived = 0;
        private readonly long TotalSize;

        private long StartupTime = -1;
        private long LastReceivedTime = long.MaxValue;

        private const int UnitSizeInBytes = 1024;
        private int UnitNumber = 10;

        private readonly RequestCacheClass RequestCache;

        public class ProgressChangedEventArgs : EventArgs
        {
            public readonly long Received, Size;
            public readonly long TotalReceived, TotalSize;
            public readonly float Percent, TotalPercent;
            public readonly double BytesPerSec;

            public ProgressChangedEventArgs(
                long Received, long Size,
                long TotalReceived, long TotalSize,
                double BytesPerSec)
            {
                this.Received = Received;
                this.Size = Size;
                this.TotalReceived = TotalReceived;
                this.TotalSize = TotalSize;

                this.Percent = (float)((double)Received / (double)Size) * 100f;
                this.TotalPercent = (float)((double)TotalReceived / (double)TotalSize) * 100f;

                this.BytesPerSec = BytesPerSec;
            }
        }
        public class NextFileEventArgs : EventArgs
        {
            public readonly int Current, Count;
            public readonly string dest;

            public NextFileEventArgs(int Current, int Count, string dest)
            {
                this.Current = Current;
                this.Count = Count;
                this.dest = dest;
            }
        }
        public class FileAlreadyExistEventArgs : EventArgs
        {
            public enum Handle
            {
                SKIP, OVERWRITE, RENAME
            }

            public readonly string Dest;

            public string NewName;
            public Handle Action;

            public FileAlreadyExistEventArgs(string dest)
            {
                this.Dest = dest;
                this.Action = Handle.SKIP;
            }
        }

        public delegate void PercentageChangedEventHandler(object sender, ProgressChangedEventArgs e);
        public delegate void NextFileEventHandler(object sender, NextFileEventArgs e);
        public delegate void FileAlreadyExistEventHandler(object sender, FileAlreadyExistEventArgs e);

        public event PercentageChangedEventHandler ProgressChanged;
        public event NextFileEventHandler NextFile;
        public event FileAlreadyExistEventHandler FileAlreadyExist;
        public event EventHandler Finished;

        private readonly Thread WatcherThread;
        private readonly Thread WriteThread;
        private readonly Dispatcher WriteDispatcher;

        public FileTransmission(PhoneClient client, PendingsClass.Entry[] entries, long TotalSize = -1)
        {
            this.Client = client;
            this.Entries = entries;
            if (TotalSize < 0)
            {
                this.TotalSize = PendingsClass.SizeOfAllFiles(entries);
            }
            else
            {
                this.TotalSize = TotalSize;
            }

            this.RequestCache = new RequestCacheClass();

            PhoneMessageCenter.Singleton.Register(
                client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnReconnected,
                false
            );

            WatcherThread = new Thread(() => WatchJob());
            WatcherThread.Start();

            WriteThread = new Thread(() =>
            {
                Dispatcher.Run();
            });

            WriteThread.Start();

            Dispatcher dispatcher = null;
            while (dispatcher == null)
            {
                Thread.Sleep(5);
                dispatcher = Dispatcher.FromThread(WriteThread);
            }

            WriteDispatcher = dispatcher;
        }

        private void WatchJob()
        {
            while (LastReceivedTime != long.MinValue)
            {
                Thread.Sleep(5000);
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (now - LastReceivedTime > 5000)
                {
                    Client?.SendMsgNoThrow(PhoneClient.MSG_TYPE_HELLO);
                }
            }
        }

        private void RequestNext(int count)
        {
            Client.WriteString(RequestCache.Get(count, UnitNumber * UnitSizeInBytes));
        }

        private string LastFolder = null;

        private int FileIndex = -1;
        private PendingsClass.Entry CurrentEntry = null;
        public void StartNext(string folder, string name)
        {
            if (folder == null && LastFolder == null)
            {
                throw new ArgumentNullException();
            }

            WriteDispatcher.InvokeAsyncCatchable(delegate
            {
                if (folder != null)
                {
                    LastFolder = folder;
                }
                else
                {
                    folder = LastFolder;
                }

                while (true)
                {
                    if (++FileIndex >= Entries.Length)
                    {
                        LastReceivedTime = long.MinValue;

                        Application.Current.Dispatcher.InvokeAsyncCatchable(delegate
                        {
                            Finished?.Invoke(this, null);
                        });

                        return;
                    }

                    CurrentEntry = Entries[FileIndex];
                    RequestCache.Key = CurrentEntry.key;
                    CurrnetReceived = 0;

                    string filename = name ?? CurrentEntry.name;
                    filename = Utils.ReplaceInvalidFileName(filename);
                    string path = folder + (folder.EndsWith("\\") ? "" : "\\") + filename;

                    Application.Current.Dispatcher.InvokeAsyncCatchable(delegate
                    {
                        NextFile?.Invoke(
                            this,
                            new NextFileEventArgs(
                                FileIndex + 1,
                                Entries.Length,
                                CurrentEntry.name
                                )
                            );
                    });

                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        FileAlreadyExistEventArgs args = new FileAlreadyExistEventArgs(path);
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            FileAlreadyExist?.Invoke(this, args);
                        });

                        switch (args.Action)
                        {
                            case FileAlreadyExistEventArgs.Handle.SKIP:
                                continue;

                            case FileAlreadyExistEventArgs.Handle.OVERWRITE:
                                break;

                            case FileAlreadyExistEventArgs.Handle.RENAME:
                                if (args.NewName == null)
                                {
                                    continue;
                                }
                                path = folder + (folder.EndsWith("\\") ? "" : "\\") + args.NewName;
                                break;
                        }
                    }

                    CurrnetPath = path;

                    try
                    {
                        file = File.Open(CurrnetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    }
                    catch (Exception e)
                    {
                        continue;
                    }

                    file.SetLength(CurrentEntry.length);
                    RequestNext(10);
                    break;
                }
            });

            if (StartupTime < 0)
                StartupTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (speed == null)
                speed = new SpeedWatch();

            if (speed1 == null)
                speed1 = new SpeedWatch();
        }

        private SpeedWatch speed = null;
        private SpeedWatch speed1 = null;
        private double MaxSpeed = 0;
        private void OnBlobReceived(string id, string msgType, object msgObj, PhoneClient client)
        {
            if (!(msgObj is MessageWithBinary msg))
                return;

            if ((string)msg.Message["key"] != CurrentEntry.key)
                return;

            long start = msg.Message.OptLong("position", -1);
            int length = msg.Binary.Length;

            if (start < 0)
            {
                if (length > CurrentEntry.length - CurrnetReceived)
                    return;
            }
            else
            {
                if (start + length > CurrentEntry.length)
                    return;

                file.Seek(start, SeekOrigin.Begin);
            }

            CurrnetReceived += length;
            TotalReceived += length;

            speed.Add(length);
            speed1.Add(length);

            file.Write(msg.Binary, 0, length);

            double BytesPerSec = speed.BytesPerSec(250);

            if (BytesPerSec >= 0)
            {
                Application.Current.Dispatcher.InvokeAsyncCatchable(delegate
                {
                    ProgressChanged?.Invoke(
                        this,
                        new ProgressChangedEventArgs(
                            CurrnetReceived, CurrentEntry.length,
                            TotalReceived, TotalSize,
                            BytesPerSec)
                        );
                });

                speed.Reset();
            }

            double BytesPer1 = speed1.BytesPerSec(1500);
            if (BytesPer1 >= 0)
            {
                if (BytesPer1 > MaxSpeed)
                {
                    MaxSpeed = BytesPer1;
                    UnitNumber += 2;
                }
                else
                {
                    //UnitNumber = Math.Max(UnitNumber / 2, 1);
                }

                speed1.Reset();
            }

            if (CurrnetReceived < CurrentEntry.length)
            {
                RequestNext(1);
            }
            else
            {
                file.Close();
                Client.SendMsg(
                    new JObject()
                    {
                        ["key"] = CurrentEntry.key
                    },
                    MSG_TYPE_FILE_TRANSFER_END
                );

                StartNext(null, null);
            }

            LastReceivedTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private void OnReconnected(string id, string msgType, object msgObj, PhoneClient client)
        {
            this.Client = client;
            if (CurrentEntry != null)
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["key"] = CurrentEntry.key,
                        ["position"] = CurrnetReceived
                    },
                    MSG_TYPE_FILE_TRANSFER_REWIND
                );

                RequestNext(10);
            }
        }

        private void EndAll()
        {
            foreach (PendingsClass.Entry entry in Entries)
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["key"] = entry.key
                    },
                    MSG_TYPE_FILE_TRANSFER_END
                );
            }
        }

        public void Dispose()
        {
            LastReceivedTime = long.MinValue;

            PhoneMessageCenter.Singleton.Unregister(
                Client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnReconnected
            );

            PhoneMessageCenter.Singleton.Unregister(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived
            );

            EndAll();

            WriteDispatcher.InvokeShutdown();

            try
            {
                file?.Close();
            }
            catch (Exception e) { }

            if (CurrentEntry != null && CurrnetReceived < CurrentEntry.length && CurrnetPath != null && !Directory.Exists(CurrnetPath) && File.Exists(CurrnetPath))
            {
                try
                {
                    File.Delete(CurrnetPath);
                }
                catch (Exception e) { }
            }
        }
    }
}
