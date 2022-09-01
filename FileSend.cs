using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FnSync.Model.ControlFolderList;
using FileInfo = System.IO.FileInfo;

namespace FnSync
{
    class FileSend : FileTransmissionAbstract 
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

        public const string MSG_TYPE_FILE_CONTENT_RECEIVED = "file_content_received";
        public const string MSG_TYPE_FILE_TRANSFER_DATA_RECEIVED = "file_transfer_data_received";
        public const string MSG_TYPE_FILE_LENGTH = "file_length";
        public const string MSG_TYPE_FILE_LENGTH_RESULT = "file_length_result";
        public const string MSG_TYPE_NEW_FOLDER = "file_new_folder";
        public const string MSG_TYPE_NEW_FOLDER_CREATED = "file_new_folder_creates";

        public static BaseEntry[] ConvertToEntries(IList<string> FileList, string ParentPath = null)
        {
            List<SendEntry> ret = new List<SendEntry>();

            ParentPath = ParentPath ?? GetCommonParentFolder(FileList);

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

        public static string GetCommonParentFolder(IList<string> FileList)
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

        public new SendEntry Entry => base.Entry as SendEntry;

        private string DestinationFolder = null;
        private string SourceFolder = null;
        public string RemoteStorage { get; private set; }

        private FileStream CurrentFileStream = null;

        public override void Initialization(string ClientId, BaseEntry Entry, string DestFolder = null, string SrcFolder = null,
            string DestStorage = null, string SrcStorage = null,
            ChunkSizeCalculatorClass ChunkSizeCaclulator = null)
        {
            base.Initialization(ClientId, Entry, DestFolder, SrcFolder, DestStorage, SrcStorage, ChunkSizeCaclulator);
            if(!(Entry is SendEntry))
            {
                throw new ArgumentException("Entry");
            }
            this.RemoteStorage = DestStorage;
            this.SourceFolder = SrcFolder.AppendIfNotEnding("/");
            this.DestinationFolder = DestFolder.AppendIfNotEnding("/");
            if (!Entry.IsFolder)
            {
                CurrentFileStream = File.OpenRead(Path.Combine(SrcFolder, Entry.path));
            }
        }

        public override Task<bool> DetermineFileExistence()
        {
            // Only check files not folders.

            if (this.Entry.IsFolder)
            {
                return Task.FromResult(false);
            }

            return FileExistsOnPhone(this.Client, DestinationFolder, this.Entry);
        }

        public override async Task Transmit(FileAlreadyExistEventArgs.Measure Measure)
        {
            await base.Transmit(Measure);

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

            if (Entry.IsFolder)
            {
                bool success = await CreateNewFolder();
                if (success)
                {
                    return;
                }
                else
                {
                    throw new FileTransException("Failed");
                }
            }
            else if (Entry.length <= ChunkSizeCalculator.ChunkSize)
            {
                await OneChunkSend(ExistAction);
            }
            else
            {
                await MultipleChunkSend(ExistAction);
            }

            return;
        }

        /*
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
                    / *
                    if(UnitCoefficient > 10)
                    {
                        UnitCoefficient -= 1;
                    }
                    * /
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
*/
        private async Task SendChunk()
        {
            for (long available = CurrentFileStream.Available(); available > 0; available = CurrentFileStream.Available())
            {
                byte[] chunk = new byte[Math.Min(ChunkSizeCalculator.ChunkSize * 10, available)];
                long read = await CurrentFileStream.ReadAsync(chunk, 0, chunk.Length);

                if (read == 0)
                {
                    return;
                }
                else if (read < chunk.Length)
                {
                    Array.Resize(ref chunk, (int)read);
                }

                JObject msg = new JObject()
                {
                    ["key"] = Entry.key,
                };

                object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                    Client,
                    msg,
                    FileReceive.MSG_TYPE_FILE_TRANSFER_DATA,
                    chunk,
                    MSG_TYPE_FILE_TRANSFER_DATA_RECEIVED,
                    60000
                    );

                AddTransmitLength(chunk.Length);
            }
        }

        private async Task OneChunkSend(string strategy)
        {
            byte[] binary = File.ReadAllBytes(Path.Combine(this.SourceFolder, Entry.path));
            string RemotePath = DestinationFolder + Entry.RemotePath;

            while (true) try
                {
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

                    AddTransmitLength(Entry.length);

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

        private async Task MultipleChunkSend(string ExistAction)
        {
            while (true) try
                {
                    if (string.IsNullOrEmpty(Entry.key))
                    {
                        await AcquireKey(ExistAction);
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
                            long pos = await AcquireKey("continue");
                            CurrentFileStream.Position = pos;
                        }
                    }

                    await SendChunk();

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

        private async Task<long> AcquireKey(string strategy)
        {
            SourceFolder.AssureNotEmpty();
            Entry.RemotePath.AssureNotEmpty();

            object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                Client,
                new JObject()
                {
                    ["path"] = DestinationFolder + Entry.RemotePath,
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

            Entry.RemoteName = (string)msg["name"];
            Entry.key = key;

            return (long)msg["length"];
        }

        private async Task<bool> CreateNewFolder()
        {
            if (!Entry.IsFolder)
            {
                return false;
            }

            while (true) try
                {
                    object msgObj = await PhoneMessageCenter.Singleton.OneShot(
                        Client,
                        new JObject()
                        {
                            ["path"] = DestinationFolder + Entry.RemotePath,
                        },
                        MSG_TYPE_NEW_FOLDER,
                        null,
                        MSG_TYPE_NEW_FOLDER_CREATED,
                        5000
                        );

                    if (!(msgObj is JObject msg))
                    {
                        throw new FileTransException(Entry.path);
                    }

                    return (bool)msg["success"];
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


        public void DeleteCurrentFileIfNotCompleted()
        {
            if (Entry.key != null && TransmittedLength < Entry.length)
            {
                Client.SendMsg(
                    new JObject()
                    {
                        ["paths"] = DestinationFolder + Entry.RemotePath,
                    },
                    FileBaseModel.MSG_TYPE_FILE_DELETE);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            CurrentFileStream?.Close();
            EndOneEntry();
            DeleteCurrentFileIfNotCompleted();
        }
    }
}
