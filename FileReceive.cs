using FnSync.FileTransmission;
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
    public class FileReceive : BaseModule<FileReceive.ReceiveEntry>
    {
        public class ReceiveEntry : BaseEntry
        {

            private string _name;
            public override string name
            {
                get { return _name; }
                set
                {
                    if (value != null)
                    {
                        _name = value;
                        ConvertedName = value;
                    }
                }
            }

            public string _convertedName = null;
            public override string ConvertedName
            {
                get
                {
                    return _convertedName;
                }
                set
                {
                    _convertedName = Utils.ReplaceInvalidFileNameChars(value);

                    if (!string.IsNullOrWhiteSpace(ConvertedPath))
                    {
                        string parent = Path.GetDirectoryName(ConvertedPath.TrimEnd('\\'));
                        ConvertedPath = parent + '\\' + ConvertedName + (IsFolder ? "\\" : "");
                    }
                    else
                    {
                        ConvertedPath = ConvertedName;
                    }
                }
            }

            //
            // Summary:
            //     File path under a specific folder on phone. It's a relative path including the filename
            public override string path
            {
                get
                {
                    return base.path;
                }
                set
                {
                    base.path = value;
                    if (value != null)
                    {
                        ConvertedPath = Utils.ReplaceInvalidFileNameChars(value, true).Replace('/', '\\');
                    }
                    else
                    {
                        ConvertedPath = null;
                    }
                }
            }

            public override string ConvertedPath { get; set; } = null;

            public override bool IsFolder => ConvertedPath != null && ConvertedPath.EndsWith("\\");
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
            private readonly Dictionary<string, ReceiveEntry[]> map = new Dictionary<string, ReceiveEntry[]>();

            public PendingsClass()
            {

            }

            public string Add(JArray files)
            {
                List<ReceiveEntry> entries = files.ToObject<List<ReceiveEntry>>();

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

            public ReceiveEntry[] Get(string PendingKey)
            {
                return map.ContainsKey(PendingKey) ? map[PendingKey] : null;
            }

            public void Remove(string PendingKey)
            {
                map.Remove(PendingKey);
            }

            public ReceiveEntry[] GetAndRemove(string PendingKey)
            {
                ReceiveEntry[] entries = Get(PendingKey);
                Remove(PendingKey);
                return entries;
            }


            public static string First5Names(IEnumerable<ReceiveEntry> entries)
            {
                StringBuilder sb = new StringBuilder();
                int count = 0;

                foreach (ReceiveEntry entry in entries)
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

        static FileReceive()
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

            ReceiveEntry[] entries = Pendings.Get(PendingKey);
            long total = ReceiveEntry.SizeOfAllFiles(entries);

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
            // 'Save As' from Notification

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
                new WindowFileOperation(
                    new FileReceive().Apply<IBase>(it =>
                    {
                        it.Init(client, Pendings.GetAndRemove(PendingKey), total);
                    })
                    ).Show();
                return null;
            });
        }

        ////////////////////////////////////////////////////////////////////////////////


        private FileStream file = null;

        private ChunkRequestCache RequestCache;
        private ChunkSizeCalculatorClass SizeCalculator;

        private bool IsDisposed = false;

        private enum TransmitionStageClass
        {
            NONE = 0,
            GETTING_KEY,
            ONE_CHUNK_RECEIVE,
            MULTIPLE_CHUNK_RECEIVE
        }

        public override event EventHandler OnErrorEvent;

        private TransmitionStageClass TransmitionStage = TransmitionStageClass.NONE;

        public FileReceive()
        {
            Operation = OperationClass.COPY;
            Direction = DirectionClass.PHONE_TO_PC;
        }

        public override void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnPhone = null)
        {
            base.Init(client, Entries, TotalSize, FileRootOnPhone);

            RequestCache = new ChunkRequestCache
            {
                StringMaker = delegate (string Key, int Count, int Length)
                {
                    return new JObject
                    {
                        ["key"] = Key,
                        ["count"] = Count,
                        ["length"] = Length,
                        [PhoneClient.MSG_TYPE_KEY] = MSG_TYPE_FILE_TRANSFER_META
                    }.ToString(Newtonsoft.Json.Formatting.None);
                }
            };
            SizeCalculator = new ChunkSizeCalculatorClass();

            PhoneMessageCenter.Singleton.Register(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived,
                false
            );
        }

        private void RequestNext(int UnitCount)
        {
            Client.WriteQueued(RequestCache.Get(UnitCount, SizeCalculator.ChunkSize));
        }

        private string DestLocalFolder = null;
        public override string DestinationFolder
        {
            get => DestLocalFolder;
            set
            {
                DestLocalFolder = value.AppendIfNotEnding("\\");
            }
        }

        private async Task OneChunkReceive(ReceiveEntry entry)
        {
            if (String.IsNullOrWhiteSpace(FileRootOnSource) ||
                String.IsNullOrWhiteSpace(entry.ConvertedPath))
            {
                throw new ArgumentException("FileRootOnPhone & entry.path");
            }

            TransmitionStage = TransmitionStageClass.ONE_CHUNK_RECEIVE;
            object MsgObject = await PhoneMessageCenter.Singleton.OneShot(
                Client,
                new JObject()
                {
                    ["path"] = FileRootOnSource + entry.path,
                },
                MSG_TYPE_FILE_CONTENT_GET,
                null,
                MSG_TYPE_FILE_CONTENT,
                5000
                );

            IMessageWithBinary Msg = MsgObject as IMessageWithBinary;
            WriteBinaryToFile(Msg.Binary, 0);
        }

        private async Task AcquireKey(ReceiveEntry entry, bool Force = false)
        {
            if (String.IsNullOrWhiteSpace(entry.key) || Force)
            {
                if (String.IsNullOrWhiteSpace(FileRootOnSource) ||
                    String.IsNullOrWhiteSpace(entry.ConvertedPath))
                {
                    throw new ArgumentException("FileRootOnPhone & entry.ConvertedPath");
                }

                string key = await PhoneMessageCenter.Singleton.OneShotGetString(
                    Client,
                    new JObject()
                    {
                        ["path"] = FileRootOnSource + entry.path,
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
                    throw new ArgumentException("entry.key");
                }

                entry.key = key;
            }

            RequestCache.RemoteKey = entry.key;
        }

        private void MultipleChunksReceive()
        {
            TransmitionStage = TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE;
            RequestNext(10);
        }

        private SpeedWatch SpeedLong = null;
        private double LargestSpeed = 0;

        protected override void AddTransmitLength(long len)
        {
            base.AddTransmitLength(len);
            SpeedLong.Add(len);

            double BytesPer = SpeedLong.BytesPerSec(2000);
            if (BytesPer >= 0)
            {
                if (BytesPer > LargestSpeed)
                {
                    LargestSpeed = BytesPer;
                    SizeCalculator.UnitCount += 4;
                }
                else
                {
                    /*
                    if(UnitCoefficient > 10)
                    {
                        UnitCoefficient -= 1;
                    }
                    */
                }

                SpeedLong.Reset();
            }
        }

        public override void StartTransmittion()
        {
            SpeedLong = new SpeedWatch();
            StartWatchJob();
            base.StartTransmittion();
        }

        private void WriteBinaryToFile(byte[] Binary, long Offset)
        {
            int length = Binary.Length;

            file.Position = Offset;
            file.Write(Binary, 0, length);
            // XXX TODO: Not thread-safe here

            AddTransmitLength(length);
        }

        private void OnBlobReceived(string id, string msgType, object msgObj, PhoneClient client)
        {
            if (!(msgObj is MessageWithBinary msg))
                return;

            if ((string)msg.Message["key"] != CurrentEntry?.key)
                return;

            long start = msg.Message.OptLong("position", -1);
            int length = msg.Binary.Length;

            if (start < 0)
            {
                if (length > CurrentEntry.length - CurrnetTransmittedLength)
                    return;

                start = CurrnetTransmittedLength;
            }
            else
            {
                if (start + length > CurrentEntry.length)
                    return;
            }

            WriteBinaryToFile(msg.Binary, start);

            if (CurrnetTransmittedLength < CurrentEntry.length)
            {
                RequestNext(1);
            }
            else
            {
                StartNext(TransmissionStatus.SUCCESSFUL);
            }
        }

        protected override async Task OnReconnected()
        {
            try
            {
                switch (TransmitionStage)
                {
                    case TransmitionStageClass.GETTING_KEY:
                    case TransmitionStageClass.ONE_CHUNK_RECEIVE:
                        StartNext(TransmissionStatus.RESET_CURRENT);
                        break;

                    case TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE:
                        if (string.IsNullOrWhiteSpace(CurrentEntry.key))
                        {
                            // It this happended, there are some bugs.
                            throw new Exception();
                        }

                        bool KeyIsExist = await PhoneMessageCenter.Singleton.OneShotGetBoolean(
                            Client,
                            new JObject()
                            {
                                ["key"] = CurrentEntry.key,
                            },
                            MSG_TYPE_FILE_TRANSFER_KEY_EXISTS,
                            MSG_TYPE_FILE_TRANSFER_KEY_EXISTS_REPLY,
                            5000,
                            "exists",
                            false
                            );

                        if (!KeyIsExist)
                        {
                            await AcquireKey(CurrentEntry, true);
                        }

                        long SeekResult = await PhoneMessageCenter.Singleton.OneShotGetLong(
                            Client,
                            new JObject()
                            {
                                ["key"] = CurrentEntry.key,
                                ["position"] = CurrnetTransmittedLength
                            },
                            MSG_TYPE_FILE_TRANSFER_SEEK,
                            MSG_TYPE_FILE_TRANSFER_SEEK_OK,
                            5000,
                            "position",
                            -1
                            );

                        if (SeekResult != CurrnetTransmittedLength)
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
                OnErrorEvent?.Invoke(this, null);
                return;
            }
        }

        protected override Task OnDisconnected()
        {
            if (TransmitionStage == TransmitionStageClass.MULTIPLE_CHUNK_RECEIVE &&
                string.IsNullOrWhiteSpace(CurrentEntry.path) /* If it is from ContentProvider, cannot resume after reconnect */)
            {
                OnErrorEvent?.Invoke(this, null);
            }

            return Task.CompletedTask;
        }

        private void EndAll()
        {
            foreach (ReceiveEntry entry in EntryList)
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

        private string GetLocalPath(ReceiveEntry entry)
        {
            return entry.ConvertedPath != null ? DestLocalFolder + entry.ConvertedPath : null;
        }

        public void DeleteCurrentFileIfNotCompleted()
        {
            try
            {
                file?.Close();
            }
            catch (Exception e) { }

            if (CurrentEntry == null || CurrnetTransmittedLength >= CurrentEntry.length)
            {
                return;
            }

            string path = GetLocalPath(CurrentEntry);

            if (path == null || Directory.Exists(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception e) { }

        }

        public override void Dispose()
        {
            base.Dispose();
            IsDisposed = true;

            PhoneMessageCenter.Singleton.Unregister(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived
            );

            EndAll();

            DeleteCurrentFileIfNotCompleted();
        }

        protected override void ResetCurrentFileTransmisionAction(ReceiveEntry entry)
        {
            file.Close();
        }

        protected override void FileFailedCleanUpAction(ReceiveEntry entry)
        {
            DeleteCurrentFileIfNotCompleted();
        }

        protected override Task<bool> DetermineFileExistence(ReceiveEntry entry)
        {
            string path = GetLocalPath(CurrentEntry);

            if (entry.IsFolder)
            {
                if (File.Exists(path))
                {
                    throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
                }

                return Task<bool>.FromResult(false);
            }
            else
            {
                return Task<bool>.FromResult(File.Exists(path) || Directory.Exists(path));
            }
        }

        protected override async Task Transmit(ReceiveEntry entry, FileAlreadyExistEventArgs.Measure Measure)
        {
            if (Measure == FileAlreadyExistEventArgs.Measure.RENAME)
            {
                NewNameOperation(entry);
            }

            string DestLocalPath = GetLocalPath(entry);

            if (entry.IsFolder)
            {
                try
                {
                    Directory.CreateDirectory(DestLocalPath);
                }
                catch (Exception e)
                {
                    throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
                }

                throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
            }

            // Is a File
            try
            {
                file = File.Open(DestLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
                file.SetLength(entry.length);
            }
            catch (Exception e)
            {
                throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
            }

            if (entry.length == 0)
            {
                throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
            }
            else if (entry.length <= Math.Max(SizeCalculator.ChunkSize, 100 * 1024) &&
                String.IsNullOrWhiteSpace(entry.key))
            {
                await OneChunkReceive(entry);
                throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
            }
            else
            {
                TransmitionStage = TransmitionStageClass.GETTING_KEY;
                await AcquireKey(entry);
                MultipleChunksReceive();
            }
        }

        protected override void FileTransmitSuccessAction(ReceiveEntry entry)
        {
            if (entry.IsFolder)
                return;

            if (file == null)
                return;

            string DestLocalPath = GetLocalPath(entry);

            file.Close();

            if (entry.last >= 0)
            {
                DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(entry.last).LocalDateTime;

                File.SetLastWriteTime(
                    DestLocalPath,
                    time
                );
                File.SetCreationTime(
                    DestLocalPath,
                    time
                );
                File.SetLastAccessTime(
                    DestLocalPath,
                    time
                );
            }

            if (!String.IsNullOrWhiteSpace(entry.key))
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

        private void NewNameOperation(ReceiveEntry entry)
        {
            string dirpart = DestinationFolder + Path.GetDirectoryName(entry.ConvertedPath);
            string namepart = Path.GetFileNameWithoutExtension(entry.ConvertedPath);
            string extension = Path.GetExtension(entry.ConvertedPath);

            for (int i = 2; i <= int.MaxValue; ++i)
            {
                string newname = $"{namepart} ({i}){extension}";
                string path = Path.Combine(dirpart, newname);
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    entry.ConvertedName = newname;
                    return;
                }
            }
        }
    }
}
