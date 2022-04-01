using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using static FnSync.FileTransHandler;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileReceive.xaml
    /// </summary>
    public partial class WindowFileOperation : Window
    {
        private bool PromptOnClose = false;
        private bool CloseAutomatically = true;
        private bool IsClosing = false;

        public IList<BaseEntry> EntryList = null;
        public Func<IList<BaseEntry>> PrepareEntryList { get; private set; } = null;
        private FileTransHandler.HandlerList HandlerList = null;

        public readonly DirectionClass Direction;

        public readonly string ClientId = null;
        public string DestFolder { get; private set; } = null;
        public readonly string SrcFolder = null;
        public readonly string DestStorage = null;
        public readonly string SrcStorage = null;

        private long FinishedLength = 0;

        private event EventHandler OnExitEvent;

        public WindowFileOperation(
            DirectionClass Direction,
            OperationClass Operation,
            string ClientId,
            string DestFolder = null,
            string SrcFolder = null,
            string DestStorage = null,
            string SrcStorage = null
            )
        {
            InitializeComponent();
            SetTitle(Direction, Operation);
            this.ContentRendered += OnWindowRendered;

            this.Direction = Direction;

            this.ClientId = ClientId;
            this.DestFolder = DestFolder;
            this.SrcFolder = SrcFolder;
            this.DestStorage = DestStorage;
            this.SrcStorage = SrcStorage;
        }

        public WindowFileOperation SetEntryList(IList<BaseEntry> EntryList)
        {
            this.EntryList = EntryList;
            return this;
        }

        public WindowFileOperation SetPreparation(Func<IList<BaseEntry>> Action)
        {
            this.PrepareEntryList = Action;
            return this;
        }

        public WindowFileOperation SetOnExitEventHandler(EventHandler Handler)
        {
            this.OnExitEvent += Handler;
            return this;
        }

        private void SetTitle(DirectionClass Direction, OperationClass Operation)
        {
            string PatternString;
            switch (Direction)
            {
                case DirectionClass.INSIDE_PHONE:
                    PatternString = (string)FindResource("FileOperation_InsidePhone");
                    break;

                case DirectionClass.PC_TO_PHONE:
                    PatternString = (string)FindResource("FileOperation_PcToPhone");
                    break;

                case DirectionClass.PHONE_TO_PC:
                    PatternString = (string)FindResource("FileOperation_PhoneToPc");
                    break;

                default:
                    return;
            }

            string OperationString;
            switch (Operation)
            {
                case OperationClass.COPY:
                    OperationString = (string)FindResource("Copy");
                    break;

                case OperationClass.CUT:
                    OperationString = (string)FindResource("Cut");
                    break;

                default:
                    return;
            }

            Title = string.Format(PatternString, OperationString);
        }

        private async void OnWindowRendered(object sender, EventArgs e)
        {
            this.TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
            if (PrepareEntryList != null)
            {
                PercentTotal.IsIndeterminate = true;
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                EntryList = await Task.Run(this.PrepareEntryList);
                this.PrepareEntryList = null;
            }

            if (EntryList == null)
            {
                throw new ArgumentNullException("EntryList");
            }

            if (this.DestFolder == null && this.Direction == DirectionClass.PHONE_TO_PC)
            {
                if (this.EntryList.Count == 1)
                {
                    string FirstName = this.EntryList[0].ConvertedName;
                    string ext = Path.GetExtension(FirstName);

                    SaveFileDialog dlg = new SaveFileDialog
                    {
                        FileName = FirstName, // Default file name
                        DefaultExt = ext ?? "", // Default file extension
                        Filter = (ext != null ? $"*{ext}|*{ext}|" : "") + "*|*",
                        OverwritePrompt = true
                    };

                    bool? result = dlg.ShowDialog();

                    if (result == true)
                    {
                        string FileName = dlg.FileName;
                        this.DestFolder = Path.GetDirectoryName(FileName).AppendIfNotEnding("\\");
                        this.EntryList[0].ConvertedName = Path.GetFileName(FileName);

                        PromptOnClose = true;

                        FileExistsAction = new WindowFileAlreadyExists.ExistAction(FileAlreadyExistEventArgs.Measure.OVERWRITE, true);
                    }
                    else
                    {
                        Close();
                        return;
                    }
                }
                else
                {
                    VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
                    {
                        Description = (string)FindResource("SaveTo"),
                        UseDescriptionForTitle = true
                    };

                    if ((bool)dialog.ShowDialog(this))
                    {
                        this.DestFolder = dialog.SelectedPath.AppendIfNotEnding("\\");
                        PromptOnClose = true;
                    }
                    else
                    {
                        Close();
                        return;
                    }
                }
            }
            else
            {
            }

            HandlerList = new FileTransHandler.HandlerList(this.EntryList, ClientId, DestFolder, SrcFolder, DestStorage, SrcStorage);

            StartTramsmission();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        /*
        public WindowFileOperation(IBase InitializedTransmission, string DestFolder = null) : this(InitializedTransmission, null, null, null, DestFolder)
        { }

        public WindowFileOperation(IBase Transmission, PhoneClient Client, IList<ControlFolderListItemViewBase.UiItem> UiItems, string RootFolderOnSource, string DestFolder = null) : this()
        {
            this.Transmission = Transmission;
            this.DestFolder = DestFolder;
            this.Client = Client;
            this.PendingItems = UiItems;
            this.SrcFolder = RootFolderOnSource;
        }
        */

        /*
        public WindowFileOperation(BaseModule<BaseEntry> Transmission, PhoneClient Client, IList items, string RootOnPhone, string DestFolder = null) : this(Transmission, Client, items.CloneToTypedList<ControlFolderListItemViewBase.UiItem>(), RootOnPhone, DestFolder)
        { }
        */

        private static readonly string FAILED = (string)App.Current.FindResource("Failed");
        private static readonly string SKIPPED = (string)App.Current.FindResource("Skipped");
        private static readonly string SUCCEED = (string)App.Current.FindResource("Succeed");

        private readonly StringBuilder LogBuffer = new StringBuilder();

        private void FileFailed()
        {
            CloseAutomatically = false;
            LogBuffer.Append(FAILED);
            LogBuffer.Append("\n");
        }

        private void FileSkipped()
        {
            LogBuffer.Append(SKIPPED);
            LogBuffer.Append("\n");
        }

        private void FileSuccess()
        {
            LogBuffer.Append(SUCCEED);
            LogBuffer.Append("\n");
        }

        private void FileStart(BaseEntry Entry)
        {
            LogBuffer.Append(Entry.ConvertedPath);
            LogBuffer.Append(" … ");
        }

        private void OnErrorHandler(object sender, EventArgs e)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    (string)FindResource("TransmitionFailed"),
                    (string)FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                    );

                PromptOnClose = false;
                Close();
                return null;
            });
        }

        private WindowFileAlreadyExists.ExistAction FileExistsAction = null;

        private FileAlreadyExistEventArgs.Measure FileAlreadyExist(BaseEntry Entry)
        {
            if (FileExistsAction?.ApplyToAll == true)
            {
                return FileExistsAction.Action;
            }
            else
            {
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
                WindowFileAlreadyExists window = new WindowFileAlreadyExists(Entry.ConvertedName);
                window.ActionChanged += ActionChangedEventHandler;
                window.ShowDialog();
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

                return FileExistsAction.Action;
            }
        }

        private void ActionChangedEventHandler(object sender, WindowFileAlreadyExists.ExistAction e)
        {
            FileExistsAction = e;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            IsClosing = true;
            if (PromptOnClose && MessageBox.Show(
                    (string)FindResource("CancelFileTransferPrompt"),
                    (string)FindResource("Cancelling"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                ) != MessageBoxResult.Yes
                )
            {
                e.Cancel = true;
                IsClosing = false;
                return;
            }

            this.HandlerList?.Dispose();
            this.OnExitEvent?.Invoke(this, null);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void StartTramsmission()
        {
            PercentTotal.IsIndeterminate = false;
            this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            FilesTotal.Content = HandlerList.Count.ToString();

            int i = 0;
            foreach (FileTransHandler Handler in this.HandlerList)
            {
                if (this.HandlerList.IsDisposed)
                {
                    break;
                }

                if (Handler.IsDisposed)
                {
                    continue;
                }

                Handler.ProgressChangedEvent += Handler_ProgressChangedEvent;
                CurrentFile.Text = Handler.Entry.ConvertedName;

                try
                {
                    FileStart(Handler.Entry);
                    FileAlreadyExistEventArgs.Measure Measure = FileAlreadyExistEventArgs.Measure.OVERWRITE;
                    if (await Handler.DetermineFileExistence())
                    {
                        Measure = FileAlreadyExist(Handler.Entry);
                    }

                    if (Measure != FileAlreadyExistEventArgs.Measure.SKIP)
                    {
                        await Handler.Transmit(Measure);
                        FileSuccess();
                        ++i;
                    }
                    else
                    {
                        FileSkipped();
                        ++i;
                    }

                    this.FinishedLength += Handler.Entry.length;
                    FilesAlready.Content = i.ToString();
                }
                catch (Exception E)
                {
                    FileFailed();
                }

                Handler.Dispose();
            }

            App.FakeDispatcher.Invoke(() =>
            {
                PromptOnClose = false;
                if (!IsClosing && CloseAutomatically)
                {
                    Close();
                }

                return null;
            });
        }

        private void Handler_ProgressChangedEvent(object sender, FileTransHandler.ProgressChangedEventArgs e)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                Percent.Value = Convert.ToInt32(e.Percent);
                long TotalTransmitted = this.FinishedLength + e.Received;
                double TotalPercent = this.HandlerList.TotalLength > 0 ? (double)TotalTransmitted / (double)this.HandlerList.TotalLength : 0;
                PercentTotal.Value = Convert.ToInt32(TotalPercent * 100.0);
                this.TaskbarItemInfo.ProgressValue = TotalPercent;

                Speed.Content = Utils.ToHumanReadableSize((long)e.BytesPerSec) + "/s";

                BytesAlready.Content = Utils.ToHumanReadableSize(e.Received);
                BytesTotal.Content = Utils.ToHumanReadableSize(e.Size);

                AllBytesAlready.Content = Utils.ToHumanReadableSize(TotalTransmitted);
                AllBytesTotal.Content = Utils.ToHumanReadableSize(this.HandlerList.TotalLength);

                Logs.AppendText(LogBuffer.ToString());
                LogBuffer.Clear();

                Logs.ScrollToEnd();

                return null;
            });
        }
    }
}

