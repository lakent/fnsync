using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileAlreadyExists.xaml
    /// </summary>
    public partial class WindowFileAlreadyExists : Window
    {
        public class ActionChangedEventArgs : EventArgs
        {
            public readonly FileTransmission.FileAlreadyExistEventArgs.Measure Action;
            public readonly bool ApplyToAll;
            public ActionChangedEventArgs(
                FileTransmission.FileAlreadyExistEventArgs.Measure Action,
                bool ApplyToAll
                )
            {
                this.Action = Action;
                this.ApplyToAll = ApplyToAll;
            }
        }

        public delegate void ActionChangedEventHandler(object sender, ActionChangedEventArgs e);

        public event ActionChangedEventHandler ActionChanged;
        public WindowFileAlreadyExists(string dest)
        {
            InitializeComponent();
            Dest.Text = dest;
        }

        private bool AllowClose = false;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !AllowClose;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            FileTransmission.FileAlreadyExistEventArgs.Measure action = FileTransmission.FileAlreadyExistEventArgs.Measure.SKIP;

            if( Skip.IsChecked == true )
            {
                action = FileTransmission.FileAlreadyExistEventArgs.Measure.SKIP;
            } else if( Overwrite.IsChecked == true)
            {
                action = FileTransmission.FileAlreadyExistEventArgs.Measure.OVERWRITE;
            } else if( Rename.IsChecked == true)
            {
                action = FileTransmission.FileAlreadyExistEventArgs.Measure.RENAME;
            }

            ActionChanged?.Invoke(this, new ActionChangedEventArgs(action, ApplyToAll.IsChecked == true));

            AllowClose = true;
            Close();
        }
    }
}
