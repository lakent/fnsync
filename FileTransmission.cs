using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FnSync
{
    public class FileTransmission : IDisposable
    {
        public static int _Force = -1;

        public const string MSG_TYPE_FILE_TRANSFER_REQUEST_TO = "file_transfer_request_to";
        public const string MSG_TYPE_FILE_TRANSFER_REQUEST_FROM = "file_transfer_request_from";
        public const string MSG_TYPE_FILE_TRANSFER_REQUEST_KEY = "file_transfer_request_key";
        public const string MSG_TYPE_FILE_TRANSFER_REQUEST_KEY_OK = "file_transfer_request_key_ok";
        public const string MSG_TYPE_FILE_TRANSFER_KEY_EXISTS = "file_transfer_key_exists";
        public const string MSG_TYPE_FILE_TRANSFER_KEY_EXISTS_REPLY = "file_transfer_key_exists_reply";
        public const string MSG_TYPE_FILE_TRANSFER_META = "file_transfer_meta";
        public const string MSG_TYPE_FILE_TRANSFER_SEEK = "file_transfer_seek";
        public const string MSG_TYPE_FILE_TRANSFER_SEEK_OK = "file_transfer_seek_ok";
        public const string MSG_TYPE_FILE_TRANSFER_DATA = "file_transfer_data";
        public const string MSG_TYPE_FILE_TRANSFER_END = "file_transfer_end";
        public const string MSG_TYPE_FILE_CONTENT_GET = "file_content_get";
        public const string MSG_TYPE_FILE_CONTENT = "file_content";

        private static readonly PendingsClass Pendings = new PendingsClass();

        public class PendingsClass
        {
            public class Entry
            {
                public string mime;

                private string _name;
                public string name
                {
                    get { return _name; }
                    set
                    {
                        if (value != null)
                        {
                            _name = value;
                            ConvertedName = Utils.ReplaceInvalidFileNameChars(value);
                        }
                    }
                }

                public string ConvertedName { get; private set; } = null;

                public long length;
                public string key = null;
                public long last = -1;

                //
                // Summary:
                //     File path under a specific folder on phone. It's a relative path including the filename
                private string _path = null;
                public string path
                {
                    get
                    {
                        return _path;
                    }
                    set
                    {
                        if (value != null)
                        {
                            ConvertedPath = Utils.ReplaceInvalidFileNameChars(value, true).Replace('/', '\\');
                            _path = value;
                        }
                        else
                        {
                            ConvertedPath = null;
                            _path = value;
                        }
                    }
                }

                public string ConvertedPath { get; private set; } = null;

                public bool IsFolder => ConvertedPath != null && ConvertedPath.EndsWith("\\");
                public bool IsUnderSubfolder
                {
                    get
                    {
                        if (ConvertedPath != null)
                        {
                            int SlashIndex = ConvertedPath.IndexOf("\\");

                            if (SlashIndex >= 0 && SlashIndex != ConvertedPath.Length - 1)
                                return true;
                        }

                        return false;
                    }
                }
            }

            private readonly Dictionary<string, Entry[]> map = new Dictionary<string, Entry[]>();

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
                MSG_TYPE_FILE_TRANSFER_REQUEST_TO,
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

            App.FakeDispatcher.Invoke(delegate
            {
                new WindowFileReceive(
                    new FileTransmission(client, Pendings.GetAndRemove(PendingKey), total)
                    ).Show();
                return null;
            });
        }

        ////////////////////////////////////////////////////////////////////////////////


        private PendingsClass.Entry[] Entries;
        private PhoneClient Client;

        public int FileCount => Entries.Length;
        public string FirstName => Entries[0].name;

        private FileStream file = null;
        private long CurrnetReceived = 0;
        private string CurrnetLocalFilePath = null;
        private long TotalReceived = 0;
        private long TotalSize;

        private long StartupTime = -1;

        private const int UnitSizeInBytes = 1024;
        private int UnitNumber = 10;

        private RequestCacheClass RequestCache;

        private bool IsDisposed = false;

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

                if (Size != 0)
                {
                    this.Percent = (float)((double)Received / (double)Size) * 100f;
                }
                else
                {
                    this.Percent = 100;
                }

                if (TotalSize != 0)
                {
                    this.TotalPercent = (float)((double)TotalReceived / (double)TotalSize) * 100f;
                }
                else
                {
                    this.TotalPercent = 100;
                }

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
            public enum Measure
            {
                SKIP, OVERWRITE, RENAME
            }

            public readonly string Dest;

            public string NewName;
            public Measure Action;

            public FileAlreadyExistEventArgs(string dest)
            {
                this.Dest = dest;
                this.Action = Measure.SKIP;
            }
        }

        public delegate void PercentageChangedEventHandler(object sender, ProgressChangedEventArgs e);
        public delegate void NextFileEventHandler(object sender, NextFileEventArgs e);
        public delegate void FileAlreadyExistEventHandler(object sender, FileAlreadyExistEventArgs e);

        public event PercentageChangedEventHandler ProgressChangedEvent;
        public event NextFileEventHandler OnNextFileEvent;
        public event FileAlreadyExistEventHandler FileAlreadyExistEvent;
        public event EventHandler OnFinishedEvent;
        public event EventHandler OnErrorEvent;

        private enum TransmitionStageClass
        {
            NONE = 0,
            GETTING_KEY,
            ONE_CHUNK_RECEIVE,
            MULTIPLE_CHUNK_RECEIVE
        }

        private TransmitionStageClass TransmitionStage = TransmitionStageClass.NONE;

        private readonly string FileRootOnPhone;

        private void InitFromEntries(PendingsClass.Entry[] entries, long TotalSize = -1)
        {
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
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                Client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnReconnected,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                Client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                OnDisconnected,
                false
            );
        }

        public FileTransmission(PhoneClient client, PendingsClass.Entry[] entries, long TotalSize = -1, string FileRootOnPhone = null)
        {
            this.Client = client;
            this.FileRootOnPhone = FileRootOnPhone;

            if (this.FileRootOnPhone != null && !this.FileRootOnPhone.EndsWith("/"))
            {
                this.FileRootOnPhone += "/";
            }

            InitFromEntries(entries, TotalSize);
        }

        private object WatchJobToken = null;
        private async void WatchJob()
        {
            object token = new object();
            WatchJobToken = token;

            const int IntervalMills = 200;
            long LastReceivedTotal = long.MinValue;

            while (WatchJobToken == token)
            {
                await Task.Delay(IntervalMills);
                if (LastReceivedTotal == TotalReceived)
                {
                    Client?.SendMsgNoThrow(PhoneClient.MSG_TYPE_HELLO);
                }
                else
                {
                    LastReceivedTotal = TotalReceived;
                }
            }
        }

        private void CancelWatchJob()
        {
            WatchJobToken = null;
        }

        private void RequestNext(int count)
        {
            Client.WriteQueued(RequestCache.Get(count, UnitNumber * UnitSizeInBytes));
        }

        private string DestLocalFolder = null;

        private int FileIndex = -1;
        private PendingsClass.Entry CurrentEntry = null;

        public void SetLocalFolder(string LocalFolder)
        {
            DestLocalFolder = LocalFolder;
            if (!DestLocalFolder.EndsWith("\\"))
            {
                DestLocalFolder += "\\";
            }
        }

        public enum TransmissionStatus
        {
            INITIAL = -1,
            SUCCESSFUL = 0,
            FAILED,
            SKIPPED,
            RESET_CURRENT,
        }

        private void RevertToPreviousState()
        {
            file.Close();
            TotalReceived -= CurrnetReceived;
            CurrnetReceived = 0;
            --FileIndex;
        }

        private async Task OneChunkReceive()
        {
            if (String.IsNullOrWhiteSpace(FileRootOnPhone) ||
                String.IsNullOrWhiteSpace(CurrentEntry.ConvertedPath))
            {
                throw new ArgumentException("FileRootOnPhone & CurrentEntry.path");
            }

            TransmitionStage = TransmitionStageClass.ONE_CHUNK_RECEIVE;
            object MsgObject = await PhoneMessageCenter.Singleton.OneShot(
                Client,
                new JObject()
                {
                    ["path"] = FileRootOnPhone + CurrentEntry.path,
                },
                MSG_TYPE_FILE_CONTENT_GET,
                MSG_TYPE_FILE_CONTENT,
                5000
                );

            IMessageWithBinary Msg = MsgObject as IMessageWithBinary;
            WriteBinaryToFile(Msg.Binary, 0);
        }

        private async Task GetKey(bool Force = false)
        {
            if (String.IsNullOrWhiteSpace(CurrentEntry.key) || Force)
            {
                if (String.IsNullOrWhiteSpace(FileRootOnPhone) ||
                    String.IsNullOrWhiteSpace(CurrentEntry.ConvertedPath))
                {
                    throw new ArgumentException("FileRootOnPhone & CurrentEntry.ConvertedPath");
                }

                string key = await PhoneMessageCenter.Singleton.OneShotGetString(
                    Client,
                    new JObject()
                    {
                        ["path"] = FileRootOnPhone + CurrentEntry.path,
                        //["stub_size"] = UnitSizeInBytes * UnitNumber
                    },
                    MSG_TYPE_FILE_TRANSFER_REQUEST_KEY,
                    MSG_TYPE_FILE_TRANSFER_REQUEST_KEY_OK,
                    5000,
                    "key",
                    null
                    );

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("CurrentEntry.key");
                }

                CurrentEntry.key = key;
            }

            RequestCache.Key = CurrentEntry.key;
        }

        private void MultipleChunksReceive()
        {
            TransmitionStage = TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE;
            RequestNext(10);
        }

        private string SpecifiedName = null;

        public async void StartNext(
            string SpecifyName = null,
            TransmissionStatus LastStatus = TransmissionStatus.INITIAL
            )
        {
            TransmitionStage = TransmitionStageClass.NONE;
            this.SpecifiedName = SpecifyName;

            if (IsDisposed)
            {
                return;
            }

            if (DestLocalFolder == null)
            {
                throw new ArgumentNullException();
            }

            if (StartupTime < 0)
                StartupTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (speed == null)
                speed = new SpeedWatch();

            if (speed1 == null)
                speed1 = new SpeedWatch();

            switch (LastStatus)
            {
                case TransmissionStatus.SKIPPED:
                    // Skipped
                    TotalSize -= CurrentEntry.length;
                    break;

                case TransmissionStatus.FAILED:
                    // Failed transmition
                    DeleteCurrentFileIfNotCompleted();
                    break;

                case TransmissionStatus.SUCCESSFUL:
                    // Successful transmition
                    if (file != null)
                    {
                        file.Close();
                        File.SetLastWriteTime(
                            CurrnetLocalFilePath,
                            DateTimeOffset.FromUnixTimeMilliseconds(CurrentEntry.last).LocalDateTime
                        );
                        File.SetLastAccessTime(
                            CurrnetLocalFilePath,
                            DateTimeOffset.FromUnixTimeMilliseconds(CurrentEntry.last).LocalDateTime
                        );

                        if (!String.IsNullOrWhiteSpace(CurrentEntry.key))
                        {
                            Client.SendMsg(
                                new JObject()
                                {
                                    ["key"] = CurrentEntry.key
                                },
                                MSG_TYPE_FILE_TRANSFER_END
                            );
                        }
                    }
                    break;

                case TransmissionStatus.INITIAL:
                    WatchJob();
                    break;

                case TransmissionStatus.RESET_CURRENT:
                    RevertToPreviousState();
                    break;
            }

            ++FileIndex;

            if (FileIndex >= Entries.Length)
            {
                CancelWatchJob();

                App.FakeDispatcher.Invoke(delegate
                {
                    OnFinishedEvent?.Invoke(this, null);
                    return null;
                });

                return;
            }

            App.FakeDispatcher.Invoke(delegate
            {
                OnNextFileEvent?.Invoke(
                        this,
                        new NextFileEventArgs(
                            FileIndex + 1,
                            Entries.Length,
                            string.IsNullOrWhiteSpace(CurrentEntry.path) ? CurrentEntry.name : CurrentEntry.path
                            )
                        );
                return null;
            });

            CurrnetReceived = 0;
            CurrentEntry = Entries[FileIndex];

            try
            {
                if (CurrentEntry.IsFolder)
                {   // A Folder
                    Directory.CreateDirectory(DestLocalFolder + CurrentEntry.ConvertedPath);
                    StartNext(null, TransmissionStatus.SUCCESSFUL);
                    return;
                }
                else
                {   // A File
                    string FinalDestLocalName = SpecifiedName ?? CurrentEntry.ConvertedName;

                    string DestLocalPath;
                    string Subfolder = "";
                    if (CurrentEntry.IsUnderSubfolder)
                    {
                        Subfolder = Path.GetDirectoryName(CurrentEntry.ConvertedPath) + '\\';
                    }

                    DestLocalPath = DestLocalFolder + Subfolder + FinalDestLocalName;

                    if (LastStatus != TransmissionStatus.RESET_CURRENT &&
                        (File.Exists(DestLocalPath) || Directory.Exists(DestLocalPath)))
                    {
                        FileAlreadyExistEventArgs args = new FileAlreadyExistEventArgs(DestLocalPath);

                        CancelWatchJob();
                        await App.FakeDispatcher.Invoke(delegate
                        {
                            FileAlreadyExistEvent?.Invoke(this, args);
                            return null;
                        });
                        WatchJob();

                        switch (args.Action)
                        {
                            case FileAlreadyExistEventArgs.Measure.SKIP:
                                StartNext(null, TransmissionStatus.SKIPPED);
                                return;

                            case FileAlreadyExistEventArgs.Measure.OVERWRITE:
                                break;

                            case FileAlreadyExistEventArgs.Measure.RENAME:
                                if (args.NewName == null)
                                {
                                    StartNext(null, TransmissionStatus.FAILED);
                                    return;
                                }

                                DestLocalPath = DestLocalFolder + Subfolder + args.NewName;
                                break;
                        }
                    }

                    CurrnetLocalFilePath = DestLocalPath;

                    file = File.Open(CurrnetLocalFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    file.SetLength(CurrentEntry.length);

                    if (CurrentEntry.length == 0)
                    {
                        StartNext(null, TransmissionStatus.SUCCESSFUL);
                    }
                    else if (CurrentEntry.length <= Math.Max(UnitSizeInBytes * UnitNumber, 100 * 1024) &&
                        String.IsNullOrWhiteSpace(CurrentEntry.key))
                    {
                        await OneChunkReceive();
                        StartNext(null, TransmissionStatus.SUCCESSFUL);
                    }
                    else
                    {
                        TransmitionStage = TransmitionStageClass.GETTING_KEY;
                        await GetKey();
                        MultipleChunksReceive();
                    }
                }
            }
            catch (TimeoutException e)
            {

            }
            catch (PhoneMessageCenter.PhoneDisconnectedException e)
            {

            }
            catch (Exception e)
            {
                StartNext(null, TransmissionStatus.FAILED);
            }
        }

        private SpeedWatch speed = null;
        private SpeedWatch speed1 = null;
        private double LargestSpeed = 0;
        private void WriteBinaryToFile(byte[] Binary, long Offset)
        {
            int length = Binary.Length;

            file.Position = Offset;
            file.Write(Binary, 0, length);

            CurrnetReceived += length;
            TotalReceived += length;

            speed.Add(length);
            speed1.Add(length);

            double BytesPerSec = speed.BytesPerSec(250);

            if (BytesPerSec >= 0)
            {
                App.FakeDispatcher.Invoke(delegate
                {
                    ProgressChangedEvent?.Invoke(
                    this,
                    new ProgressChangedEventArgs(
                        CurrnetReceived, CurrentEntry.length,
                        TotalReceived, TotalSize,
                        BytesPerSec)
                    );
                    return null;
                });

                speed.Reset();
            }

            double BytesPer1 = speed1.BytesPerSec(1500);
            if (BytesPer1 >= 0)
            {
                if (BytesPer1 > LargestSpeed)
                {
                    LargestSpeed = BytesPer1;
                    UnitNumber += 3;
                }
                else
                {
                    //UnitNumber = Math.Max(UnitNumber / 2, 1);
                }

                speed1.Reset();
            }
        }

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

                start = CurrnetReceived;
            }
            else
            {
                if (start + length > CurrentEntry.length)
                    return;
            }

            WriteBinaryToFile(msg.Binary, start);

            if (CurrnetReceived < CurrentEntry.length)
            {
                RequestNext(1);
            }
            else
            {
                StartNext(null, TransmissionStatus.SUCCESSFUL);
            }
        }

        private async void OnReconnected(string id, string msgType, object msgObj, PhoneClient client)
        {
            this.Client = client;

            if (CurrentEntry != null)
            {
                await Task.Delay(1000);
                if (this.Client != client)
                {
                    // Phones may reconnect multiple times within this 1 second delayed above.
                    return;
                }

                try
                {
                    switch (TransmitionStage)
                    {
                        case TransmitionStageClass.GETTING_KEY:
                        case TransmitionStageClass.ONE_CHUNK_RECEIVE:
                            StartNext(SpecifiedName, TransmissionStatus.RESET_CURRENT);
                            break;

                        case TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE:
                            if (string.IsNullOrWhiteSpace(CurrentEntry.path))
                            {
                                throw new Exception();
                            }

                            bool KeyIsExist = await PhoneMessageCenter.Singleton.OneShotGetBoolean(
                                client,
                                new JObject()
                                {
                                    ["path"] = CurrentEntry.path,
                                },
                                MSG_TYPE_FILE_TRANSFER_KEY_EXISTS,
                                MSG_TYPE_FILE_TRANSFER_KEY_EXISTS_REPLY,
                                5000,
                                "exists",
                                false
                                );

                            if (!KeyIsExist)
                            {
                                await GetKey(true);
                            }

                            long SeekResult = await PhoneMessageCenter.Singleton.OneShotGetLong(
                                client,
                                new JObject()
                                {
                                    ["key"] = CurrentEntry.key,
                                    ["position"] = CurrnetReceived
                                },
                                MSG_TYPE_FILE_TRANSFER_SEEK,
                                MSG_TYPE_FILE_TRANSFER_SEEK_OK,
                                5000,
                                "position",
                                -1
                                );

                            if (SeekResult != CurrnetReceived)
                            {
                                throw new Exception();
                            }

                            file.Position = SeekResult;

                            RequestNext(10);
                            break;

                        default:
                            break;
                    }
                }
                catch (TimeoutException e)
                {
                    return;
                }
                catch (PhoneMessageCenter.PhoneDisconnectedException e)
                {
                    return;
                }
                catch (Exception e)
                {
                    App.FakeDispatcher.Invoke(delegate
                    {
                        OnErrorEvent?.Invoke(this, null);
                        return null;
                    });
                    return;
                }
            }
        }

        private void OnDisconnected(string id, string msgType, object msgObj, PhoneClient client)
        {
            if (CurrentEntry != null)
            {
                if (TransmitionStage == TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE && string.IsNullOrWhiteSpace(CurrentEntry.path))
                {
                    OnErrorEvent?.Invoke(this, null);
                }
            }
        }

        private void EndAll()
        {
            foreach (PendingsClass.Entry entry in Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.key))
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
        }

        public void DeleteCurrentFileIfNotCompleted()
        {
            if (CurrentEntry != null && CurrnetReceived < CurrentEntry.length && CurrnetLocalFilePath != null && !Directory.Exists(CurrnetLocalFilePath) && File.Exists(CurrnetLocalFilePath))
            {
                try
                {
                    File.Delete(CurrnetLocalFilePath);
                }
                catch (Exception e) { }
            }
        }

        public void Dispose()
        {
            IsDisposed = true;

            CancelWatchJob();

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

            PhoneMessageCenter.Singleton.Unregister(
                Client.Id,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                OnDisconnected
            );

            EndAll();

            try
            {
                file?.Close();
            }
            catch (Exception e) { }

            DeleteCurrentFileIfNotCompleted();
        }
    }
}
