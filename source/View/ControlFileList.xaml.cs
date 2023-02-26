using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FnSync.Model.ControlFolderList;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlFileList.xaml
    /// </summary>
    public partial class ControlFileList : UserControlExtension
    {
        /*
        public PhoneClient Client
        {
            get => (PhoneClient)GetValue(ClientProperty);
            set => SetValue(ClientProperty, value);
        }

        public static readonly DependencyProperty ClientProperty =
            DependencyProperty.Register(
                "Client",
                typeof(PhoneClient),
                typeof(ControlFileList)
            );

        public string Folder // Current Folder On Phone
        {
            get
            {
                return (string)GetValue(FolderProperty);
            }
            set
            {
                SetValue(FolderProperty, value);
            }
        }

        public static readonly DependencyProperty FolderProperty =
            DependencyProperty.Register(
                "Folder",
                typeof(string),
                typeof(ControlFileList)
            );

        public ControlFolderList FolderList
        {
            get
            {
                return (ControlFolderList)GetValue(FolderListProperty);
            }
            set
            {
                SetValue(FolderListProperty, value);
            }
        }

        public static readonly DependencyProperty FolderListProperty =
            DependencyProperty.Register(
                "FolderList",
                typeof(ControlFolderList),
                typeof(ControlFileList)
            );
        */

        private ViewModel.ControlFolderList.ViewModel ViewModel
            => (this.DataContext as ViewModel.ControlFolderList.ViewModel)!;

        public ControlFileList()
        {
            InitializeComponent();
        }

        private void ListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid list || e == null)
            {
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
                )
            {
                return;
            }

            if (e.OriginalSource is Border b && b.Name == "DGR_Border")
            {
                e.Handled = true;

                list.UnselectAll();
                list.UnselectAllCells();
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid list || e == null ||
                list.SelectedItem is not PhoneFileInfo info ||
                (info.type != ItemType.Directory && info.type != ItemType.Storage)
                )
            {
                return;
            }

            e.Handled = true;
            ViewModel.ToChildFolder(list.SelectedIndex);
        }

        private void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DataGrid list || e == null ||
                list.SelectedItems.Count != 1 ||
                list.SelectedItem is not PhoneFileInfo info
                )
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    ViewModel.ToChildFolder(list.SelectedIndex);
                    break;

                case Key.Back:
                    e.Handled = true;
                    ViewModel.ToParentFolder();
                    break;

                default:
                    break;
            }
        }
    }

    [ValueConversion(typeof(PhoneFileInfo), typeof(String))]
    public class LongToHumanReadableSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is not PhoneFileInfo info)
            {
                return "";
            }

            if (info.type == ItemType.Directory)
            {
                return "";
            }
            else if (info.type == ItemType.Storage)
            {
                return "";
            }
            else
            {
                return Utils.ToHumanReadableSize(info.size);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(long), typeof(DateTime))]
    public class TimestampToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is long ts ?
                DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime :
                (object)"";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(PhoneFileInfo), typeof(ImageSource))]
    public class FileIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not PhoneFileInfo info)
            {
                return null;
            }

            if (info.type == ItemType.Directory)
            {
                return IconUtil.Folder;
            }
            else if (info.type == ItemType.Storage)
            {
                return IconUtil.Storage;
            }
            else
            {
                return IconUtil.ByExtension(info.name);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NonNullToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
