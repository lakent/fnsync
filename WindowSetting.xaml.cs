using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Windows.ApplicationModel;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowSetting.xaml
    /// </summary>
    public partial class WindowSetting : Window
    {
        public static WindowSetting CurrentWindow { get; protected set; } = null;
        public static void NewOne()
        {
            App.FakeDispatcher.Invoke(() => { if (CurrentWindow == null) { new WindowSetting().Show(); } else { CurrentWindow.Activate(); } return null; });
        }

        public WindowSetting()
        {
            CurrentWindow = this;
            InitializeComponent();
            Settings.DataContext = MainConfig.Config;
            AppList.ItemsSource = RecordedPkgMapping.Singleton;

            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            Version.Text = $"{v.Major}.{v.Minor}.{v.Build}";

            IdField.Text = MainConfig.Config.ThisId;
            LocaleField.Text = Thread.CurrentThread.CurrentCulture.Name;
#if DEBUG
            NotificationClickEventTab.Visibility = Visibility.Visible;
#endif
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            CurrentWindow = null;
            MainConfig.Config.Update();
        }

        private void ClipboardSync_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            ClipboardManager.Singleton.MonitorClipboard = cb.IsChecked ?? false;
        }

        private void HyperLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink link)
            {
                System.Diagnostics.Process.Start(link.NavigateUri.AbsoluteUri);
            }
        }

        private void FixedListenPort_Checked(object sender, RoutedEventArgs e)
        {
            PortNumber.Text = ClientListener.Singleton.Port.ToString();
        }

        private void FixedListenPort_Unchecked(object sender, RoutedEventArgs e)
        {
            PortNumber.Text = "0";
        }
    }

    [ValueConversion(typeof(int), typeof(bool))]
    public class SpecificPortConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int v))
                return false;

            return v != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool b))
                return 0;

            return b ? 11223 : 0;
        }
    }

}
