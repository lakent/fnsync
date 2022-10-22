using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlSettings.xaml
    /// </summary>
    public partial class ControlSettings : UserControlExtension
    {
        public ControlSettings()
        {
            InitializeComponent();
            Settings.DataContext = MainConfig.Config;
            AppList.ItemsSource = RecordedPkgMapping.Singleton;

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            Version.Text = $"{v.Major}.{v.Minor}.{v.Build}";

            IdField.Text = MainConfig.Config.ThisId;
            LocaleField.Text = Thread.CurrentThread.CurrentCulture.Name;
#if DEBUG
            NotificationClickEventTab.Visibility = Visibility.Visible;
#endif
        }

        private void FixedListenPort_Checked(object sender, RoutedEventArgs e)
        {
            MainConfig.Config.FixedListenPort = PhoneListener.Singleton.Port;
        }

        private void FixedListenPort_Unchecked(object sender, RoutedEventArgs e)
        {
            MainConfig.Config.FixedListenPort = 0;
        }

        private void HyperLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink link)
            {
                _ = System.Diagnostics.Process.Start(link.NavigateUri.AbsoluteUri);
            }
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
