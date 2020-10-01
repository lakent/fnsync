using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
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
        private readonly FileTransmission Transmission;
        private bool PromptOnClose = false;

        public WindowFileReceive(FileTransmission transmission)
        {
            this.Transmission = transmission;
            InitializeComponent();
            this.ContentRendered += Rendered;
        }

        private void OnPercentageChanged(object sender, FileTransmission.ProgressChangedEventArgs e)
        {
            Percent.Value = Convert.ToInt32(e.Percent);
            PercentTotal.Value = Convert.ToInt32(e.TotalPercent);

            Speed.Content = Utils.ToHumanReadableSize((long)(e.BytesPerSec)) + "/s";

            BytesAlready.Content = Utils.ToHumanReadableSize(e.Received);
            BytesTotal.Content = Utils.ToHumanReadableSize(e.Size);

            AllBytesAlready.Content = Utils.ToHumanReadableSize(e.TotalReceived);
            AllBytesTotal.Content = Utils.ToHumanReadableSize(e.TotalSize);
        }

        private void OnFinished(object sender, EventArgs e)
        {
            PromptOnClose = false;
            Close();
        }

        private void OnNextFile(object sender, FileTransmission.NextFileEventArgs e)
        {
            FilesAlready.Content = e.Current.ToString();
            FilesTotal.Content = e.Count.ToString();
            SaveTo.Text = e.dest;
        }

        private WindowFileAlreadyExists.ActionChangedEventArgs FileAlreadyExistsArgs = null;

        private string MakeNewName(string dest)
        {
            string dirpart = Path.GetDirectoryName(dest);
            string namepart = Path.GetFileNameWithoutExtension(dest);
            string extension = Path.GetExtension(dest);

            for( int i = 2; i <= int.MaxValue; ++i)
            {
                string newname = $"{namepart} ({i}){extension}";
                string path = Path.Combine(dirpart, newname);
                if( !Directory.Exists(path) && !File.Exists(path))
                {
                    return newname;
                }
            }

            return null;
        }

        private void FileAlreadyExistHandler(object sender, FileTransmission.FileAlreadyExistEventArgs e)
        { 
            if( Transmission.FileCount == 1)
            {
                e.Action = FileTransmission.FileAlreadyExistEventArgs.Handle.OVERWRITE;
                return;
            } else
            {
                if( FileAlreadyExistsArgs?.ApplyToAll == true)
                {
                    e.Action = FileAlreadyExistsArgs.Action;
                } else
                {
                    WindowFileAlreadyExists window = new WindowFileAlreadyExists(e.Dest);
                    window.ActionChanged += ActionChangedEventHandler;
                    window.ShowDialog();

                    e.Action = FileAlreadyExistsArgs.Action;
                }

                if(e.Action == FileTransmission.FileAlreadyExistEventArgs.Handle.RENAME)
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
            if ( PromptOnClose && MessageBox.Show(
                    (string)FindResource("CancelFileTransferPrompt"),
                    (string)FindResource("Cancelling"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                ) != MessageBoxResult.Yes
                )
            {
                e.Cancel = true;
                return;
            }

            Transmission.Dispose();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Rendered(object sender, EventArgs e)
        {
            this.Transmission.ProgressChanged += OnPercentageChanged;
            this.Transmission.Finished += OnFinished;
            this.Transmission.NextFile += OnNextFile;
            this.Transmission.FileAlreadyExist += FileAlreadyExistHandler;

            SaveTo.Text = string.Format((string)FindResource("SaveTo"), "");

            if (this.Transmission.FileCount == 1)
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.FileName = Transmission.FirstName; // Default file name
                //dlg.DefaultExt = ".text"; // Default file extension
                dlg.Filter = "*|*"; // Filter files by extension
                dlg.OverwritePrompt = true;

                // Show save file dialog box
                bool? result = dlg.ShowDialog();

                // Process save file dialog box results
                if (result == true)
                {
                    // Save document
                    string filename = dlg.FileName;
                    this.Transmission.StartNext(
                        Path.GetDirectoryName(filename),
                        Path.GetFileName(filename)
                        );
                }
                else
                {
                    Close();
                }
            }
            else
            {
                VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
                dialog.Description = (string)FindResource("SaveTo");
                dialog.UseDescriptionForTitle = true; // This applies to the Vista style dialog only, not the old dialog.
                if (!VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
                    MessageBox.Show(this, "Because you are not using Windows Vista or later, the regular folder browser dialog will be used. Please use Windows Vista to see the new dialog.", "Sample folder browser dialog");
                if ((bool)dialog.ShowDialog(this))
                {
                    string folder = dialog.SelectedPath;

                    this.Transmission.StartNext(folder, null);
                }
                else
                {
                    Close();
                }
            }

            PromptOnClose = true;
        }
    }
}
