using FnSync.FileTransmission;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class FileSend : BaseModule<FileSend.SendEntry>
    {
        public class SendEntry : BaseEntry
        {
        }

        private string DestRemoteFolder = null;
        public override string DestinationFolder
        {
            get{
                return DestRemoteFolder;
            }
            set {
                DestRemoteFolder = value;
                if (!DestRemoteFolder.EndsWith("/"))
                {
                    DestRemoteFolder += '/';
                }
            }
        }

        public override event EventHandler OnErrorEvent;

        protected override Task<bool> DetermineFileExistence(SendEntry entry)
        {
            throw new NotImplementedException();
        }

        protected override void FileFailedCleanUpAction(SendEntry entry)
        {
            throw new NotImplementedException();
        }

        protected override void FileTransmitSuccessAction(SendEntry entry)
        {
            throw new NotImplementedException();
        }

        protected override Task OnDisconnected()
        {
            throw new NotImplementedException();
        }

        protected override Task OnReconnected()
        {
            throw new NotImplementedException();
        }

        protected override void ResetCurrentFileTransmisionAction(SendEntry entry)
        {
            throw new NotImplementedException();
        }

        protected override Task Transmit(SendEntry entry, FileAlreadyExistEventArgs.Measure Measure)
        {
            throw new NotImplementedException();
        }
    }
}
