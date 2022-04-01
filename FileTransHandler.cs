using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    public abstract class FileTransHandler : IDisposable
    {
        public enum ListModeClass
        {
            DEEP,
            PLAIN_WITHOUT_FOLDER_LENGTH,
            PLAIN_WITH_FOLDER_LENGTH,
        }

        public enum OperationClass
        {
            CUT,
            COPY
        }

        public enum DirectionClass
        {
            INSIDE_PHONE,
            PHONE_TO_PC,
            PC_TO_PHONE,
        }

        public class BaseEntry
        {
            private static async Task<IList<ControlFolderListItemViewBase.UiItem>> ListDirFiles(PhoneClient Client, string FullPath)
            {
                JObject List = await PhoneMessageCenter.Singleton.OneShotMsgPart(
                    Client,
                    new JObject()
                    {
                        ["path"] = FullPath,
                        ["recursive"] = true
                    },
                    ControlFolderListPhoneRootItem.MSG_TYPE_LIST_FOLDER,
                    null,
                    ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT,
                    60000
                );

                JArray ListPart = (JArray)List["files"];
                List<ControlFolderListItemViewBase.UiItem> Files = ListPart.ToObject<List<ControlFolderListItemViewBase.UiItem>>();

                return Files;
            }

            public static async Task<T[]> ConvertFromUiItems<T>(IEnumerable<ControlFolderListItemViewBase.UiItem> UiItems, String RootOnPhone, PhoneClient Client, ListModeClass ListMode = ListModeClass.DEEP) where T : BaseEntry, new()
            {
                List<T> ResultEntries = new List<T>();

                foreach (ControlFolderListItemViewBase.UiItem item in UiItems)
                {
                    if (item.type == "dir")
                    {
                        string FolderPathOnPhone = item.path.AppendIfNotEnding("/");

                        if (ListMode == ListModeClass.DEEP)
                        {
                            ResultEntries.Add(new T()
                            {
                                key = null,
                                length = 0,
                                mime = null,
                                name = item.name,
                                path = FolderPathOnPhone,
                                last = item.last
                            });

                            IList<ControlFolderListItemViewBase.UiItem> Files = await ListDirFiles(Client, RootOnPhone + item.path);

                            foreach (ControlFolderListItemViewBase.UiItem i in Files)
                            {
                                if (i.type == "dir")
                                {
                                    ResultEntries.Add(new T()
                                    {
                                        key = null,
                                        length = 0,
                                        mime = null,
                                        name = i.name,
                                        path = FolderPathOnPhone + i.path.AppendIfNotEnding("/"),
                                        last = item.last
                                    });
                                }
                                else if (i.type == "file")
                                {
                                    ResultEntries.Add(new T()
                                    {
                                        key = null,
                                        length = i.size,
                                        mime = null,
                                        name = i.name,
                                        path = FolderPathOnPhone + i.path,
                                        last = item.last
                                    });
                                }
                            }
                        }
                        else if (ListMode == ListModeClass.PLAIN_WITH_FOLDER_LENGTH)
                        {
                            IList<ControlFolderListItemViewBase.UiItem> Files = await ListDirFiles(Client, RootOnPhone + item.path);

                            long DirLength = 0;
                            foreach (ControlFolderListItemViewBase.UiItem i in Files)
                            {
                                DirLength += i.size;
                            }

                            ResultEntries.Add(new T()
                            {
                                key = null,
                                length = DirLength,
                                mime = null,
                                name = item.name,
                                path = FolderPathOnPhone,
                                last = item.last
                            });
                        }
                        else if (ListMode == ListModeClass.PLAIN_WITHOUT_FOLDER_LENGTH)
                        {
                            ResultEntries.Add(new T()
                            {
                                key = null,
                                length = 0,
                                mime = null,
                                name = item.name,
                                path = FolderPathOnPhone,
                                last = item.last
                            });
                        }
                    }
                    else if (item.type == "file")
                    {
                        ResultEntries.Add(new T()
                        {
                            key = null,
                            length = item.size,
                            mime = null,
                            name = item.name,
                            path = item.path,
                            last = item.last
                        });
                    }
                }

                return ResultEntries.ToArray();
            }

            public static long LengthOfAllFiles(IEnumerable<BaseEntry> entries)
            {
                long ret = 0;

                foreach (BaseEntry entry in entries)
                {
                    ret += entry.length;
                }

                return ret;
            }

            ////////////////////////////////////////////////////////

            public virtual string mime { get; set; }

            public virtual string name { get; set; }
            public virtual string ConvertedName
            {
                get => name;
                set { }
            }

            public virtual long length { get; set; }
            public virtual string key { get; set; }

            // Last Modified in Milliseconds
            public virtual long last { get; set; } = -1;

            //
            // Summary:
            //     File path under a specific folder on source. It's a relative path including the filename
            public virtual string path { get; set; }
            public virtual string ConvertedPath
            {
                get => path;
                set { throw new InvalidOperationException("Cannot set ConvertedPath"); }
            }

            public virtual bool IsFolder => path != null && path.EndsWith("/");
        }

        public class FileTransException : Exception
        {
            public FileTransException(string Message) : base(Message)
            {
            }
        }

        public class HandlerList : IList<FileTransHandler>, IDisposable
        {
            private class HandlerEnum : IEnumerator<FileTransHandler>
            {
                public HandlerList HList;

                int position = -1;

                public HandlerEnum(HandlerList list)
                {
                    HList = list;
                }

                public bool MoveNext()
                {
                    position++;
                    return position < HList.EntryList.Count;
                }

                public void Reset()
                {
                    position = -1;
                }

                public void Dispose()
                {
                }

                object IEnumerator.Current
                {
                    get
                    {
                        return Current;
                    }
                }

                public FileTransHandler Current
                {
                    get
                    {
                        try
                        {
                            return HList[position];
                        }
                        catch (IndexOutOfRangeException)
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
            }

            public readonly IList<BaseEntry> EntryList;
            private readonly List<FileTransHandler> InnerList;

            private readonly string ClientId;
            private readonly string DestFolder;
            private readonly string SrcFolder;
            private readonly string DestStorage;
            private readonly string SrcStorage;

            public readonly ChunkSizeCalculatorClass SharedChunkSizeCalculator = new ChunkSizeCalculatorClass();

            public HandlerList(
                IList<BaseEntry> EntryList,
                string ClientId,
                string DestFolder = null,
                string SrcFolder = null,
                string DestStorage = null,
                string SrcStorage = null
                )
            {
                this.ClientId = ClientId;
                this.EntryList = EntryList;
                this.DestFolder = DestFolder;
                this.SrcFolder = SrcFolder;
                this.DestStorage = DestStorage;
                this.SrcStorage = SrcStorage;

                this.InnerList = new List<FileTransHandler>(EntryList.Count);
                for (int i = 0; i < EntryList.Count; ++i)
                {
                    this.InnerList.Add(null);
                }
            }

            private long _TotalLength = -1;
            public long TotalLength
            {
                get
                {
                    if (_TotalLength < 0)
                    {
                        _TotalLength = BaseEntry.LengthOfAllFiles(this.EntryList);
                    }
                    return _TotalLength;
                }
            }

            public FileTransHandler this[int index]
            {
                get
                {
                    FileTransHandler at = this.InnerList[index];
                    if (at == null)
                    {
                        BaseEntry Entry = EntryList[index];
                        FileTransHandler NewEntry;
                        if (Entry is FileSend.SendEntry)
                        {
                            NewEntry = new FileSend();
                        }
                        else if (Entry is FileReceive.ReceiveEntry)
                        {
                            NewEntry = new FileReceive();
                        }
                        else if (Entry is FileCopyInside.CopyInsideEntry)
                        {
                            NewEntry = new FileCopyInside();
                        }
                        else if (Entry is FileCutInside.CutInsideEntry)
                        {
                            NewEntry = new FileCutInside();
                        }
                        else
                        {
                            throw new ArgumentException("EntryList");
                        }

                        NewEntry.Initialization(ClientId, Entry, DestFolder, SrcFolder, DestStorage, SrcStorage, SharedChunkSizeCalculator);

                        this.InnerList[index] = NewEntry;
                        return NewEntry;
                    }
                    else
                    {
                        return at;
                    }
                }
                set => throw new NotImplementedException();
            }

            public int Count => this.EntryList.Count;

            public bool IsReadOnly => true;

            void ICollection<FileTransHandler>.Add(FileTransHandler item)
            {
                throw new NotImplementedException();
            }

            void ICollection<FileTransHandler>.Clear()
            {
                throw new NotImplementedException();
            }

            bool ICollection<FileTransHandler>.Contains(FileTransHandler item)
            {
                throw new NotImplementedException();
            }

            void ICollection<FileTransHandler>.CopyTo(FileTransHandler[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<FileTransHandler> GetEnumerator()
            {
                return new HandlerEnum(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            int IList<FileTransHandler>.IndexOf(FileTransHandler item)
            {
                throw new NotImplementedException();
            }

            void IList<FileTransHandler>.Insert(int index, FileTransHandler item)
            {
                throw new NotImplementedException();
            }

            bool ICollection<FileTransHandler>.Remove(FileTransHandler item)
            {
                throw new NotImplementedException();
            }

            void IList<FileTransHandler>.RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            private int _IsDisposed = 0;
            public bool IsDisposed => _IsDisposed != 0;
            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _IsDisposed, 1, 0) == 0)
                {
                    foreach (FileTransHandler Handler in this)
                    {
                        Handler.Dispose();
                    }
                }
            }
        }

        public class FileAlreadyExistEventArgs : EventArgs
        {
            public enum Measure
            {
                FILE_NOT_EXIST,
                SKIP, // No need to handle this
                OVERWRITE,
                RENAME
            }

            public readonly BaseEntry entry;

            public Measure Action;

            public FileAlreadyExistEventArgs(BaseEntry entry)
            {
                this.entry = entry;
                Action = Measure.SKIP;
            }
        }

        public class ChunkSizeCalculatorClass
        {
            public const int UnitSizeInBytes = 1024;
            public int UnitCount = 10;

            public int ChunkSize => UnitCount * UnitSizeInBytes;
        }

        public const string MSG_TYPE_FILE_EXISTS = "file_exists";
        public const string MSG_TYPE_FILE_EXISTS_BACK = "file_exists_back";
        public const string MSG_TYPE_FILE_TRANSFER_KEY_EXISTS = "file_transfer_key_exists";
        public const string MSG_TYPE_FILE_TRANSFER_KEY_EXISTS_REPLY = "file_transfer_key_exists_reply";

        protected static async Task<bool> FileExistsOnPhone(
            PhoneClient Client,
            string DestinationFolder,
            BaseEntry entry)
        {
            string Condition = await PhoneMessageCenter.Singleton.OneShotGetString(
                Client,
                new JObject()
                {
                    ["path"] = DestinationFolder + entry.ConvertedPath,
                },
                MSG_TYPE_FILE_EXISTS,
                MSG_TYPE_FILE_EXISTS_BACK,
                5000,
                "type",
                null
            );

            if (string.IsNullOrWhiteSpace(Condition))
            {
                throw new ArgumentNullException("FileExistsOnPhone");
            }

            if (Condition == "not_exist")
            {
                return false;
            }

            if (Condition == "dir" && entry.IsFolder)
            {
                return true;
            }

            if (Condition == "file" && !entry.IsFolder)
            {
                return true;
            }

            throw new ArgumentException("FileExistsOnPhone");
        }

        public class ProgressChangedEventArgs : EventArgs
        {

            public readonly long Received, Size;
            public readonly float Percent;
            public readonly double BytesPerSec;

            public ProgressChangedEventArgs(
                long Received, long Size,
                double BytesPerSec)
            {
                this.Received = Received;
                this.Size = Size;

                if (Size != 0)
                {
                    Percent = (float)((double)Received / (double)Size) * 100f;
                }
                else
                {
                    Percent = 100;
                }

                this.BytesPerSec = BytesPerSec;
            }
        }
        public delegate void PercentageChangedEventHandler(object sender, ProgressChangedEventArgs e);

        public virtual event PercentageChangedEventHandler ProgressChangedEvent;

        public ChunkSizeCalculatorClass ChunkSizeCalculator = null;

        private readonly CancellationTokenSource CancellationSource = new CancellationTokenSource();
        protected CancellationToken Cancellation => CancellationSource.Token;

        public long TransmittedLength { get; private set; } = 0;
        private SpeedWatch SpeedShortTerm = new SpeedWatch();

        public PhoneClient Client { get; protected set; }
        public BaseEntry Entry { get; private set; }

        public virtual void Initialization(
            string ClientId,
            BaseEntry Entry,
            string DestFolder = null,
            string SrcFolder = null,
            string DestStorage = null,
            string SrcStorage = null,
            ChunkSizeCalculatorClass ChunkSizeCaclulator = null)
        {
            this.Client = AlivePhones.Singleton[ClientId];
            if (this.Client == null)
            {
                throw new ArgumentException("Client " + ClientId + " not found");
            }
            this.Entry = Entry;

            this.ChunkSizeCalculator = ChunkSizeCalculator ?? new ChunkSizeCalculatorClass();
        }

        public virtual Task<bool> DetermineFileExistence()
        {
            return Task.FromResult(false);
        }

        public virtual Task Transmit(FileAlreadyExistEventArgs.Measure Measure)
        {
            if (Measure == FileAlreadyExistEventArgs.Measure.SKIP)
            {
                throw new ArgumentException("Don't pass SKIP to this method");
            }
            SpeedShortTerm.Reset();
            Watcher();

            return Task.CompletedTask;
        }

        private async void Watcher()
        {
            while (_IsDisposed == 0)
            {
                await Task.Delay(300);
                double speed = AddTransmitLength(0);
                if (speed <= 0)
                {
                    Client?.SendMsgNoThrow(PhoneClient.MSG_TYPE_HELLO);
                }
            }
        }

        private double MaxSpeed = 0.0;
        protected double AddTransmitLength(long Length)
        {
            if (Length > 0)
            {
                this.TransmittedLength += Length;
                SpeedShortTerm.Add(Length);
            }

            double speed = SpeedShortTerm.BytesPerSec(300);
            if (speed >= 0)
            {
                this.ProgressChangedEvent?.Invoke(
                    this,
                    new ProgressChangedEventArgs(
                        this.TransmittedLength,
                        this.Entry.length,
                        speed
                        )
                    );

                SpeedShortTerm.Reset();

                if (speed > MaxSpeed)
                {
                    MaxSpeed = speed;
                    ChunkSizeCalculator.UnitCount += 10;
                }
            }

            return speed;
        }

        protected void EndOneEntry()
        {
            if (!string.IsNullOrWhiteSpace(Entry.key))
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["key"] = Entry.key
                    },
                    FileReceive.MSG_TYPE_FILE_TRANSFER_END
                );
            }
        }

        private async void FreeCancellationSource()
        {
            await Task.Delay(2000);
            CancellationSource.Dispose();
        }

        private int _IsDisposed = 0;
        public bool IsDisposed => _IsDisposed != 0;
        public virtual void Dispose()
        {
            if (Interlocked.CompareExchange(ref _IsDisposed, 1, 0) == 0)
            {
                CancellationSource.Cancel();
                FreeCancellationSource();
            }
        }
    }
}

