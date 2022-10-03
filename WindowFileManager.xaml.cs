using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AdonisUI.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowFileManager.xaml
    /// </summary>
    public partial class WindowFileManager : AdonisWindow
    {
        public static void NewOne(string Id = null)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (AlivePhones.Singleton.Count == 0)
                {
                    _ = MessageBox.Show(
                            (string)Application.Current.FindResource("NoConnectedDevice"),
                            (string)Application.Current.FindResource("Prompt"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation
                            );

                    return null;
                }

                WindowFileManager window = new WindowFileManager(Id);
                window.Show();
                return null;
            });
        }

        private readonly ViewModel.ControlFolderList.ViewModel viewModel;
        public WindowFileManager(string Id)
        {
            InitializeComponent();
            viewModel = new ViewModel.ControlFolderList.ViewModel(Id);
            this.DataContext = this.viewModel;
            FolderTree.DataContext = this.viewModel;
            FileList.DataContext = this.viewModel;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ToParentFolder();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                viewModel.RefreshCurrentSelected();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        private void ProgressBar_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            /*
              Force update prompt,
              https://stackoverflow.com/a/5676257/1968839
             */

            Prompt.GetBindingExpression(ContentProperty).UpdateTarget();
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool boolval))
            {
                throw new ArgumentException();
            }

            bool inverse = Equals("true", parameter);
            return boolval != inverse ? Visibility.Visible : Visibility.Collapsed;
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

