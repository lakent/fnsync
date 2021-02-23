﻿using FnSync.FileTransmission;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class FileCopyInside : BaseModule<FileCopyInside.CopyInsideEntry>
    {
        public class CopyInsideEntry : BaseEntry 
        {
        }

        public const string MSG_TYPE_FILE_EXISTS = "file_exists";
        public const string MSG_TYPE_FILE_EXISTS_BACK = "file_exists_back";
        public const string MSG_TYPE_FILE_MOVE = "file_move";
        public const string MSG_TYPE_FILE_COPY = "file_copy";
        public const string MSG_TYPE_COPY_DONE = "copy_done";
        public const string MSG_TYPE_MOVE_DONE = "move_done";

        private string DestPhoneFolder = "";
        public override string DestinationFolder
        {
            get => DestPhoneFolder;
            set
            {
                DestPhoneFolder = value;
                if (!DestPhoneFolder.EndsWith("/"))
                {
                    DestPhoneFolder += "/";
                }
            }
        }

        public override event EventHandler OnErrorEvent;

        public FileCopyInside()
        {
            ListMode = ListModeClass.PLAIN_WITH_FOLDER_LENGTH;
            Direction = DirectionClass.INSIDE_PHONE;
        }

        protected override void FileFailedCleanUpAction(CopyInsideEntry entry)
        {

        }

        protected override async Task<bool> DetermineFileExistence(CopyInsideEntry entry)
        {
            string Condition = await PhoneMessageCenter.Singleton.OneShotGetString(
                Client,
                new JObject()
                {
                    ["path"] = DestinationFolder + CurrentEntry.path,
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

        protected override void ResetCurrentFileTransmisionAction(CopyInsideEntry entry)
        {

        }

        protected override async Task Transmit(CopyInsideEntry entry, FileAlreadyExistEventArgs.Measure Measure)
        {
            string ExistAction;
            if(Measure == FileAlreadyExistEventArgs.Measure.OVERWRITE)
            {
                ExistAction = "overwrite";
            } else if(Measure == FileAlreadyExistEventArgs.Measure.RENAME)
            {
                ExistAction = "rename";
            } else
            {
                ExistAction = "none";
            }

            bool Done = await PhoneMessageCenter.Singleton.OneShotGetBoolean(
                Client,
                new JObject()
                {
                    ["src"] = FileRootOnPhone + CurrentEntry.path,
                    ["to"] = DestinationFolder,
                    ["exist_action"] = ExistAction
                },
                Operation == OperationClass.CUT ? MSG_TYPE_FILE_MOVE : MSG_TYPE_FILE_COPY,
                Operation == OperationClass.CUT ? MSG_TYPE_MOVE_DONE : MSG_TYPE_COPY_DONE,
                int.MaxValue,
                "success",
                false
            );

            if (!Done)
            {
                throw new TransmissionStatusReport(TransmissionStatus.FAILED_CONTINUE);
            }

            AddTransmitLength(entry.length);

            throw new TransmissionStatusReport(TransmissionStatus.SUCCESSFUL);
        }

        protected override void FileTransmitSuccessAction(CopyInsideEntry entry)
        {

        }

        public override void StartTransmittion()
        {
            StartWatchJob(1000);
            base.StartTransmittion();
        }

        protected override Task OnReconnected()
        {
            StartNext(TransmissionStatus.FAILED_CONTINUE);
            return Task.CompletedTask;
        }

        protected override Task OnDisconnected()
        {
            return Task.CompletedTask;
        }
    }
}