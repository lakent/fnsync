using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FnSync
{
    namespace FileTransmission
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
            private static async Task<IList<ControlFolderListItemView.UiItem>> ListDirFiles(PhoneClient Client, string FullPath)
            {
                JObject List = await PhoneMessageCenter.Singleton.OneShotMsgPart(
                    Client,
                    new JObject()
                    {
                        ["path"] = FullPath,
                        ["recursive"] = true
                    },
                    ControlFolderListPhoneRootItem.MSG_TYPE_LIST_FOLDER,
                    ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT,
                    60000
                );

                JArray ListPart = (JArray)List["files"];
                List<ControlFolderListItemView.UiItem> Files = ListPart.ToObject<List<ControlFolderListItemView.UiItem>>();

                return Files;
            }

            public static async Task<T[]> ConvertFromUiItems<T>(IEnumerable<ControlFolderListItemView.UiItem> UiItems, String RootOnPhone, PhoneClient Client, ListModeClass ListMode = ListModeClass.DEEP) where T : BaseEntry, new()
            {
                List<T> ResultEntries = new List<T>();

                foreach (ControlFolderListItemView.UiItem item in UiItems)
                {
                    if (item.type == "dir")
                    {
                        string FolderPathOnPhone =
                            item.path.EndsWith("/") ? item.path : item.path + '/';

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

                            IList<ControlFolderListItemView.UiItem> Files = await ListDirFiles(Client, RootOnPhone + item.path);

                            foreach (ControlFolderListItemView.UiItem i in Files)
                            {
                                if (i.type == "dir")
                                {
                                    ResultEntries.Add(new T()
                                    {
                                        key = null,
                                        length = 0,
                                        mime = null,
                                        name = i.name,
                                        path = FolderPathOnPhone + (i.path.EndsWith("/") ? i.path : i.path + '/'),
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
                            IList<ControlFolderListItemView.UiItem> Files = await ListDirFiles(Client, RootOnPhone + item.path);

                            long DirLength = 0;
                            foreach (ControlFolderListItemView.UiItem i in Files)
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

            public static long SizeOfAllFiles(IEnumerable<BaseEntry> entries)
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
            public virtual long last { get; set; } = -1;

            //
            // Summary:
            //     File path under a specific folder on phone. It's a relative path including the filename
            public virtual string path { get; set; }
            public virtual string ConvertedPath
            {
                get => path;
                protected set { }
            }

            public virtual bool IsFolder => path != null && path.EndsWith("/");
        }

        public enum TransmissionStatus
        {
            INITIAL = -1,
            SUCCESSFUL = 0,
            FAILED_CONTINUE,
            FAILED_ABORT,
            SKIPPED,
            RESET_CURRENT,
        }

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
                    Percent = (float)((double)Received / (double)Size) * 100f;
                }
                else
                {
                    Percent = 100;
                }

                if (TotalSize != 0)
                {
                    TotalPercent = (float)((double)TotalReceived / (double)TotalSize) * 100f;
                }
                else
                {
                    TotalPercent = 100;
                }

                this.BytesPerSec = BytesPerSec;
            }
        }
        public delegate void PercentageChangedEventHandler(object sender, ProgressChangedEventArgs e);

        public class NextFileEventArgs : EventArgs
        {
            public readonly int Current, Count;
            public readonly BaseEntry entry, last;
            public readonly TransmissionStatus lastStatus;

            public NextFileEventArgs(int Current, int Count, BaseEntry entry, BaseEntry last, TransmissionStatus lastStatus)
            {
                this.Current = Current;
                this.Count = Count;
                this.entry = entry;

                this.last = last;
                this.lastStatus = lastStatus;
            }
        }

        public delegate void NextFileEventHandler(object sender, NextFileEventArgs e);

        public class FileAlreadyExistEventArgs : EventArgs
        {
            public enum Measure
            {
                NONE, SKIP, OVERWRITE, RENAME
            }

            public readonly BaseEntry entry;

            public Measure Action;

            public FileAlreadyExistEventArgs(BaseEntry entry)
            {
                this.entry = entry;
                Action = Measure.SKIP;
            }
        }

        public delegate void FileAlreadyExistEventHandler(object sender, FileAlreadyExistEventArgs e);

        public interface IBase : IDisposable
        {
            OperationClass Operation { get; }
            DirectionClass Direction { get; }
            bool IsInitialzed { get; }
            event PercentageChangedEventHandler ProgressChangedEvent;
            event NextFileEventHandler OnNextFileEvent;
            event FileAlreadyExistEventHandler FileAlreadyExistEvent;
            event EventHandler OnErrorEvent;
            event EventHandler OnExitEvent;
            int FileCount { get; }
            string FirstName { get; set; }
            string DestinationFolder { get; set; }
            void StartTransmittion();
            Task Init(PhoneClient client, IEnumerable<ControlFolderListItemView.UiItem> UiItems, long TotalSize = -1, string FileRootOnPhone = null);
            void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnPhone = null);

        }

        public abstract class BaseModule<E> : IBase where E : BaseEntry, new()
        {
            public OperationClass Operation { get; set; } = OperationClass.COPY;
            public DirectionClass Direction { get; protected set; } = DirectionClass.INSIDE_PHONE;
            public int CurrentFileIndex { get; private set; } = -1;
            protected E[] EntryList { get; set; } = null;
            public bool IsInitialzed => EntryList != null;

            protected PhoneClient Client;
            protected string FileRootOnPhone;
            public string FirstName
            {
                get => EntryList[0].ConvertedName;
                set
                {
                    EntryList[0].ConvertedName = value;
                }
            }

            public int FileCount => EntryList.Length;
            protected E CurrentEntry { get; private set; } = null;

            public long CurrnetReceived { get; private set; } = 0;
            public long TotalReceived { get; private set; } = 0;
            public long TotalSize { get; protected set; }

            protected ListModeClass ListMode = ListModeClass.DEEP;

            public abstract string DestinationFolder { get; set; }

            public event NextFileEventHandler OnNextFileEvent;

            private SpeedWatch Speeder = null;

            public virtual void StartTransmittion()
            {
                Speeder = new SpeedWatch();
                StartNext(TransmissionStatus.INITIAL);
            }

            protected abstract void ResetCurrentFileTransmisionAction(E entry);
            protected abstract void FileTransmitSuccessAction(E entry);
            protected abstract void FileFailedCleanUpAction(E entry);

            protected class ExitLoop : Exception { }
            protected class ContinueLoop : Exception
            {
                public readonly TransmissionStatus LastStatus;
                public ContinueLoop(TransmissionStatus LastStatus)
                {
                    this.LastStatus = LastStatus;
                }
            }

            protected class TransmissionStatusReport : ContinueLoop
            {
                public TransmissionStatusReport(TransmissionStatus LastStatus) : base(LastStatus)
                {
                }
            }

            public event PercentageChangedEventHandler ProgressChangedEvent;

            protected void FireProgressChangedEvent(double speed)
            {
                ProgressChangedEvent?.Invoke(
                    this,
                    new ProgressChangedEventArgs(
                        CurrnetReceived, CurrentEntry.length,
                        TotalReceived, TotalSize,
                        speed
                    )
                );
            }

            public abstract event EventHandler OnErrorEvent;
            public event EventHandler OnExitEvent;

            protected virtual void AddTransmitLength(long len)
            {
                if (len < 0)
                    throw new ArgumentOutOfRangeException("len");

                CurrnetReceived += len;
                TotalReceived += len;
                Speeder.Add(len);

                double BytesPerSec = Speeder.BytesPerSec(250);

                if (BytesPerSec >= 0)
                {
                    FireProgressChangedEvent(BytesPerSec);
                    Speeder.Reset();
                }
            }

            public async Task Init(PhoneClient client, IEnumerable<ControlFolderListItemView.UiItem> UiItems, long TotalSize = -1, string FileRootOnPhone = null)
            {
                if (EntryList != null)
                {
                    throw new InvalidOperationException("Already Initialized");
                }

                E[] Entries = await BaseEntry.ConvertFromUiItems<E>(UiItems, FileRootOnPhone, client, ListMode);
                this.Init(client, Entries, TotalSize, FileRootOnPhone);
            }

            public virtual void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnPhone = null)
            {
                if (EntryList != null)
                {
                    throw new InvalidOperationException("Already Initialized");
                }

                Client = client;
                this.FileRootOnPhone = FileRootOnPhone;

                if (this.FileRootOnPhone != null && !this.FileRootOnPhone.EndsWith("/"))
                {
                    this.FileRootOnPhone += "/";
                }

                EntryList = (E[])Entries;
                if (TotalSize < 0)
                {
                    this.TotalSize = BaseEntry.SizeOfAllFiles(Entries);
                }
                else
                {
                    this.TotalSize = TotalSize;
                }

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                    OnReconnectedInner,
                    false
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                    OnDisconnectedInner,
                    false
                );

            }

            protected abstract Task OnReconnected();

            private async void OnReconnectedInner(string id, string msgType, object msgObj, PhoneClient client)
            {
                Client = client;

                if (CurrentEntry != null)
                {
                    await Task.Delay(1000);
                    if (Client != client)
                    {
                        // Phones may reconnect multiple times within this 1 second delayed above.
                        return;
                    }

                    await OnReconnected();
                }
            }

            protected abstract Task OnDisconnected();

            private async void OnDisconnectedInner(string id, string msgType, object msgObj, PhoneClient client)
            {
                if (CurrentEntry != null)
                {
                    await OnDisconnected();
                }
            }

            protected abstract Task Transmit(E entry, FileAlreadyExistEventArgs.Measure Measure);
            protected abstract Task<bool> DetermineFileExistence(E entry);

            public event FileAlreadyExistEventHandler FileAlreadyExistEvent;

            private async Task StartNextInner(TransmissionStatus LastStatus)
            {
                E LastEntry = CurrentEntry;

                switch (LastStatus)
                {
                    case TransmissionStatus.SUCCESSFUL:
                        FileTransmitSuccessAction(CurrentEntry);
                        goto case TransmissionStatus.INITIAL;

                    case TransmissionStatus.RESET_CURRENT:
                        TotalReceived -= CurrnetReceived;
                        CurrnetReceived = 0;
                        ResetCurrentFileTransmisionAction(LastEntry);
                        --CurrentFileIndex;
                        goto case TransmissionStatus.INITIAL;

                    case TransmissionStatus.FAILED_CONTINUE:
                        FileFailedCleanUpAction(LastEntry);
                        goto case TransmissionStatus.INITIAL;

                    case TransmissionStatus.FAILED_ABORT:
                        FileFailedCleanUpAction(LastEntry);
                        throw new ExitLoop();

                    case TransmissionStatus.SKIPPED:
                        goto case TransmissionStatus.INITIAL;

                    case TransmissionStatus.INITIAL:
                        ++CurrentFileIndex;
                        break;

                }

                if (CurrentFileIndex >= EntryList.Length)
                {
                    StopWatchJob();
                    OnNextFileEvent?.Invoke(
                        this,
                        new NextFileEventArgs(
                            CurrentFileIndex,
                            -1,
                            null,
                            LastEntry,
                            LastStatus)
                    );

                    OnExitEvent?.Invoke(this, null);

                    throw new ExitLoop();
                }

                CurrnetReceived = 0;
                CurrentEntry = EntryList[CurrentFileIndex];
                OnNextFileEvent?.Invoke(
                    this,
                    new NextFileEventArgs(
                        CurrentFileIndex,
                        EntryList.Length,
                        CurrentEntry,
                        LastEntry,
                        LastStatus)
                    );

                FileAlreadyExistEventArgs args = null;
                if (await DetermineFileExistence(CurrentEntry))
                {
                    args = new FileAlreadyExistEventArgs(CurrentEntry);
                    FileAlreadyExistEvent?.Invoke(this, args);

                    switch (args.Action)
                    {
                        case FileAlreadyExistEventArgs.Measure.SKIP:
                            throw new ContinueLoop(TransmissionStatus.SKIPPED);

                        case FileAlreadyExistEventArgs.Measure.OVERWRITE:
                            break;

                        case FileAlreadyExistEventArgs.Measure.RENAME:
                            break;
                    }
                }

                await Transmit(CurrentEntry, args?.Action ?? FileAlreadyExistEventArgs.Measure.NONE);
            }

            public async void StartNext(TransmissionStatus LastStatus)
            {
                TransmissionStatus status = LastStatus;
                while (true)
                {
                    try
                    {
                        await StartNextInner(status);
                        break;
                    }
                    catch (ContinueLoop e)
                    {
                        status = e.LastStatus;
                        continue;
                    }
                    catch (ExitLoop e)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        break;
                    }
                }
            }

            private object WatchJobToken = null;
            private long LastReceivedTotal = long.MinValue;
            protected virtual void WatchJob()
            {
                if (LastReceivedTotal == TotalReceived)
                {
                    Client?.SendMsgNoThrow(PhoneClient.MSG_TYPE_HELLO);
                    FireProgressChangedEvent(0);
                }
                else
                {
                    LastReceivedTotal = TotalReceived;
                }
            }

            protected virtual async void StartWatchJob(int IntervalMills = 200)
            {
                object token = new object();
                WatchJobToken = token;

                while (WatchJobToken == token)
                {
                    await Task.Delay(IntervalMills);
                    WatchJob();
                }
            }

            protected virtual void StopWatchJob()
            {
                WatchJob();
                WatchJobToken = null;
            }

            public virtual void Dispose()
            {
                StopWatchJob();

                if (Client != null)
                {
                    PhoneMessageCenter.Singleton.Unregister(
                        Client.Id,
                        PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                        OnReconnectedInner
                    );

                    PhoneMessageCenter.Singleton.Unregister(
                        Client.Id,
                        PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                        OnDisconnectedInner
                    );
                }
            }
        }
    }
}
