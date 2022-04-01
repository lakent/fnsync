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
                IList<ControlFolderListItemViewBase.UiItem> UiItemList = FileList.ListView.SelectedItems.CloneToTypedList<ControlFolderListItemViewBase.UiItem>();
                string CurrentFolder = FileList.Folder;
                PhoneClient Client = FileList.Client;

                new WindowFileOperation(
                    FileTransHandler.DirectionClass.PHONE_TO_PC,
                    FileTransHandler.OperationClass.COPY,
                    Client.Id,
                    null,
                    CurrentFolder,
                    null,
                    FileList.FolderList.CurrentStorage)
                    .SetPreparation(() =>
                    {
                        Task<FileReceive.ReceiveEntry[]> t = FileTransHandler.BaseEntry.ConvertFromUiItems<FileReceive.ReceiveEntry>(
                            UiItemList,
                       CurrentFolder,
                       Client,
                       FileTransHandler.ListModeClass.DEEP
                            );

                        t.Wait();
                        return t.Result;
                    })
                    .Show();
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
            public readonly FileTransHandler.OperationClass Operation;
            public readonly string Storage;
            public string DestinationFolder { get; set; } = null;

            public FileOperationInsideDescriptorClass(string clientId, IList items, string SourceFolder, FileTransHandler.OperationClass operation, string Storage)
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
                    FileTransHandler.OperationClass.CUT,
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
                    FileTransHandler.OperationClass.COPY,
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
                if (DestFolder == FileList.FileOperationInsideDescriptor.SourceFolder)
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
                ExecutingDescriptor.DestinationFolder = DestFolder;

                PhoneClient Client = FileList.Client;

                new WindowFileOperation(
                    FileTransHandler.DirectionClass.INSIDE_PHONE,
                    ExecutingDescriptor.Operation,
                    Client.Id,
                    DestFolder,
                    ExecutingDescriptor.SourceFolder,
                    FileList.FolderList.CurrentStorage,
                    ExecutingDescriptor.Storage)
                    .SetPreparation(() =>
                    {
                        if (ExecutingDescriptor.Operation == FileTransHandler.OperationClass.CUT)
                        {
                            Task<FileCutInside.CutInsideEntry[]> t = FileTransHandler.BaseEntry.ConvertFromUiItems<FileCutInside.CutInsideEntry>(
                          ExecutingDescriptor.Items,
                          ExecutingDescriptor.SourceFolder,
                          Client,
                          FileTransHandler.ListModeClass.PLAIN_WITHOUT_FOLDER_LENGTH
                               );
                            t.Wait();
                            return t.Result;
                        } else
                        {
                            Task<FileCopyInside.CopyInsideEntry[]> t = FileTransHandler.BaseEntry.ConvertFromUiItems<FileCopyInside.CopyInsideEntry>(
                          ExecutingDescriptor.Items,
                          ExecutingDescriptor.SourceFolder,
                          Client,
                          FileTransHandler.ListModeClass.PLAIN_WITHOUT_FOLDER_LENGTH
                               );
                            t.Wait();
                            return t.Result;
                        }
                    })
                    .SetOnExitEventHandler(OnExitEvent)
                    .Show();
            }

            protected void OnExitEvent(object sender, EventArgs e)
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

                    if (ExecutingDescriptor.Operation == FileTransHandler.OperationClass.CUT)
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
                    }

                    FileList.FileOperationInsideDescriptor = null;
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
                    FileTransHandler.OperationClass.COPY,
                    this.FileList.FolderList.CurrentStorage
                    );
                ExecutingDescriptor.DestinationFolder = this.FileList.Folder;

                IList<string> ClipboardFileList = ClipboardManager.Singleton.GetFileList();
                string CommonParent = FileSend.GetCommonParentFolder(ClipboardFileList);

                new WindowFileOperation(
                    FileTransHandler.DirectionClass.PC_TO_PHONE,
                    FileTransHandler.OperationClass.COPY,
                    FileList.Client.Id,
                    FileList.Folder,
                    CommonParent,
                    FileList.FolderList.CurrentStorage,
                    null)
                    .SetEntryList(FileSend.ConvertToEntries(ClipboardFileList, CommonParent))
                    .SetOnExitEventHandler(OnExitEvent)
                    .Show();
            }

            protected void OnExitEvent(object sender, EventArgs e)
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
