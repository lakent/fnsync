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
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileManager.xaml
    /// </summary>
    public partial class WindowFileManager : Window
    {
        private static WindowFileManager ThisWindow = null;
        public static void NewOne()
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (ThisWindow != null)
                {
                    ThisWindow.Activate();
                }
                else
                {
                    WindowFileManager window = new WindowFileManager();
                    window.Show();
                }
                return null;
            });
        }

        public WindowFileManager()
        {
            InitializeComponent();
            FolderTree.DataContext = AlivePhones.Singleton;
            ThisWindow = this;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            FolderTree.ToUpfolder();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if( e.Key == Key.F5)
            {
                e.Handled = true;
                FolderTree.RefreshCurrentSelected();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ThisWindow = null;
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Boolean.Equals(value, true) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
