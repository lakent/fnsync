using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public partial class WindowUnhandledException : AdonisWindow
    {
        public static void ShowException(Exception e)
        {
#if DEBUG
            throw new Exception("", e);
#else
            Environment.SetEnvironmentVariable("LAST_ERROR_STRING", BuildString(e));
            Environment.SetEnvironmentVariable("LAST_ERROR_PID", Process.GetCurrentProcess().Id.ToString());
            _ = Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-LE");
#endif
        }

        public WindowUnhandledException(Exception e) : this(BuildString(e))
        {

        }

        public WindowUnhandledException(string e)
        {
            InitializeComponent();
            Message.Text = e;
        }

        private static string BuildString(Exception e)
        {
            StringBuilder sb = new StringBuilder();
            while (e != null)
            {
                if (sb.Length > 0)
                {
                    _ = sb.Append("\n\nCaused By:\n");
                }

                _ = sb.Append(e.Message).Append('\n')
                    .Append(e.StackTrace);

                e = e.InnerException;
            }

            _ = sb.Append('\n');

            return sb.ToString();
        }

        private void QuitThis_Click(object sender, RoutedEventArgs e)
        {
            _ = Process.Start("taskkill", "/F /PID " + Environment.GetEnvironmentVariable("LAST_ERROR_PID"));
            Close();
        }

        private void IgnoreThis_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            App.ExitApp();
        }
    }
}
