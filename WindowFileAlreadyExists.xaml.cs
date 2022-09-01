using AdonisUI.Controls;
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
using static FnSync.FileTransmissionAbstract;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileAlreadyExists.xaml
    /// </summary>
    public partial class WindowFileAlreadyExists : AdonisWindow
    {
        public class ExistAction : EventArgs
        {
            public readonly FileAlreadyExistEventArgs.Measure Action;
            public readonly bool ApplyToAll;
            public ExistAction(
                FileAlreadyExistEventArgs.Measure Action,
                bool ApplyToAll
                )
            {
                this.Action = Action;
                this.ApplyToAll = ApplyToAll;
            }
        }

        public delegate void ActionChangedEventHandler(object sender, ExistAction e);

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
            FileAlreadyExistEventArgs.Measure action = FileAlreadyExistEventArgs.Measure.SKIP;

            if (Skip.IsChecked == true)
            {
                action = FileAlreadyExistEventArgs.Measure.SKIP;
            }
            else if (Overwrite.IsChecked == true)
            {
                action = FileAlreadyExistEventArgs.Measure.OVERWRITE;
            }
            else if (Rename.IsChecked == true)
            {
                action = FileAlreadyExistEventArgs.Measure.RENAME;
            }

            ActionChanged?.Invoke(this, new ExistAction(action, ApplyToAll.IsChecked == true));

            AllowClose = true;
            Close();
        }
    }
}
