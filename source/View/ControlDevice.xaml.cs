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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlDevice.xaml
    /// </summary>
    public partial class ControlDevice : UserControlExtension
    {
        private readonly SavedPhones.Phone Saved;
        public ControlDevice(SavedPhones.Phone Saved)
        {
            this.Saved = Saved;
            InitializeComponent();
        }

        private void DeviceInfoTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This forces tabs to be loaded lazily
            if (e.Source is not TabControl)
            {
                return;
            }

            TabItem Selected = (TabItem)((TabControl)e.Source).SelectedItem;

            Selected.DataContext ??= this.Saved;
        }
    }
}
