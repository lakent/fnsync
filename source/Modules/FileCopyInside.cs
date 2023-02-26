using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class FileCopyInside : FileTransmissionAbstract
    {
        public class CopyInsideEntry : BaseEntry { }

        public const string MSG_TYPE_FILE_MOVE = "file_move";
        public const string MSG_TYPE_FILE_COPY = "file_copy";
        public const string MSG_TYPE_COPY_DONE = "copy_done";
        public const string MSG_TYPE_MOVE_DONE = "move_done";

        public string DestinationFolder { get; private set; } = null!;
        public string FileRootOnSource { get; private set; } = null!;

        public Operations Operation{ get; protected set; }

        public FileCopyInside()
        {
            Operation = Operations.COPY;
        }

        public override void Initialization(string ClientId, BaseEntry Entry, string DestFolder, string? SrcFolder,
            string? DestStorage = null, string? SrcStorage = null,
            ChunkSizeCalculatorClass? ChunkSizeCaclulator = null)
        {
            base.Initialization(ClientId, Entry, DestFolder, SrcFolder, DestStorage, SrcStorage, ChunkSizeCaclulator);
            this.DestinationFolder = DestFolder.AppendIfNotEnding("/");
            this.FileRootOnSource = SrcFolder!;
        }

        public override Task<bool> DetermineFileExistence()
        {
            return FileExistsOnPhone(Client, DestinationFolder, this.Entry);
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
                ExistAction = "rename";
            }
            else
            {
                ExistAction = "none";
            }

            while (true) try
                {
                    bool Done = await PhoneMessageCenter.Singleton.OneShotGetBoolean(
                        Client!,
                        new JObject()
                        {
                            ["src"] = FileRootOnSource + this.Entry.path,
                            ["to"] = DestinationFolder,
                            ["exist_action"] = ExistAction
                        },
                        Operation == Operations.CUT ? MSG_TYPE_FILE_MOVE : MSG_TYPE_FILE_COPY,
                        Operation == Operations.CUT ? MSG_TYPE_MOVE_DONE : MSG_TYPE_COPY_DONE,
                        int.MaxValue,
                        "success",
                        false
                    );

                    if (!Done)
                    {
                        throw new FileTransException(this.Entry.path!);
                    }

                    AddTransmitLength(this.Entry.length);

                    return;
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {
                    this.Client = await PhoneMessageCenter.Singleton.WaitOnline(this.Client!.Id, int.MaxValue, Cancellation);
                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    this.Client = e.Current ?? throw new Exception();
                }
        }
    }
}
