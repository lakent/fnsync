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
using static FnSync.WindowFileManagerRename;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlFileList.xaml
    /// </summary>
    public partial class ControlFileList : UserControlExtension
    {
        public PhoneClient Client
        {
            get
            {
                return (PhoneClient)GetValue(ClientProperty);
            }
            set
            {
                SetValue(ClientProperty, value);
            }
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

        public ControlFileList()
        {
            InitializeComponent();
        }

        public ICommand RenameCommand
        {
            get
            {
                return new RenameCommandClass(this);
            }
        }
        public ICommand DeleteCommand
        {
            get
            {
                return new DeleteCommandClass(this);
            }
        }
        public ICommand CopyToPcCommand
        {
            get
            {
                return new CopyToPcCommandClass(this);
            }
        }
        public ICommand RefreshCommand
        {
            get
            {
                return new RefreshCommandClass(this);
            }
        }
        public ICommand CutInsideCommand
        {
            get
            {
                return new CutInsideCommandClass(this);
            }
        }
        public ICommand CopyInsideCommand
        {
            get
            {
                return new CopyInsideCommandClass(this);
            }
        }
        public ICommand PasteHereInsideCommand
        {
            get
            {
                return new PasteHereInsideCommandClass(this);
            }
        }
        public ICommand PasteToInsideCommand
        {
            get
            {
                return new PasteToInsideCommandClass(this);
            }
        }
        public ICommand RefreshMediaStoreCommand
        {
            get
            {
                return new RefreshMediaStoreCommandClass(this);
            }
        }
        public ICommand PasteHereFromPCCommand
        {
            get
            {
                return new PasteHereFromPCCommandClass(this);
            }
        }

        private void ListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid list) || e == null)
                return;

            if (e.ChangedButton != MouseButton.Left)
                return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
                )
                return;

            if (e.OriginalSource is Border b && b.Name == "DGR_Border")
            {
                e.Handled = true;

                list.UnselectAll();
                list.UnselectAllCells();
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid list) || e == null ||
                !(list.SelectedItem is ControlFolderListItemViewBase.UiItem item) ||
                (item.type != "dir" && item.type != "storage")
                )
                return;

            e.Handled = true;
            FolderList.ToSubfolder(item.name);
        }

        private void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is DataGrid list) || e == null ||
                list.SelectedItems.Count != 1 ||
                !(list.SelectedItem is ControlFolderListItemViewBase.UiItem item)
                )
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    FolderList.ToSubfolder(item.name);
                    break;

                case Key.Back:
                    e.Handled = true;
                    FolderList.ToUpfolder();
                    break;
            }
        }
    }

    [ValueConversion(typeof(ControlFolderListItemViewBase.UiItem), typeof(String))]
    public class LongToHumanReadableSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (!(value is ControlFolderListItemViewBase.UiItem item))
                return "";

            if (item.type == "dir")
            {
                return "";
            }
            else if (item.type == "storage")
            {
                return "";
            }
            else
            {
                return Utils.ToHumanReadableSize(item.size);
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
            if (!(value is long ts))
                return "";

            return DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(ControlFolderListItemViewBase.UiItem), typeof(ImageSource))]
    public class FileIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ControlFolderListItemViewBase.UiItem item))
                return null;

            if (item.type == "dir")
            {
                return IconUtil.Folder;
            }
            else if (item.type == "storage")
            {
                return IconUtil.Storage;
            }
            else
            {
                return IconUtil.ByExtension(item.name);
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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
