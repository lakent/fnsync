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
    /// Interaction logic for WindowFileManager.xaml
    /// </summary>
    public partial class WindowFileManager : Window
    {
        public static void NewOne()
        {
            Application.Current.Dispatcher.InvokeAsyncCatchable(() =>
            {
                WindowFileManager window = new WindowFileManager();
                window.Show();
            });
        }

        public WindowFileManager()
        {
            InitializeComponent();
            FolderTree.DataContext = AlivePhones.Singleton;
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

    }
}
