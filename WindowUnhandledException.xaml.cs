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
    /// Interaction logic for WindowUnhandledException.xaml
    /// </summary>
    public partial class WindowUnhandledException : Window
    {
        public static void ShowException(Exception e)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                new WindowUnhandledException(e).ShowDialog();
            });
        }

        public WindowUnhandledException(Exception e)
        {
            InitializeComponent();
            Message.Text = BuildString(e);
        }

        private String BuildString(Exception e)
        {
            StringBuilder sb = new StringBuilder();
            while (e != null)
            {
                if (sb.Length > 0)
                {
                    sb.Append("Caused By:\n");
                }

                sb.Append(e.Message).Append('\n')
                    .Append(e.StackTrace);

                e = e.InnerException;
            }

            sb.Append('\n');

            return sb.ToString();
        }

        private void QuitThis_Click(object sender, RoutedEventArgs e)
        {
            Close();
            App.ExitApp();
        }

        private void IgnoreThis_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
