using FnSync.FileTransmission;
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

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileReceive.xaml
    /// </summary>
    public partial class WindowFileOperation : Window
    {
        private IBase Transmission = null;
        private readonly IList<ControlFolderListItemView.UiItem> PendingItems;
        private readonly PhoneClient Client = null;
        private readonly string RootFolderOnSource = null;
        private readonly string DestFolder = null;
        private bool PromptOnClose = false;
        private bool CloseAutomatically = true;
        private bool IsClosing = false;
        private bool IsClosed = false;

        private void SetTitle()
        {
            string Pattern;
            switch (this.Transmission.Direction)
            {
                case DirectionClass.INSIDE_PHONE:
                    Pattern = (string)FindResource("FileOperation_InsidePhone");
                    break;

                case DirectionClass.PC_TO_PHONE:
                    Pattern = (string)FindResource("FileOperation_PcToPhone");
                    break;

                case DirectionClass.PHONE_TO_PC:
                    Pattern = (string)FindResource("FileOperation_PhoneToPc");
                    break;

                default:
                    return;
            }

            string Operation;
            switch (this.Transmission.Operation)
            {
                case OperationClass.COPY:
                    Operation = (string)FindResource("Copy");
                    break;

                case OperationClass.CUT:
                    Operation = (string)FindResource("Cut");
                    break;


                default:
                    return;
            }

            Title = string.Format(Pattern, Operation);
        }

        private void Rendered(object sender, EventArgs e)
        {
            SetTitle();
            this.TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
            if (this.Transmission.IsInitialzed)
            {
                StartTramsmission();
            }
            else if (this.PendingItems != null && this.PendingItems.Any())
            {
                PercentTotal.IsIndeterminate = true;
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                GetFileMetaDataAndInitialize();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private WindowFileOperation()
        {
            InitializeComponent();
            this.ContentRendered += Rendered;
        }

        public WindowFileOperation(IBase InitializedTransmission, string DestFolder = null) : this()
        {
            this.Transmission = InitializedTransmission;
            this.DestFolder = DestFolder;
        }

        public WindowFileOperation(IBase Transmission, PhoneClient Client, IList<ControlFolderListItemView.UiItem> items, string RootOnPhone, string DestFolder = null) : this(Transmission, DestFolder)
        {
            this.Client = Client;
            this.PendingItems = items;
            this.RootFolderOnSource = RootOnPhone;
        }


        public WindowFileOperation(BaseModule<BaseEntry> Transmission, PhoneClient Client, IList items, string RootOnPhone, string DestFolder = null) : this(Transmission, Client, items.CloneToTypedList<ControlFolderListItemView.UiItem>(), RootOnPhone, DestFolder)
        { }

        private void OnPercentageChangedEventHandler(object sender, ProgressChangedEventArgs e)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                Percent.Value = Convert.ToInt32(e.Percent);
                PercentTotal.Value = Convert.ToInt32(e.TotalPercent);
                this.TaskbarItemInfo.ProgressValue = e.TotalPercent / 100;

                Speed.Content = Utils.ToHumanReadableSize((long)(e.BytesPerSec)) + "/s";

                BytesAlready.Content = Utils.ToHumanReadableSize(e.Received);
                BytesTotal.Content = Utils.ToHumanReadableSize(e.Size);

                AllBytesAlready.Content = Utils.ToHumanReadableSize(e.TotalReceived);
                AllBytesTotal.Content = Utils.ToHumanReadableSize(e.TotalSize);

                Logs.AppendText(LogsBuffer.ToString());
                LogsBuffer.Clear();

                Logs.ScrollToEnd();

                return null;
            });
        }

        private static readonly string FAILED = (string)App.Current.FindResource("Failed");
        private static readonly string SKIPPED = (string)App.Current.FindResource("Skipped");
        private static readonly string SUCCEED = (string)App.Current.FindResource("Succeed");

        private readonly StringBuilder LogsBuffer = new StringBuilder();

        private void OnNextFileEventHandler(object sender, NextFileEventArgs e)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                switch (e.lastStatus)
                {
                    case TransmissionStatus.FAILED_ABORT:
                    case TransmissionStatus.FAILED_CONTINUE:
                        CloseAutomatically = false;
                        LogsBuffer.Append(FAILED);
                        LogsBuffer.Append("\n");
                        break;

                    case TransmissionStatus.SKIPPED:
                        LogsBuffer.Append(SKIPPED);
                        LogsBuffer.Append("\n");
                        break;

                    case TransmissionStatus.SUCCESSFUL:
                        LogsBuffer.Append(SUCCEED);
                        LogsBuffer.Append("\n");
                        break;

                    default:
                        break;
                }

                FilesAlready.Content = e.Current.ToString();

                if (e.entry != null)
                {
                    FilesTotal.Content = e.Count.ToString();
                    CurrentFile.Text = e.entry.ConvertedName;

                    LogsBuffer.Append(e.entry.ConvertedPath);
                    LogsBuffer.Append(" … ");
                }
                else
                {
                    CurrentFile.Text = "";
                }

                return null;
            });


            if (e.entry == null)
            {
                // Transfer end
                Transmission.Dispose();
                App.FakeDispatcher.Invoke(() =>
                {
                    PromptOnClose = false;
                    if (!IsClosing && CloseAutomatically)
                        Close();

                    return null;
                });
            }
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

        private void FileAlreadyExistHandler(object sender, FileAlreadyExistEventArgs e)
        {
            if (FileExistsAction?.ApplyToAll == true)
            {
                e.Action = FileExistsAction.Action;
            }
            else
            {
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
                WindowFileAlreadyExists window = new WindowFileAlreadyExists(e.entry.ConvertedName);
                window.ActionChanged += ActionChangedEventHandler;
                window.ShowDialog();
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

                e.Action = FileExistsAction.Action;
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

            Transmission?.Dispose();
            IsClosed = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void StartTramsmission()
        {
            PercentTotal.IsIndeterminate = false;
            this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

            this.Transmission.ProgressChangedEvent += OnPercentageChangedEventHandler;
            this.Transmission.OnNextFileEvent += OnNextFileEventHandler;
            this.Transmission.FileAlreadyExistEvent += FileAlreadyExistHandler;
            this.Transmission.OnErrorEvent += OnErrorHandler;

            CurrentFile.Text = string.Format((string)FindResource("SaveTo"), "");

            if (this.DestFolder == null)
            {
                if (this.Transmission.FileCount == 1)
                {
                    string FirstName = Transmission.FirstName;
                    string ext = Path.GetExtension(FirstName);

                    SaveFileDialog dlg = new SaveFileDialog
                    {
                        FileName = FirstName, // Default file name
                        DefaultExt = ext ?? "", // Default file extension
                        Filter = (ext != null ? $"*{ext}|*{ext}|" : "") + "*|*",
                        OverwritePrompt = true
                    };

                    // Show save file dialog box
                    bool? result = dlg.ShowDialog();

                    // Process save file dialog box results
                    if (result == true)
                    {
                        // Save document
                        string filename = dlg.FileName;
                        this.Transmission.DestinationFolder = Path.GetDirectoryName(filename);
                        this.Transmission.FirstName = Path.GetFileName(filename);
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
                        this.Transmission.DestinationFolder = dialog.SelectedPath;
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
                this.Transmission.DestinationFolder = this.DestFolder;
            }

            this.Transmission.StartTransmittion();
        }

        private async void GetFileMetaDataAndInitialize()
        {
            CurrentFile.Text = (string)FindResource("GettingFileMetaData");

            try
            {
                await this.Transmission.Init(Client, PendingItems, -1, RootFolderOnSource);
            }
            catch (Exception e)
            {
                Close();
                return;
            }

            if (IsClosed)
            {
                return;
            }

            StartTramsmission();
        }
    }
}
