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
    public partial class WindowFileReceive : Window
    {
        private FileTransmission Transmission = null;
        private readonly List<ControlFolderListItemView.Item> PendingItems;
        private readonly PhoneClient Client = null;
        private readonly string RootOnPhone = null;
        private bool PromptOnClose = false;
        private bool IsClosing = false;
        private bool IsClosed = false;

        private WindowFileReceive()
        {
            InitializeComponent();
            this.ContentRendered += Rendered;
        }

        public WindowFileReceive(FileTransmission transmission) : this()
        {
            this.Transmission = transmission;
        }

        public WindowFileReceive(PhoneClient Client, IList items, string RootOnPhone) : this()
        {
            this.Client = Client;

            this.PendingItems = new List<ControlFolderListItemView.Item>(items.Count);

            foreach (object item in items)
            {
                if (item is ControlFolderListItemView.Item i)
                {
                    this.PendingItems.Add(i);
                }
            }

            this.RootOnPhone = RootOnPhone.EndsWith("/") ? RootOnPhone : RootOnPhone + '/';
        }

        private void OnPercentageChangedEventHandler(object sender, FileTransmission.ProgressChangedEventArgs e)
        {
            Percent.Value = Convert.ToInt32(e.Percent);
            PercentTotal.Value = Convert.ToInt32(e.TotalPercent);
            this.TaskbarItemInfo.ProgressValue = e.TotalPercent / 100;

            Speed.Content = Utils.ToHumanReadableSize((long)(e.BytesPerSec)) + "/s";

            BytesAlready.Content = Utils.ToHumanReadableSize(e.Received);
            BytesTotal.Content = Utils.ToHumanReadableSize(e.Size);

            AllBytesAlready.Content = Utils.ToHumanReadableSize(e.TotalReceived);
            AllBytesTotal.Content = Utils.ToHumanReadableSize(e.TotalSize);
        }

        private void OnFinishedEventHandler(object sender, EventArgs e)
        {
            PromptOnClose = false;
            if (!IsClosing)
                Close();
        }

        private void OnNextFileEventHandler(object sender, FileTransmission.NextFileEventArgs e)
        {
            FilesAlready.Content = e.Current.ToString();
            FilesTotal.Content = e.Count.ToString();
            SaveTo.Text = e.dest;
        }

        private void OnErrorHandler(object sender, EventArgs e)
        {
            MessageBox.Show(
                (string)FindResource("TransmitionFailed"),
                (string)FindResource("Prompt"),
                MessageBoxButton.OK,
                MessageBoxImage.Exclamation
                );

            PromptOnClose = false;
            Close();
        }

        private WindowFileAlreadyExists.ActionChangedEventArgs FileAlreadyExistsArgs = null;

        private string MakeNewName(string dest)
        {
            string dirpart = Path.GetDirectoryName(dest);
            string namepart = Path.GetFileNameWithoutExtension(dest);
            string extension = Path.GetExtension(dest);

            for (int i = 2; i <= int.MaxValue; ++i)
            {
                string newname = $"{namepart} ({i}){extension}";
                string path = Path.Combine(dirpart, newname);
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    return newname;
                }
            }

            return null;
        }

        private void FileAlreadyExistHandler(object sender, FileTransmission.FileAlreadyExistEventArgs e)
        {
            if (Transmission.FileCount == 1)
            {
                e.Action = FileTransmission.FileAlreadyExistEventArgs.Measure.OVERWRITE;
                return;
            }
            else
            {
                if (FileAlreadyExistsArgs?.ApplyToAll == true)
                {
                    e.Action = FileAlreadyExistsArgs.Action;
                }
                else
                {
                    this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
                    WindowFileAlreadyExists window = new WindowFileAlreadyExists(e.Dest);
                    window.ActionChanged += ActionChangedEventHandler;
                    window.ShowDialog();
                    this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

                    e.Action = FileAlreadyExistsArgs.Action;
                }

                if (e.Action == FileTransmission.FileAlreadyExistEventArgs.Measure.RENAME)
                {
                    e.NewName = MakeNewName(e.Dest);
                }
            }
        }

        public void ActionChangedEventHandler(object sender, WindowFileAlreadyExists.ActionChangedEventArgs e)
        {
            FileAlreadyExistsArgs = e;
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
            this.Transmission.OnFinishedEvent += OnFinishedEventHandler;
            this.Transmission.OnNextFileEvent += OnNextFileEventHandler;
            this.Transmission.FileAlreadyExistEvent += FileAlreadyExistHandler;
            this.Transmission.OnErrorEvent += OnErrorHandler;

            SaveTo.Text = string.Format((string)FindResource("SaveTo"), "");

            if (this.Transmission.FileCount == 1)
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    FileName = Transmission.FirstName, // Default file name
                    //dlg.DefaultExt = ".text"; // Default file extension
                    Filter = "*|*", // Filter files by extension
                    OverwritePrompt = true
                };

                // Show save file dialog box
                bool? result = dlg.ShowDialog();

                // Process save file dialog box results
                if (result == true)
                {
                    // Save document
                    string filename = dlg.FileName;
                    this.Transmission.SetLocalFolder(Path.GetDirectoryName(filename));
                    this.Transmission.StartNext(Path.GetFileName(filename));
                    PromptOnClose = true;
                }
                else
                {
                    Close();
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
                    this.Transmission.SetLocalFolder(dialog.SelectedPath);
                    this.Transmission.StartNext();
                    PromptOnClose = true;
                }
                else
                {
                    Close();
                }
            }

        }

        private async void GetFileMetaData()
        {
            SaveTo.Text = (string)FindResource("GettingFileMetaData");

            List<FileTransmission.PendingsClass.Entry> ResultEntries = new List<FileTransmission.PendingsClass.Entry>();

            foreach (ControlFolderListItemView.Item item in this.PendingItems)
            {
                if (item.type == "dir")
                {
                    string FolderPathOnPhone =
                        item.path.EndsWith("/") ? item.path : item.path + '/';

                    ResultEntries.Add(new FileTransmission.PendingsClass.Entry
                    {
                        key = null,
                        length = 0,
                        mime = null,
                        name = item.name,
                        path = FolderPathOnPhone,
                        last = item.last
                    });

                    JObject List = null;
                    try
                    {
                        List = await PhoneMessageCenter.Singleton.OneShotMsgPart(
                            Client,
                            new JObject()
                            {
                                ["path"] = RootOnPhone + item.path,
                                ["recursive"] = true
                            },
                            ControlFolderListPhoneRootItem.MSG_TYPE_LIST_FOLDER,
                            ControlFolderListPhoneRootItem.MSG_TYPE_FOLDER_CONTENT,
                            60000
                        );
                    }
                    catch (Exception e)
                    {
                        Close();
                    }

                    JArray ListPart = (JArray)List["files"];
                    List<ControlFolderListItemView.Item> Files = ListPart.ToObject<List<ControlFolderListItemView.Item>>();

                    foreach (ControlFolderListItemView.Item i in Files)
                    {
                        if (i.type == "dir")
                        {
                            ResultEntries.Add(new FileTransmission.PendingsClass.Entry
                            {
                                key = null,
                                length = 0,
                                mime = null,
                                name = i.name,
                                path = FolderPathOnPhone + (i.path.EndsWith("/") ? i.path : i.path + '/'),
                                last = item.last
                            });
                        }
                        else if (i.type == "file")
                        {
                            ResultEntries.Add(new FileTransmission.PendingsClass.Entry
                            {
                                key = null,
                                length = i.size,
                                mime = null,
                                name = i.name,
                                path = FolderPathOnPhone + i.path,
                                last = item.last
                            });
                        }
                    }
                }
                else if (item.type == "file")
                {
                    ResultEntries.Add(new FileTransmission.PendingsClass.Entry
                    {
                        key = null,
                        length = item.size,
                        mime = null,
                        name = item.name,
                        path = item.path,
                        last = item.last
                    });
                }

                if (IsClosed)
                {
                    return;
                }
            }

            this.Transmission = new FileTransmission(Client, ResultEntries.ToArray(), -1, RootOnPhone);

            StartTramsmission();
        }

        private void Rendered(object sender, EventArgs e)
        {
            this.TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
            if (this.Transmission != null)
            {
                StartTramsmission();
            }
            else if (this.PendingItems != null && this.PendingItems.Any())
            {
                PercentTotal.IsIndeterminate = true;
                this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                GetFileMetaData();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.MinWidth = this.ActualWidth;
            this.MinHeight = this.ActualHeight;
            this.MaxHeight = this.ActualHeight;
        }
    }
}
