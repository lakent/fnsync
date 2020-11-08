using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
using System.Windows.Media.TextFormatting;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileManagerRename.xaml
    /// </summary>
    public partial class WindowFileManagerRename : Window
    {
        public class RenameSubmitEventArgs : EventArgs
        {
            public readonly string OldName;
            public readonly string NewName;

            public RenameSubmitEventArgs(string OldName, string NewName)
            {
                this.OldName = OldName;
                this.NewName = NewName;
            }
        }

        public delegate void RenameSubmitEvent(object sender, RenameSubmitEventArgs Args);

        public event RenameSubmitEvent RenameSubmit;

        public string OldName { get; }

        public WindowFileManagerRename(string OldName)
        {
            this.OldName = OldName;
            InitializeComponent();
            NameBox.Text = OldName;
            this.ContentRendered += Rendered;
        }

        private void Rendered(object sender, EventArgs e)
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }

        protected override void OnClosing(CancelEventArgs e)
        {

        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string NewName = NameBox.Text;

            if (string.IsNullOrWhiteSpace(NewName))
                return;

            if (NewName != OldName)
            {
                if (NewName.Contains('/') || NewName.Contains('\\'))
                {
                    MessageBox.Show(
                        (string)FindResource("IllegalFileName"),
                        (string)FindResource("Prompt"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                        );

                    return;
                }

                RenameSubmit?.Invoke(this, new RenameSubmitEventArgs(this.OldName, NewName));
            }

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    OkButton_Click(null, null);
                    break;

                case Key.Escape:
                    CancelButton_Click(null, null);
                    break;
            }
        }
    }

    [ValueConversion(typeof(string), typeof(bool))]
    public class EmptyToDisableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string str))
                return false;

            return !string.IsNullOrWhiteSpace(str);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
