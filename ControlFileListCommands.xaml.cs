using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static FnSync.WindowFileManagerRename;

namespace FnSync
{
    public partial class ControlFileList : UserControlExtension
    {
        public const string MSG_TYPE_REFRESH_MEDIA_STORE = "refresh_media_store";

        private class RenameCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public RenameCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItems.Count == 1 && !string.IsNullOrWhiteSpace(FileList.Folder) && FileList.ListView.SelectedItem is ControlFolderListItemViewBase.UiItem item && (item.type == "dir" || item.type == "file");
            }

            public void RenameSubmit(object sender, RenameSubmitEventArgs Args)
            {
                JObject msg = new JObject()
                {
                    ["folder"] = FileList.Folder,
                    ["old"] = Args.OldName,
                    ["new"] = Args.NewName,
                    ["storage"] = FileList.FolderList.CurrentStorage
                };

                FileList.Client.SendMsg(msg, ControlFolderListPhoneRootItem.MSG_TYPE_FILE_RENAME);
            }

            public void Execute(object parameter)
            {
                if (!(FileList.ListView.SelectedItem is ControlFolderListItemViewBase.UiItem item))
                    return;


                WindowFileManagerRename dialog = new WindowFileManagerRename(item.name);
                dialog.RenameSubmit += RenameSubmit;

                dialog.ShowDialog();
            }
        }

        private class DeleteCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public DeleteCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItem != null && !string.IsNullOrWhiteSpace(FileList.Folder);
            }

            private string JoinItems(string delimiter, int max)
            {
                StringBuilder sb = new StringBuilder();

                int Count = FileList.ListView.SelectedItems.Count;

                for (int i = 0; i < max && i < Count; ++i)
                {
                    if (FileList.ListView.SelectedItems[i] is ControlFolderListItemViewBase.UiItem item)
                    {
                        sb.Append(item.name).Append(delimiter);
                    }
                }

                return sb.ToString();
            }

            public void Execute(object parameter)
            {
                int Count = FileList.ListView.SelectedItems.Count;

                if (MessageBox.Show(
                    string.Format(
                        "{0}\n\n{1}{2}",
                        (string)App.Current.FindResource("BeSureToDelete"),
                        JoinItems("\n", 5),
                        Count > 5 ? string.Format((string)App.Current.FindResource("AndSomeMore"), Count) : ""
                        ),
                    (string)App.Current.FindResource("Prompt"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                ) == MessageBoxResult.Yes
                    )
                {
                    JObject msg = new JObject();
                    msg["folder"] = FileList.Folder;
                    msg["storage"] = FileList.FolderList.CurrentStorage;

                    JArray names = new JArray();
                    foreach (object obj in FileList.ListView.SelectedItems)
                    {
                        if (obj is ControlFolderListItemViewBase.UiItem item && (item.type == "dir" || item.type == "file"))
                        {
                            names.Add(item.name);
                        }
                    }

                    msg["names"] = names;

                    FileList.Client.SendMsg(msg, ControlFolderListPhoneRootItem.MSG_TYPE_FILE_DELETE);
                }
            }
        }

        private class CopyToPcCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public CopyToPcCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItem != null && !string.IsNullOrWhiteSpace(FileList.Folder);
            }

            public void Execute(object parameter)
            {
                new WindowFileOperation(
                    new FileReceive(),
                    FileList.Client,
                    FileList.ListView.SelectedItems.CloneToTypedList<ControlFolderListItemViewBase.UiItem>(),
                    FileList.Folder
                    ).Show();
            }
        }

        private class RefreshCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public RefreshCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                FileList.FolderList.RefreshCurrentSelected();
            }
        }

        private class FileOperationInsideDescriptorClass
        {

            public readonly string ClientId;
            public readonly IList<ControlFolderListItemViewBase.UiItem> Items;
            public readonly string SourceFolder;
            public readonly FileTransmission.OperationClass Operation;
            public readonly string Storage;
            public string DestinationFolder { get; set; } = null;

            public FileOperationInsideDescriptorClass(string clientId, IList items, string SourceFolder, FileTransmission.OperationClass operation, string Storage)
            {
                ClientId = clientId;
                Items = items.CloneToTypedList<ControlFolderListItemViewBase.UiItem>();
                Operation = operation;
                this.SourceFolder = SourceFolder;
                this.Storage = Storage;
            }
        }

        private FileOperationInsideDescriptorClass FileOperationInsideDescriptor = null;

        private class CutInsideCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public CutInsideCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItem != null && !string.IsNullOrWhiteSpace(FileList.Folder);
            }

            public void Execute(object parameter)
            {
                FileList.FileOperationInsideDescriptor = new FileOperationInsideDescriptorClass(
                    FileList.Client.Id,
                    FileList.ListView.SelectedItems,
                    FileList.Folder,
                    FileTransmission.OperationClass.CUT,
                    FileList.FolderList.CurrentStorage
                    );
            }
        }

        private class CopyInsideCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public CopyInsideCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItem != null && !string.IsNullOrWhiteSpace(FileList.Folder);
            }

            public void Execute(object parameter)
            {
                FileList.FileOperationInsideDescriptor = new FileOperationInsideDescriptorClass(
                    FileList.Client.Id,
                    FileList.ListView.SelectedItems,
                    FileList.Folder,
                    FileTransmission.OperationClass.COPY,
                    FileList.FolderList.CurrentStorage
                    );
            }
        }

        private class PasteHereInsideCommandClass : ICommand
        {
            protected readonly ControlFileList FileList;
            protected FileOperationInsideDescriptorClass ExecutingDescriptor = null;

            protected virtual string DestFolder => FileList.Folder;

            public PasteHereInsideCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public virtual bool CanExecute(object parameter)
            {
                return !string.IsNullOrWhiteSpace(FileList.Folder) &&
                    FileList.FileOperationInsideDescriptor != null &&
                    FileList.Client.Id == FileList.FileOperationInsideDescriptor.ClientId &&
                    FileList.FolderList.CurrentStorage == FileList.FileOperationInsideDescriptor.Storage
                    ;
            }

            public void Execute(object parameter)
            {
                if(DestFolder == FileList.FileOperationInsideDescriptor.SourceFolder)
                {
                    MessageBox.Show(
                        (string)App.Current.FindResource("SameFolder"),
                        (string)App.Current.FindResource("Prompt"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                       );
                    return;
                }

                ExecutingDescriptor = FileList.FileOperationInsideDescriptor;
                FileCopyInside fileCopyInside = new FileCopyInside();
                fileCopyInside.Operation = ExecutingDescriptor.Operation;
                fileCopyInside.OnExitEvent += FileCopyInside_OnExitEvent;
                ExecutingDescriptor.DestinationFolder = DestFolder;

                new WindowFileOperation(
                    fileCopyInside,
                    FileList.Client,
                    FileList.FileOperationInsideDescriptor.Items,
                    FileList.FileOperationInsideDescriptor.SourceFolder,
                    ExecutingDescriptor.DestinationFolder
                ).Show();
            }

            protected void FileCopyInside_OnExitEvent(object sender, EventArgs e)
            {
                PhoneClient phoneClient = AlivePhones.Singleton[ExecutingDescriptor?.ClientId];
                if (phoneClient != null)
                {
                    PhoneMessageCenter.Singleton.Raise(
                        phoneClient.Id,
                        ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                        new JObject()
                        {
                            ["folder"] = ExecutingDescriptor.DestinationFolder,
                            ["storage"] = ExecutingDescriptor.Storage
                        },
                        phoneClient
                    );

                    if (ExecutingDescriptor.Operation == FileTransmission.OperationClass.CUT)
                    {
                        PhoneMessageCenter.Singleton.Raise(
                            phoneClient.Id,
                            ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                            new JObject()
                            {
                                ["folder"] = ExecutingDescriptor.SourceFolder,
                                ["storage"] = ExecutingDescriptor.Storage
                            },
                            phoneClient
                        );

                        FileList.FileOperationInsideDescriptor = null;
                    }

                    ExecutingDescriptor = null;
                }
            }
        }

        private class PasteHereFromPCCommandClass : ICommand
        {
            protected readonly ControlFileList FileList;
            protected FileOperationInsideDescriptorClass ExecutingDescriptor = null;

            protected virtual string DestFolder => FileList.Folder;

            public PasteHereFromPCCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public virtual bool CanExecute(object parameter)
            {
                return !string.IsNullOrWhiteSpace(FileList.Folder) &&
                    ClipboardManager.Singleton.ContainsFileList();
            }

            public void Execute(object parameter)
            {
                ExecutingDescriptor = new FileOperationInsideDescriptorClass(
                    this.FileList.Client.Id,
                    null,
                    null,
                    FileTransmission.OperationClass.COPY,
                    this.FileList.FolderList.CurrentStorage
                    );
                ExecutingDescriptor.DestinationFolder = this.FileList.Folder;

                FileSend fileSend = new FileSend(ExecutingDescriptor.Storage);
                fileSend.OnExitEvent += FileCopyInside_OnExitEvent;
                fileSend.Init(this.FileList.Client, ClipboardManager.Singleton.GetFileList());

                new WindowFileOperation(
                    fileSend,
                    ExecutingDescriptor.DestinationFolder
                ).Show();
            }

            protected void FileCopyInside_OnExitEvent(object sender, EventArgs e)
            {
                PhoneClient phoneClient = AlivePhones.Singleton[ExecutingDescriptor.ClientId];
                if (phoneClient != null)
                {
                    PhoneMessageCenter.Singleton.Raise(
                        phoneClient.Id,
                        ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                        new JObject()
                        {
                            ["folder"] = ExecutingDescriptor.DestinationFolder,
                            ["storage"] = ExecutingDescriptor.Storage
                        },
                        phoneClient
                    );

                    ExecutingDescriptor = null;
                }
            }
        }

        private class PasteToInsideCommandClass : PasteHereInsideCommandClass
        {
            protected override string DestFolder => FileList.Folder + (FileList.ListView.SelectedItem as ControlFolderListItemViewBase.UiItem).path;

            public PasteToInsideCommandClass(ControlFileList FileList) : base(FileList)
            {

            }

            public override bool CanExecute(object parameter)
            {
                return !string.IsNullOrWhiteSpace(FileList.Folder) &&
                    FileList.FileOperationInsideDescriptor != null &&
                    FileList.ListView.SelectedItems.Count == 1 &&
                    (FileList.ListView.SelectedItem as ControlFolderListItemViewBase.UiItem)?.type == "dir" &&
                    FileList.Client.Id == FileList.FileOperationInsideDescriptor.ClientId && 
                    FileList.FolderList.CurrentStorage == FileList.FileOperationInsideDescriptor.Storage
                    ;
            }
        }

        private class RefreshMediaStoreCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public RefreshMediaStoreCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return !string.IsNullOrWhiteSpace(FileList.Folder) &&
                    FileList.ListView.SelectedItems.Count == 1 &&
                    (FileList.ListView.SelectedItem as ControlFolderListItemViewBase.UiItem)?.type == "file"
                    ;
            }

            public void Execute(object parameter)
            {
                ControlFolderListItemViewBase.UiItem item = FileList.ListView.SelectedItem as ControlFolderListItemViewBase.UiItem;

                FileList.Client.SendMsg(
                    new JObject()
                    {
                        ["path"] = FileList.Folder + item.path
                    },
                    MSG_TYPE_REFRESH_MEDIA_STORE
                );
            }
        }
    }
}
