using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FnSync.Model.ControlFolderList;

namespace FnSync.ViewModel.ControlFolderList
{
    public class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private object? selectedItem = null;
        /* Twoway binding, at WindowFileOperation.xaml */
        public object? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (!ReferenceEquals(value, selectedItem))
                {
                    selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentClient)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentRoot)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStorage)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prompt)));
                }
            }
        }

        public FileBaseModel? SelectedModel => SelectedItem as FileBaseModel;
        public string? CurrentPath => SelectedModel?.Path;
        public RootModel? CurrentRoot => SelectedModel?.Root;
        public PhoneClient? CurrentClient => CurrentRoot?.Client;
        public StorageModel? CurrentStorage => SelectedModel?.Storage;

        private static readonly string NUMBER_OF_ITEMS = (string)Application.Current.FindResource("NumberOfItems");
        public string Prompt => string.Format(
            NUMBER_OF_ITEMS,
            SelectedModel?.FolderCount ?? 0,
            SelectedModel?.FileCount ?? 0
            );

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly List<RootModel> roots = new();
        public ReadOnlyCollection<RootModel> Roots => new(roots);

        public FileOperationInsideDescriptor? SharedExecutingDescriptor = null;

        public ICommand Rename => new RenameCommand(this);
        public ICommand Delete => new DeleteCommand(this);
        public ICommand CopyToPc => new CopyToPcCommand(this);
        public ICommand Refresh => new RefreshCommand(this);
        public ICommand CutInside => new CutInsideCommand(this);
        public ICommand CopyInside => new CopyInsideCommand(this);
        public ICommand PasteHereInside => new PasteHereInsideCommand(this);
        public ICommand RefreshMediaStore => new RefreshMediaStoreCommand(this);
        public ICommand PasteHereFromPC => new PasteHereFromPCCommand(this);

        private bool disposedValue;

        private RootModel? FindClient(string? Id)
        {
            if (Id == null)
            {
                return null;
            }

            foreach (RootModel Model in roots)
            {
                if (Model.Client.Id == Id)
                {
                    return Model;
                }
            }

            return null;
        }

        public ViewModel(string? Id)
        {
            LoadPhones(AlivePhones.Singleton);

            RootModel? model = FindClient(Id);
            if (model != null)
            {
                model.IsExpanded = true;
                model.IsSelected = true;
            }
            else if (roots.Count == 1)
            {
                roots[0].IsExpanded = true;
                roots[0].IsSelected = true;
            }
        }

        public ViewModel() : this(null)
        {
        }

        private void LoadPhones(IEnumerable<PhoneClient> Clients)
        {
            roots.Clear();

            foreach (PhoneClient c in Clients)
            {
                if (c.IsAlive)
                {
                    roots.Add(new RootModel(c));
                }
            }
        }

        public void Disspose()
        {
        }

        public void RefreshCurrentSelected()
        {
            SelectedModel?.Refresh();
        }

        public void ToChildFolder(string Child)
        {
            FileBaseModel? selected = SelectedModel;
            if (selected == null)
            {
                return;
            }

            foreach (FileBaseModel child in selected.Children.Cast<FileBaseModel>())
            {
                if (child.Name == Child)
                {
                    selected.IsExpanded = true;
                    child.IsExpanded = true;
                    child.IsSelected = true;
                    break;
                }
            }
        }

        public void ToChildFolder(int Index)
        {
            FileBaseModel? selected = SelectedModel;
            if (selected == null)
            {
                return;
            }

            if (Index > selected.Children.Count)
            {
                return;
            }

            selected.IsExpanded = true;

            IFileModel Child = selected.Children[Index];
            Child.IsExpanded = true;
            Child.IsSelected = true;
        }

        public void ToParentFolder()
        {
            FileBaseModel? selected = SelectedModel;
            if (selected == null)
            {
                return;
            }

            FileBaseModel? parent = selected.Parent;

            if (parent != null)
            {
                selected.IsExpanded = false;
                parent.IsExpanded = true;
                parent.IsSelected = true;
            }
        }

        public void Dispose()
        {
            if (!disposedValue)
            {
                SelectedItem = null;

                foreach (RootModel r in roots)
                {
                    r.Dispose();
                }

                IconUtil.ClearCache();

                disposedValue = true;
            }
        }
    }

    internal class CopyToPcCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public CopyToPcCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter is not IList selected)
            {
                return false;
            }

            if (selected.Count == 0)
            {
                return false;
            }

            if (viewModel.SelectedModel is RootModel)
            {
                return false;
            }

            return true;
        }

        public void Execute(object? parameter)
        {
            if (parameter is not IList selected)
            {
                return;
            }

            IList<PhoneFileInfo> FileInfoList = selected.CloneToTypedList<PhoneFileInfo>();
            string CurrentFolder = viewModel.CurrentPath ?? throw new Exception();
            PhoneClient Client = viewModel.CurrentClient ?? throw new Exception();

            new WindowFileOperation(
                FileTransmissionAbstract.Directions.PHONE_TO_PC,
                FileTransmissionAbstract.Operations.COPY,
                Client.Id,
                null,
                CurrentFolder,
                null,
                viewModel.CurrentStorage?.Path ?? throw new Exception())
                .SetPreparation(() =>
                {
                    Task<FileReceive.ReceiveEntry[]> task =
                        FileTransmissionAbstract.BaseEntry.ConvertFromUiModels<FileReceive.ReceiveEntry>(
                            FileInfoList,
                            CurrentFolder,
                            Client,
                            FileTransmissionAbstract.ListModes.DEEP
                            );

                    task.Wait();
                    return task.Result;
                })
                .Show();
        }
    }

    public class FileOperationInsideDescriptor
    {

        public readonly string ClientId;
        public readonly IList<PhoneFileInfo> Items;
        public readonly string SourceFolder;
        public readonly FileTransmissionAbstract.Operations Operation;
        public readonly StorageModel Storage;
        public string? DestinationFolder { get; set; } = null;

        public FileOperationInsideDescriptor(
            string clientId, IList items,
            string SourceFolder, FileTransmissionAbstract.Operations operation,
            StorageModel Storage)
        {
            ClientId = clientId;
            Items = items.CloneToTypedList<PhoneFileInfo>();
            Operation = operation;
            this.SourceFolder = SourceFolder;
            this.Storage = Storage;
        }
    }

    public class FileOperationFromPcDescriptor
    {

        public readonly string ClientId;
        public readonly FileTransmissionAbstract.Operations Operation;
        public readonly StorageModel Storage;
        public readonly string DestinationFolder;

        public FileOperationFromPcDescriptor(string clientId, string dest, StorageModel Storage)
        {
            ClientId = clientId;
            Operation = FileTransmissionAbstract.Operations.COPY;
            DestinationFolder = dest;
            this.Storage = Storage;
        }
    }

    internal class PasteHereInsideCommand : ICommand
    {
        protected FileOperationInsideDescriptor? ExecutingDescriptor = null;

        protected readonly ViewModel viewModel;
        public PasteHereInsideCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public virtual bool CanExecute(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(viewModel.CurrentPath) &&
                viewModel.SharedExecutingDescriptor != null &&
                viewModel.CurrentClient?.Id == viewModel.SharedExecutingDescriptor.ClientId &&
                viewModel.CurrentStorage == viewModel.SharedExecutingDescriptor.Storage
                ;
        }

        public void Execute(object? parameter)
        {
            string? DestFolder = viewModel.CurrentPath;
            if (viewModel.SharedExecutingDescriptor != null &&
                DestFolder == viewModel.SharedExecutingDescriptor.SourceFolder)
            {
                _ = MessageBox.Show(
                    (string)Application.Current.FindResource("SameFolderCannotPaste"),
                    (string)Application.Current.FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                   );
                return;
            }

            ExecutingDescriptor = viewModel.SharedExecutingDescriptor;
            ExecutingDescriptor!.DestinationFolder = viewModel.CurrentPath;

            PhoneClient Client = viewModel.CurrentClient ?? throw new Exception();

            new WindowFileOperation(
                FileTransmissionAbstract.Directions.INSIDE_PHONE,
                ExecutingDescriptor.Operation,
                Client.Id,
                DestFolder,
                ExecutingDescriptor.SourceFolder,
                viewModel.CurrentStorage?.Path ?? throw new Exception(),
                ExecutingDescriptor.Storage.Path)
                .SetPreparation(() =>
                {
                    if (ExecutingDescriptor.Operation == FileTransmissionAbstract.Operations.CUT)
                    {
                        Task<FileCutInside.CutInsideEntry[]> task =
                            FileTransmissionAbstract.BaseEntry.ConvertFromUiModels<FileCutInside.CutInsideEntry>(
                            ExecutingDescriptor.Items,
                            ExecutingDescriptor.SourceFolder,
                            Client,
                            FileTransmissionAbstract.ListModes.PLAIN_WITHOUT_FOLDER_LENGTH
                            );

                        task.Wait();
                        return task.Result;
                    }
                    else
                    {
                        Task<FileCopyInside.CopyInsideEntry[]> task =
                            FileTransmissionAbstract.BaseEntry.ConvertFromUiModels<FileCopyInside.CopyInsideEntry>(
                            ExecutingDescriptor.Items,
                            ExecutingDescriptor.SourceFolder,
                            Client,
                            FileTransmissionAbstract.ListModes.PLAIN_WITHOUT_FOLDER_LENGTH
                            );

                        task.Wait();
                        return task.Result;
                    }
                })
                .SetOnExitEventHandler(OnExitEvent)
                .Show();
        }

        protected void OnExitEvent(object? sender, EventArgs e)
        {
            PhoneClient? client = AlivePhones.Singleton[ExecutingDescriptor?.ClientId];
            if (client != null)
            {
                PhoneMessageCenter.Singleton.Raise(
                    client.Id,
                    FileBaseModel.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                    new JObject()
                    {
                        ["folder"] = ExecutingDescriptor!.DestinationFolder,
                        ["storage"] = ExecutingDescriptor.Storage.Path
                    },
                    client
                );

                if (ExecutingDescriptor.Operation == FileTransmissionAbstract.Operations.CUT)
                {
                    PhoneMessageCenter.Singleton.Raise(
                        client.Id,
                        FileBaseModel.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                        new JObject()
                        {
                            ["folder"] = ExecutingDescriptor.SourceFolder,
                            ["storage"] = ExecutingDescriptor.Storage.Path
                        },
                        client
                    );
                }

                viewModel.SharedExecutingDescriptor = null;
                ExecutingDescriptor = null;
            }
        }
    }

    internal class RenameCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public RenameCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is PhoneFileInfo selected &&
                !string.IsNullOrWhiteSpace(viewModel.CurrentPath) &&
                (selected.type == ItemType.Directory || selected.type == ItemType.File);
        }

        private void RenameSubmit(
            object sender,
            WindowFileManagerRename.RenameSubmitEventArgs Args
            )
        {
            JObject msg = new()
            {
                ["folder"] = viewModel.CurrentPath,
                ["old"] = Args.OldName,
                ["new"] = Args.NewName,
                ["storage"] = viewModel.CurrentStorage?.Path ?? throw new Exception()
            };

            viewModel.CurrentClient?.SendMsg(msg, FileBaseModel.MSG_TYPE_FILE_RENAME);
        }

        public void Execute(object? parameter)
        {
            if (parameter is not PhoneFileInfo selected)
            {
                return;
            }

            WindowFileManagerRename dialog = new(selected.name);
            dialog.RenameSubmit += RenameSubmit;
            _ = dialog.ShowDialog();
        }
    }

    internal class DeleteCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public DeleteCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is IList selected &&
                selected.Count > 0 &&
                !string.IsNullOrWhiteSpace(viewModel.CurrentPath);
        }

        private string JoinItems(IList selected, string delimiter, int max)
        {
            StringBuilder sb = new();

            int Count = selected.Count;

            for (int i = 0; i < max && i < Count; ++i)
            {
                if (selected[i] is PhoneFileInfo item)
                {
                    sb.Append(item.name).Append(delimiter);
                }
            }

            return sb.ToString();
        }

        public void Execute(object? parameter)
        {
            if (parameter is not IList selected)
            {
                return;
            }

            int Count = selected.Count;

            string promptText =
                string.Format(
                    "{0}\n\n{1}{2}",
                    (string)Application.Current.FindResource("BeSureToDelete"),
                    JoinItems(selected, "\n", 5),
                    Count > 5 ?
                        string.Format((string)Application.Current.FindResource("AndSomeMore"), Count) :
                        ""
                    );

            if (MessageBox.Show(
                promptText,
                (string)Application.Current.FindResource("Prompt"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No
            ) == MessageBoxResult.Yes
                )
            {
                JObject msg = new JObject
                {
                    ["folder"] = viewModel.CurrentPath,
                    ["storage"] = viewModel.CurrentStorage!.Path
                };

                JArray names = new();
                foreach (object item in selected)
                {
                    if (item is PhoneFileInfo info &&
                        (info.type == ItemType.Directory || info.type == ItemType.File))
                    {
                        names.Add(info.name);
                    }
                }

                msg["names"] = names;

                viewModel.CurrentClient?.SendMsg(msg, FileBaseModel.MSG_TYPE_FILE_DELETE);
            }
        }
    }

    internal class RefreshCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public RefreshCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            viewModel.RefreshCurrentSelected();
        }
    }

    internal class CutInsideCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public CutInsideCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is IList selected &&
                selected.Count > 0 &&
                !string.IsNullOrWhiteSpace(viewModel.CurrentPath);
        }

        public void Execute(object? parameter)
        {
            if (parameter is not IList selected)
            {
                return;
            }

            viewModel.SharedExecutingDescriptor = new FileOperationInsideDescriptor(
                viewModel.CurrentClient!.Id,
                selected,
                viewModel.CurrentPath!,
                FileTransmissionAbstract.Operations.CUT,
                viewModel.CurrentStorage!
                );
        }
    }

    internal class CopyInsideCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public CopyInsideCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is IList selected &&
                selected.Count > 0 &&
                !string.IsNullOrWhiteSpace(viewModel.CurrentPath);
        }

        public void Execute(object? parameter)
        {
            if (parameter is not IList selected)
            {
                return;
            }

            viewModel.SharedExecutingDescriptor = new FileOperationInsideDescriptor(
                viewModel.CurrentClient!.Id,
                selected,
                viewModel.CurrentPath!,
                FileTransmissionAbstract.Operations.COPY,
                viewModel.CurrentStorage!
                );
        }
    }

    internal class PasteHereFromPCCommand : ICommand
    {
        protected FileOperationFromPcDescriptor? ExecutingDescriptor = null;

        private readonly ViewModel viewModel;
        public PasteHereFromPCCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public virtual bool CanExecute(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(viewModel.CurrentPath) &&
                ClipboardManager.Singleton.ContainsFileList();
        }

        public void Execute(object? parameter)
        {
            ExecutingDescriptor = new FileOperationFromPcDescriptor(
                viewModel.CurrentClient!.Id,
                viewModel.CurrentPath!,
                viewModel.CurrentStorage!
                );

            IList<string> ClipboardFileList = ClipboardManager.Singleton.GetFileList()!;
            string CommonParent = FileSend.GetCommonParentFolder(ClipboardFileList)!;

            new WindowFileOperation(
                FileTransmissionAbstract.Directions.PC_TO_PHONE,
                FileTransmissionAbstract.Operations.COPY,
                viewModel.CurrentClient.Id,
                viewModel.CurrentPath,
                CommonParent,
                viewModel.CurrentStorage!.Path,
                null)
                .SetEntryList(FileSend.ConvertToEntries(ClipboardFileList, CommonParent)!)
                .SetOnExitEventHandler(OnExitEvent)
                .Show();
        }

        protected void OnExitEvent(object? sender, EventArgs e)
        {
            PhoneClient? phoneClient = AlivePhones.Singleton[ExecutingDescriptor?.ClientId];
            if (phoneClient != null)
            {
                PhoneMessageCenter.Singleton.Raise(
                    phoneClient.Id,
                    FileBaseModel.MSG_TYPE_FOLDER_CONTENT_CHANGED,
                    new JObject()
                    {
                        ["folder"] = ExecutingDescriptor!.DestinationFolder,
                        ["storage"] = ExecutingDescriptor.Storage.Path
                    },
                    phoneClient
                );

                ExecutingDescriptor = null;
            }
        }
    }

    internal class RefreshMediaStoreCommand : ICommand
    {
        private readonly ViewModel viewModel;
        public RefreshMediaStoreCommand(ViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public event EventHandler? CanExecuteChanged
        {
            /* https://stackoverflow.com/a/33422578/1968839 */
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(viewModel.CurrentPath) &&
                parameter is IList selected &&
                selected.Count == 1 &&
                (selected[0] as PhoneFileInfo)?.type == ItemType.File
                ;
        }

        public void Execute(object? parameter)
        {
            if (!(parameter is IList selected && selected[0] is PhoneFileInfo info))
            {
                return;
            }

            viewModel.CurrentClient?.SendMsg(
                new JObject()
                {
                    ["path"] = viewModel.CurrentPath + info.path
                },
                FileBaseModel.MSG_TYPE_REFRESH_MEDIA_STORE
            );
        }
    }
}

