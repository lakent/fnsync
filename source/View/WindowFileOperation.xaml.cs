using AdonisUI.Controls;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static FnSync.FileTransmissionAbstract;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileReceive.xaml
    /// </summary>
    public partial class WindowFileOperation : AdonisWindow
    {
        private bool PromptOnClose = false;
        private bool CloseAutomatically = true;
        private bool IsClosing = false;

        public IList<BaseEntry>? EntryList = null;
        public Func<IList<BaseEntry>>? PrepareEntryList { get; private set; } = null;
        private FileTransmissionAbstract.HandlerList HandlerList = null!;

        public readonly Directions Direction;

        public readonly string ClientId = null!;
        private string? destFolder = null;
        public string? DestFolder
        {
            get => destFolder;
            private set
            {
                if (value == null)
                {
                    throw new ArgumentException("Cannot set null", nameof(DestFolder));
                }

                destFolder = value;
            }
        }

        public readonly string? SrcFolder;
        public readonly string? DestStorage;
        public readonly string? SrcStorage;

        private long FinishedLength = 0;

        private event EventHandler? OnExitEvent;

        public WindowFileOperation(
            Directions Direction,
            Operations Operation,
            string ClientId,
            string? DestFolder = null,
            string? SrcFolder = null,
            string? DestStorage = null,
            string? SrcStorage = null
            )
        {
            InitializeComponent();
            SetTitle(Direction, Operation);
            this.ContentRendered += OnWindowRendered;

            this.Direction = Direction;

            this.ClientId = ClientId;

            if (DestFolder == null && this.Direction != Directions.PHONE_TO_PC)
            {
                throw new ArgumentException("Cannot be null", nameof(DestFolder));
            }

            this.destFolder = DestFolder;
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

        private void SetTitle(Directions Direction, Operations Operation)
        {
            string PatternString;
            switch (Direction)
            {
                case Directions.INSIDE_PHONE:
                    PatternString = (string)FindResource("FileOperation_InsidePhone");
                    break;

                case Directions.PC_TO_PHONE:
                    PatternString = (string)FindResource("FileOperation_PcToPhone");
                    break;

                case Directions.PHONE_TO_PC:
                    PatternString = (string)FindResource("FileOperation_PhoneToPc");
                    break;

                default:
                    return;
            }

            string OperationString;
            switch (Operation)
            {
                case Operations.COPY:
                    OperationString = (string)FindResource("Copy");
                    break;

                case Operations.CUT:
                    OperationString = (string)FindResource("Cut");
                    break;

                default:
                    return;
            }

            Title = string.Format(PatternString, OperationString);
        }

        private async void OnWindowRendered(object? sender, EventArgs e)
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

            if (this.DestFolder == null && this.Direction == Directions.PHONE_TO_PC)
            {
                if (this.EntryList.Count == 1)
                {
                    string FirstName = this.EntryList[0].ConvertedName;
                    string ext = Path.GetExtension(FirstName);

                    SaveFileDialog dlg = new()
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
                        this.DestFolder = Path.GetDirectoryName(FileName).AppendIfNotEnding("\\")!;
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
                    VistaFolderBrowserDialog dialog = new()
                    {
                        Description = FindResource("SaveTo") as string,
                        UseDescriptionForTitle = true
                    };

                    if (dialog.ShowDialog(this) == true)
                    {
                        this.DestFolder = dialog.SelectedPath.AppendIfNotEnding("\\")!;
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

            HandlerList = new HandlerList(
                this.EntryList,
                ClientId,
                DestFolder!,
                SrcFolder,
                DestStorage,
                SrcStorage
                );

            StartTramsmission();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private static readonly string FAILED = (string)Application.Current.FindResource("Failed");
        private static readonly string SKIPPED = (string)Application.Current.FindResource("Skipped");
        private static readonly string SUCCEED = (string)Application.Current.FindResource("Succeed");

        private readonly StringBuilder LogBuffer = new();

        private void FileFailed()
        {
            CloseAutomatically = false;
            LogBuffer.Append(FAILED);
            LogBuffer.Append('\n');
        }

        private void FileSkipped()
        {
            LogBuffer.Append(SKIPPED);
            LogBuffer.Append('\n');
        }

        private void FileSuccess()
        {
            LogBuffer.Append(SUCCEED);
            LogBuffer.Append('\n');
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

        private WindowFileAlreadyExists.ExistAction? FileExistsAction = null;

        private FileAlreadyExistEventArgs.Measure FileAlreadyExist(BaseEntry Entry)
        {
            if (FileExistsAction?.ApplyToAll == true)
            {
                return FileExistsAction.Action;
            }
            else
            {
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
                WindowFileAlreadyExists window = new(Entry.ConvertedName);
                window.ActionChanged += ActionChangedEventHandler;
                window.ShowDialog();
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

                return FileExistsAction!.Action;
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
            this.OnExitEvent?.Invoke(this, null!);
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

            FileTransmittionWatcher Watcher = new(ClientId);
            Watcher.Start();

            int i = 0;
            foreach (FileTransmissionAbstract Handler in this.HandlerList)
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
                catch (Exception)
                {
                    FileFailed();
                }

                Handler.Dispose();
            }

            Watcher.Dispose();

            await Task.Delay(1500);

            App.FakeDispatcher.Invoke(() =>
            {
                if (!CloseAutomatically)
                {
                    Logs.AppendText("\n");
                    Logs.AppendText((string)Application.Current.FindResource("OneOrMoreFailed"));
                }

                PromptOnClose = false;
                if (!IsClosing && CloseAutomatically)
                {
                    Close();
                }

                return null;
            });
        }

        private void Handler_ProgressChangedEvent(object sender, ProgressChangedEventArgs e)
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

