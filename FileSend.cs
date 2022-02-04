using FnSync.FileTransmission;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class FileSend : BaseModule<FileSend.SendEntry>
    {
        public class SendEntry : BaseEntry
        {
            public override string ConvertedPath
            {
                get => path.Replace('\\', '/');
            }

            public string RemoteName = null;
            public string RemotePath
            {
                get => RemoteName == null ? ConvertedPath :
                        Path.Combine(
                        Path.GetDirectoryName(path),
                        this.RemoteName
                        ).Replace('\\', '/');
            }

            public override bool IsFolder => path != null && path.EndsWith("\\");
        }
        public override OperationClass Operation
        {
            get
            {
                return OperationClass.COPY;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public readonly string RemoteStorage;

        public FileSend(string RemoteStorage)
        {
            Direction = DirectionClass.PC_TO_PHONE;
            this.RemoteStorage = RemoteStorage;
        }

        private enum TransmitionStageClass
        {
            NONE = 0,
            GETTING_KEY,
            ONE_CHUNK_SENDING,
            MULTIPLE_CHUNK_SENDING
        }

        public const string MSG_TYPE_FILE_CONTENT_RECEIVED = "file_content_received";
        public const string MSG_TYPE_FILE_TRANSFER_DATA_RECEIVED = "file_transfer_data_received";
        public const string MSG_TYPE_FILE_LENGTH = "file_length";
        public const string MSG_TYPE_FILE_LENGTH_RESULT = "file_length_result";
        public const string MSG_TYPE_NEW_FOLDER = "file_new_folder";
        public const string MSG_TYPE_NEW_FOLDER_CREATED = "file_new_folder_creates";


        private string DestRemoteFolder = null;
        public override string DestinationFolder
        {
            get
            {
                return DestRemoteFolder;
            }
            set
            {
                DestRemoteFolder = value.AppendIfNotEnding("/");
            }
        }

        public override event EventHandler OnErrorEvent;

        private SpeedWatch SpeedLong = null;
        private double LargestSpeed = 0;
        private ChunkSizeCalculatorClass ChunkSizeCalculator;

        private FileStream CurrentFileStream = null;

        private TransmitionStageClass TransmitionStage = TransmitionStageClass.NONE;

        public override void Init(PhoneClient client, BaseEntry[] Entries, long TotalSize = -1, string FileRootOnSource = null)
        {
            throw new NotSupportedException();
        }

        protected static BaseEntry[] ConvertToEntries(IList<string> FileList, string ParentPath)
        {
            List<SendEntry> ret = new List<SendEntry>();

            foreach (string fileRoot in FileList)
            {
                foreach (string file in Utils.TraverseFile(fileRoot))
                {
                    FileInfo info = new FileInfo(file);

                    SendEntry newEntry = new SendEntry
                    {
                        mime = "",
                        name = info.Name,
                        path = Utils.GetRelativePath(file, ParentPath),
                        length = Directory.Exists(file) ? 0 : info.Length,
                        last = ((DateTimeOffset)(info.LastWriteTime)).ToUnixTimeMilliseconds()
                    };

                    ret.Add(newEntry);
                }
            }

            return ret.ToArray();
        }

        protected string GetCommonParentFolder(IList<string> FileList)
        {
            string First = FileList.First<string>();
            string Folder = Path.GetDirectoryName(First);

            if (string.IsNullOrWhiteSpace(Folder))
            {
                return null;
            }

            foreach (string file in FileList)
            {
                string dir = Path.GetDirectoryName(First);

                if (string.IsNullOrWhiteSpace(Folder))
                {
                    return null;
                }

                if (Folder.Length < dir.Length)
                {
                    if (!dir.StartsWith(Folder))
                    {
                        return null;
                    }
                }
                else // Folder.Length >= dir.Length
                {
                    if (!Folder.StartsWith(dir))
                    {
                        return null;
                    }

                    Folder = dir;
                }
            }

            return Folder;
        }

        public void Init(PhoneClient client, IList<string> FileList)
        {
            string CommonParentFolder = GetCommonParentFolder(FileList);
            base.Init(client, ConvertToEntries(FileList, CommonParentFolder), -1, CommonParentFolder);

            ChunkSizeCalculator = new ChunkSizeCalculatorClass();

            PhoneMessageCenter.Singleton.Register(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA_RECEIVED,
                OnChunkReceivedVerified,
                false
            );
        }
        public override void StartTransmittion()
        {
            SpeedLong = new SpeedWatch();
            StartWatchJob();
            base.StartTransmittion();
        }

        protected override Task<bool> DetermineFileExistence(SendEntry entry)
        {
            // Only check files not folders.

            if (entry.IsFolder)
            {
                return Task.FromResult(false);
            }

            return FileExistsOnPhone(Client, DestinationFolder, entry);
        }

        protected override void FileFailedCleanUpAction(SendEntry entry)
        {
            /*
            JObject msg = new JObject();
            msg["folder"] = DestinationFolder;
            msg["storage"] = "";

            JArray names = new JArray();
            names.Add(entry.path);

            msg["names"] = names;

            Client.SendMsg(msg, ControlFolderListPhoneRootItem.MSG_TYPE_FILE_DELETE);
            */

            EndOneEntry(entry);
        }

        protected override void FileTransmitSuccessAction(SendEntry entry)
        {
            CurrentFileStream?.Close();
            CurrentFileStream = null;
            EndOneEntry(entry);
        }

        protected override Task OnDisconnected()
        {
            return Task.CompletedTask;
        }

        protected override async Task OnReconnected()
        {
            try
            {
                switch (TransmitionStage)
                {
                    case TransmitionStageClass.GETTING_KEY:
                    case TransmitionStageClass.ONE_CHUNK_SENDING: // No such case actually
                        StartNext(TransmissionStatus.RESET_CURRENT);
                        break;

                    case TransmitionStageClass.MULTIPLE_CHUNK_SENDING:
                        if (!String.IsNullOrWhiteSpace(CurrentEntry.key))
                        {
                            Client.SendMsg(
                                new JObject()
                                {
                                    ["key"] = CurrentEntry.key
                                },
                                FileReceive.MSG_TYPE_FILE_TRANSFER_END
                            );
                        }

                        long lenTransfered = await AcquireKey(CurrentEntry, "continue", true);

                        long diff = CurrentFileStream.Position - lenTransfered;
                        if (diff > 0)
                        {
                            DecreaseTransmitLength(diff);
                        }

                        CurrentFileStream.Position = lenTransfered;

                        MultipleChunkSend(CurrentEntry);
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

        protected override void ResetCurrentFileTransmisionAction(SendEntry entry)
        {
            // Only in case of TransmitionStageClass.GETTING_KEY:
            // Nothing to do

            // However, if you are implementing TransmitionStageClass.ONE_CHUNK_SENDING, you should do something here

            // But notice that, new folder creation uses TransmitionStageClass.ONE_CHUNK_SENDING, and there is nothing to be done here.
        }

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
                    ChunkSizeCalculator.UnitCount += 4;
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

        private void OnChunkReceivedVerified(string id, string msgType, object msgObj, PhoneClient client)
        {
            if (!(msgObj is JObject msg))
            {
                return;
            }

            if ((string)msg["key"] != CurrentEntry.key)
            {
                return;
            }

            long PreviousLength = (long)msg["length"];

            AddTransmitLength(PreviousLength);

            if (CurrentFileStream.Available() > 0)
            {
                SendChunk(CurrentEntry);
            }

            if (CurrnetTransmittedLength == CurrentEntry.length)
            {
                StartNext(TransmissionStatus.SUCCESSFUL);
            }
        }

        private void SendChunk(SendEntry entry)
        {
            long available = CurrentFileStream.Available();
            byte[] chunk = new byte[Math.Min(ChunkSizeCalculator.ChunkSize, available)];
            long read = CurrentFileStream.Read(chunk, 0, chunk.Length);
            // XXX TODO: Not thread-safe here
            if (read == 0)
            {
                return;
            }

            JObject msg = new JObject()
            {
                ["key"] = entry.key,
            };

            Client.SendMsg(
                msg,
                FileReceive.MSG_TYPE_FILE_TRANSFER_DATA,
                chunk
                );
        }

        private async Task OneChunkSend(SendEntry entry, string strategy)
        {
            byte[] binary = File.ReadAllBytes(Path.Combine(FileRootOnSource, entry.path));
            string RemotePath = DestinationFolder + entry.RemotePath;
            TransmitionStage = TransmitionStageClass.ONE_CHUNK_SENDING;
            object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                Client,
                new JObject()
                {
                    ["path"] = RemotePath,
                    ["newname"] = strategy != "overwrite",
                },
                FileReceive.MSG_TYPE_FILE_CONTENT,
                binary,
                MSG_TYPE_FILE_CONTENT_RECEIVED,
                5000
                );

            AddTransmitLength(entry.length);
        }

        private void MultipleChunkSend(SendEntry entry)
        {
            TransmitionStage = TransmitionStageClass.MULTIPLE_CHUNK_SENDING;
            for (int i = 0; i < 10; ++i)
            {
                SendChunk(entry);
            }
        }

        private async Task<long> AcquireKey(SendEntry entry, string strategy, bool Force = false)
        {
            if (String.IsNullOrWhiteSpace(entry.key) || Force)
            {
                FileRootOnSource.AssureNotEmpty();
                entry.RemotePath.AssureNotEmpty();

                TransmitionStage = TransmitionStageClass.GETTING_KEY;
                object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                    Client,
                    new JObject()
                    {
                        ["path"] = DestinationFolder + entry.RemotePath,
                        ["strategy"] = strategy,
                        ["direction"] = "to_phone",
                    },
                    FileReceive.MSG_TYPE_FILE_TRANSFER_REQUEST_KEY,
                    null,
                    FileReceive.MSG_TYPE_FILE_TRANSFER_REQUEST_KEY_OK,
                    5000
                    );

                if (!(msgObj is JObject msg))
                {
                    throw new Exception();
                }

                string key = (string)msg["key"];

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("entry.key");
                }

                entry.RemoteName = (string)msg["name"];
                entry.key = key;

                return (long)msg["length"];
            }
            else
            {
                return -1;
            }
        }

        private async Task<bool> CreateNewFolder(SendEntry entry)
        {
            if (!entry.IsFolder)
            {
                return false;
            }

            TransmitionStage = TransmitionStageClass.ONE_CHUNK_SENDING;
            object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                Client,
                new JObject()
                {
                    ["path"] = DestinationFolder + entry.RemotePath,
                },
                MSG_TYPE_NEW_FOLDER,
                null,
                MSG_TYPE_NEW_FOLDER_CREATED,
                5000
                );

            if (!(msgObj is JObject msg))
            {
                throw new Exception();
            }

            return (bool)msg["success"];
        }

        protected override async Task Transmit(SendEntry entry, FileAlreadyExistEventArgs.Measure Measure)
        {
            string ExistAction;
            if (Measure == FileAlreadyExistEventArgs.Measure.OVERWRITE)
            {
                ExistAction = "overwrite";
            }
            else if (Measure == FileAlreadyExistEventArgs.Measure.RENAME)
            {
                ExistAction = "newname";
            }
            else
            {
                ExistAction = "continue";
            }

            if (entry.IsFolder)
            {
                bool success = await CreateNewFolder(entry);
                if (success)
                {
                    throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
                }
                else
                {
                    throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
                }
            }
            else if (entry.length <= ChunkSizeCalculator.ChunkSize)
            {
                await OneChunkSend(entry, ExistAction);
                throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
            }
            else
            {
                await AcquireKey(entry, ExistAction, true);
                CurrentFileStream = File.OpenRead(Path.Combine(FileRootOnSource, entry.path));
                MultipleChunkSend(entry);
            }

            return;
        }

        private void EndOneEntry(SendEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.key))
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["key"] = entry.key
                    },
                    FileReceive.MSG_TYPE_FILE_TRANSFER_END
                );
            }
        }

        private void EndAll()
        {
            foreach (SendEntry entry in EntryList)
            {
                EndOneEntry(entry);
            }
        }

        public void DeleteCurrentFileIfNotCompleted()
        {
            if (CurrentEntry.key != null && CurrnetTransmittedLength < CurrentEntry.length)
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["paths"] = DestinationFolder + CurrentEntry.RemotePath,
                    },
                    ControlFolderListPhoneRootItem.MSG_TYPE_FILE_DELETE);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            PhoneMessageCenter.Singleton.Unregister(
                Client.Id,
                MSG_TYPE_FILE_TRANSFER_DATA_RECEIVED,
                OnChunkReceivedVerified
            );

            EndAll();

            DeleteCurrentFileIfNotCompleted();
        }
    }
}
