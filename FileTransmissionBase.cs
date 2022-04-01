/*
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static FnSync.FileTransHandler;

namespace FnSync
{
    namespace FileTransmission
    {



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
            Task Init(PhoneClient client, IEnumerable<ControlFolderListItemViewBase.UiItem> UiItems, long TotalSize = -1, string FileRootOnSource = null);
            void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnSource = null);

        }

        public abstract class BaseModule<E> : IBase where E : BaseEntry, new()
        {
            public const string MSG_TYPE_FILE_EXISTS = "file_exists";
            public const string MSG_TYPE_FILE_EXISTS_BACK = "file_exists_back";

            protected static async Task<bool> FileExistsOnPhone(
                PhoneClient Client,
                string DestinationFolder,
                E entry)
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
                    throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
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

                throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
            }

            ////////////////////////////////////////////////////////////////////////////////////////

            public virtual OperationClass Operation { get; set; } = OperationClass.COPY;
            public DirectionClass Direction { get; protected set; } = DirectionClass.INSIDE_PHONE;
            public int CurrentFileIndex { get; private set; } = -1;
            protected E[] EntryList { get; set; } = null;
            public bool IsInitialzed => EntryList != null;

            protected PhoneClient Client;
            protected string FileRootOnSource;
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

            private long _CurrnetTransmittedLength = 0;
            public long CurrnetTransmittedLength => _CurrnetTransmittedLength;
            private long _TotalTransmittedLength = 0;
            public long TotalTransmittedLength => _TotalTransmittedLength;
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
                        CurrnetTransmittedLength, CurrentEntry.length,
                        TotalTransmittedLength, TotalSize,
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

                Interlocked.Add(ref _CurrnetTransmittedLength, len);
                Interlocked.Add(ref _TotalTransmittedLength, len);
                Speeder.Add(len);

                double BytesPerSec = Speeder.BytesPerSec(250);

                if (BytesPerSec >= 0)
                {
                    FireProgressChangedEvent(BytesPerSec);
                    Speeder.Reset();
                }
            }

            protected virtual void DecreaseTransmitLength(long len)
            {
                len = (-1) * len;

                Interlocked.Add(ref _CurrnetTransmittedLength, len);
                Interlocked.Add(ref _TotalTransmittedLength, len);
            }

            public async Task Init(PhoneClient client, IEnumerable<ControlFolderListItemViewBase.UiItem> UiItems, long TotalSize = -1, string FileRootOnSource = null)
                // Only for FileReceive
            {
                if (EntryList != null)
                {
                    throw new InvalidOperationException("Already Initialized");
                }

                E[] Entries = await BaseEntry.ConvertFromUiItems<E>(UiItems, FileRootOnSource, client, ListMode);
                this.Init(client, Entries, TotalSize, FileRootOnSource);
            }

            public virtual void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnSource = null)
            {
                if (EntryList != null)
                {
                    throw new InvalidOperationException("Already Initialized");
                }

                Client = client;
                this.FileRootOnSource = FileRootOnSource.AppendIfNotEnding(
                    Direction == DirectionClass.PC_TO_PHONE ? "\\" : "/"
                    );

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
                    OnReconnectedWrapper,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                    OnDisconnectedWrapper,
                    true
                );
            }

            protected abstract Task OnReconnected();

            private async void OnReconnectedWrapper(string id, string msgType, object msgObj, PhoneClient client)
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

                    await OnReconnected();
                }
            }

            protected abstract Task OnDisconnected();

            private async void OnDisconnectedWrapper(string id, string msgType, object msgObj, PhoneClient client)
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
                        Interlocked.Add(ref _TotalTransmittedLength, -CurrnetTransmittedLength);
                        Interlocked.Exchange(ref _CurrnetTransmittedLength, 0);
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

                Interlocked.Exchange(ref _CurrnetTransmittedLength, 0);
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

                await Transmit(CurrentEntry, args?.Action ?? FileAlreadyExistEventArgs.Measure.FILE_NOT_EXIST);
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
                        status = TransmissionStatus.FAILED_CONTINUE;
                        break;
                    }
                }
            }

            private object WatchJobToken = null;
            private long LastReceivedTotal = long.MinValue;
            protected virtual void WatchJob()
            {
                if (LastReceivedTotal == TotalTransmittedLength)
                {
                    Client?.SendMsgNoThrow(PhoneClient.MSG_TYPE_HELLO);
                    FireProgressChangedEvent(0);
                }
                else
                {
                    LastReceivedTotal = TotalTransmittedLength;
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
                        OnReconnectedWrapper
                    );

                    PhoneMessageCenter.Singleton.Unregister(
                        Client.Id,
                        PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                        OnDisconnectedWrapper
                    );
                }
            }
        }


        public class ChunkSizeCalculatorClass
        {
            public const int UnitSizeInBytes = 1024;
            public int UnitCount = 10;

            public int ChunkSize => UnitCount * UnitSizeInBytes;
        }
    }
}
*/
