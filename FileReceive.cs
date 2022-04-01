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
    public class FileReceive : FileTransHandler
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
            long total = BaseEntry.LengthOfAllFiles(entries);

            string header = string.Format(
                entries.Length > 1 ? FILES_RECEIVED : ONE_FILE_RECEIVED,
                entries.Length,
                Utils.ToHumanReadableSize(total)
                );

            string body = PendingsClass.First5Names(entries);

            ToastContentBuilder Builder = new ToastContentBuilder()
                .AddHeader(id, client.Name, "")
                .AddText(header, hintMinLines:1)
                .AddText(body)
                .AddButton(new ToastButton()
                    .SetContent(SAVE_AS)
                    .AddArgument("FileReceive_SaveAs")
                    .AddArgument("pendingkey", PendingKey)
                    .AddArgument("totalsize", total.ToString())
                    .AddArgument("clientid", id))
                .AddButton(new ToastButton()
                    .SetContent(CANCEL)
                    .AddArgument("FileReceive_SaveAs")
                    .AddArgument("cancelpending", PendingKey))
                ;

            NotificationSubchannel.Singleton.Push(Builder);

            /*
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
            */
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
            string ClientId = queries["clientid"];

            if (AlivePhones.Singleton[ClientId] == null || !Pendings.Contains(PendingKey))
            {
                return;
            }

            App.FakeDispatcher.Invoke(delegate
            {
                /*
                new WindowFileOperation(
                    new FileReceive().Apply<IBase>(it =>
                    {
                        it.Init(client, Pendings.GetAndRemove(PendingKey), total);
                    })
                    ).Show();
                */
                new WindowFileOperation(DirectionClass.PHONE_TO_PC, OperationClass.COPY, ClientId)
                .SetEntryList(Pendings.GetAndRemove(PendingKey))
                .Show();
                return null;
            });
        }

        ////////////////////////////////////////////////////////////////////////////////

        /*
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

            private readonly string Key = null;

            private readonly Dictionary<Entry, string> Cache = new Dictionary<Entry, string>(2);

            public RequestCacheClass(string Key)
            {
                this.Key = Key;
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
        */


        private FileStream CurrentFileStream = null;

        public FileReceive()
        {
        }

        /*
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
            ChunkSizeCalculator = new ChunkSizeCalculatorClass();

            PhoneMessageCenter.Singleton.Register(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA,
                OnBlobReceived,
                false
            );
        }
        */

        public string DestinationFolder { get; private set; }
        public string FileRootOnSource { get; private set; }

        public override void Initialization(string ClientId, BaseEntry Entry, string DestFolder = null, string SrcFolder = null,
            string DestStorage = null, string SrcStorage = null,
            ChunkSizeCalculatorClass ChunkSizeCaclulator = null)
        {
            base.Initialization(ClientId, Entry, DestFolder, SrcFolder, DestStorage, SrcStorage, ChunkSizeCaclulator);
            this.DestinationFolder = DestFolder.AppendIfNotEnding("\\");
            this.FileRootOnSource = SrcFolder;
        }

        private async Task OneChunkReceive(ReceiveEntry entry)
        {
            if (String.IsNullOrWhiteSpace(FileRootOnSource) ||
                String.IsNullOrWhiteSpace(entry.ConvertedPath))
            {
                throw new ArgumentNullException("FileRootOnPhone & entry.path");
            }

            while (true) try
                {
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

                    return;
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {
                    this.Client = await PhoneMessageCenter.Singleton.WaitOnline(this.Client.Id, int.MaxValue, Cancellation);
                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    this.Client = e.Current;
                }
        }

        private async Task AcquireKey(bool Force = false)
        {
            ReceiveEntry entry = Entry as ReceiveEntry;
            if (!String.IsNullOrWhiteSpace(entry.key) && !Force)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(FileRootOnSource) ||
                String.IsNullOrWhiteSpace(entry.ConvertedPath))
            {
                throw new ArgumentException("FileRootOnPhone & entry.ConvertedPath");
            }

            while (true) try
                {
                    string key = await PhoneMessageCenter.Singleton.OneShotGetString(
                        Client,
                        new JObject()
                        {
                            ["path"] = FileRootOnSource + entry.path,
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

                    return;
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {
                    this.Client = await PhoneMessageCenter.Singleton.WaitOnline(this.Client.Id, int.MaxValue, Cancellation);
                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    this.Client = e.Current;
                }
        }

        private bool ChunksReceivedCallback(object MsgObj)
        {
            if (!(MsgObj is MessageWithBinary mwb))
            {
                throw new FileTransException("");
            }

            JObject msg = mwb.Message;
            byte[] binary = mwb.Binary;

            long start = msg.OptLong("position", -1);
            int length = binary.Length;

            if (start < 0)
            {
                start = this.TransmittedLength;
            }

            if (start == 0)
            {
                if (length > this.Entry.length)
                {
                    throw new FileTransException("Wrong length");
                }
            }
            else
            {
                if (start + length > this.Entry.length)
                {
                    throw new FileTransException("Wrong length");
                }
            }

            WriteBinaryToFile(binary, start);

            if (this.TransmittedLength == this.Entry.length)
            {
                return false; // false to break
            }
            else if (this.TransmittedLength > this.Entry.length)
            {
                throw new FileTransException("Wrong length");
            }
            else
            {
                // continue;
            }

            return true;
        }

        private async Task ChunksReceive()
        {
            while (this.TransmittedLength < this.Entry.length)
            {
                int Count = 10;

                Task WaitingTask = PhoneMessageCenter.Singleton.WaitForMessage(
                    Client.Id,
                    MSG_TYPE_FILE_TRANSFER_DATA,
                    600000,
                    ChunksReceivedCallback,
                    Count,
                    Cancellation
                    );

                Client.SendMsg(
                    new JObject
                    {
                        ["key"] = this.Entry.key,
                        ["count"] = Count,
                        ["length"] = ChunkSizeCalculator.ChunkSize,
                    },
                    MSG_TYPE_FILE_TRANSFER_META
                    );

                await WaitingTask;
            }
        }

        private async Task MultipleChunksReceive()
        {
            while (true) try
                {
                    if (string.IsNullOrWhiteSpace(Entry.key))
                    {
                        await AcquireKey();
                    }
                    else
                    {
                        bool KeyIsExist = await PhoneMessageCenter.Singleton.OneShotGetBoolean(
                            Client,
                            new JObject()
                            {
                                ["key"] = Entry.key,
                            },
                            MSG_TYPE_FILE_TRANSFER_KEY_EXISTS,
                            MSG_TYPE_FILE_TRANSFER_KEY_EXISTS_REPLY,
                            5000,
                            "exists",
                            false
                            );

                        if (!KeyIsExist)
                        {
                            await AcquireKey(true);
                        }

                        long SeekResult = await PhoneMessageCenter.Singleton.OneShotGetLong(
                            Client,
                            new JObject()
                            {
                                ["key"] = Entry.key,
                                ["position"] = TransmittedLength
                            },
                            MSG_TYPE_FILE_TRANSFER_SEEK,
                            MSG_TYPE_FILE_TRANSFER_SEEK_OK,
                            5000,
                            "position",
                            -1
                            );

                        if (SeekResult != TransmittedLength)
                        {
                            throw new FileTransException(Entry.name);
                        }
                    }

                    await ChunksReceive();

                    return;
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {
                    this.Client = await PhoneMessageCenter.Singleton.WaitOnline(this.Client.Id, int.MaxValue, Cancellation);
                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    this.Client = e.Current;
                }
        }

        private void WriteBinaryToFile(byte[] Binary, long Offset)
        {
            int length = Binary.Length;

            CurrentFileStream.Position = Offset;
            CurrentFileStream.Write(Binary, 0, length);

            AddTransmitLength(length);
        }

        private string GetLocalPath(ReceiveEntry entry)
        {
            return entry.ConvertedPath != null ? this.DestinationFolder + entry.ConvertedPath : null;
        }

        public void DeleteCurrentFileIfNotCompleted()
        {
            if (TransmittedLength == this.Entry.length)
            {
                return;
            }

            string path = GetLocalPath(this.Entry as ReceiveEntry);

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

        public override Task<bool> DetermineFileExistence()
        {
            string path = GetLocalPath(this.Entry as ReceiveEntry);

            if (this.Entry.IsFolder)
            {
                if (File.Exists(path))
                {
                    throw new FileTransException(path);
                }

                return Task.FromResult(false);
            }
            else
            {
                if (Directory.Exists(path))
                {
                    throw new FileTransException(path);
                }

                return Task.FromResult(File.Exists(path));
            }
        }

        public override async Task Transmit(FileAlreadyExistEventArgs.Measure Measure)
        {
            await base.Transmit(Measure);
            ReceiveEntry entry = this.Entry as ReceiveEntry;
            if (Measure == FileAlreadyExistEventArgs.Measure.RENAME)
            {
                NewNameOperation(entry);
            }

            string DestLocalPath = GetLocalPath(entry);

            if (entry.IsFolder)
            {
                _ = Directory.CreateDirectory(DestLocalPath);
                return;
            }

            // Is a File
            _ = Directory.CreateDirectory(Path.GetDirectoryName(DestLocalPath));
            CurrentFileStream = File.Open(DestLocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            CurrentFileStream.SetLength(entry.length);

            if (entry.length == 0)
            {
                return;
            }
            else if (entry.length <= Math.Max(ChunkSizeCalculator.ChunkSize, 100 * 1024) /* &&
                String.IsNullOrWhiteSpace(entry.key) */ )
            {
                await OneChunkReceive(entry);
            }
            else
            {
                await MultipleChunksReceive();
            }

            CurrentFileStream.Close();

            if (entry.last >= 0)
            {
                // Set file time

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

        public override void Dispose()
        {
            base.Dispose();
            DeleteCurrentFileIfNotCompleted();
            CurrentFileStream?.Close();
            EndOneEntry();
        }
    }
}
