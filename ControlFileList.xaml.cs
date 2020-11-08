using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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

        public string Folder
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

        private class RenameCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public RenameCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItems.Count == 1 && !string.IsNullOrWhiteSpace(FileList.Folder) && FileList.ListView.SelectedItem is ControlFolderListItemView.Item item && (item.type == "dir" || item.type == "file");
            }

            public void RenameSubmit(object sender, RenameSubmitEventArgs Args)
            {
                JObject msg = new JObject()
                {
                    ["folder"] = FileList.Folder,
                    ["old"] = Args.OldName,
                    ["new"] = Args.NewName,
                    ["storage"] = FileList.FolderList.CurrentStorage
                };

                FileList.Client.SendMsg(msg, ControlFolderListPhoneRootItem.MSG_TYPE_FILE_RENAME);
            }

            public void Execute(object parameter)
            {
                if (!(FileList.ListView.SelectedItem is ControlFolderListItemView.Item item))
                    return;


                WindowFileManagerRename dialog = new WindowFileManagerRename(item.name);
                dialog.RenameSubmit += RenameSubmit;

                dialog.ShowDialog();
            }
        }

        private ICommand renameCommand = null;
        public ICommand RenameCommand
        {
            get
            {
                if (renameCommand == null)
                {
                    renameCommand = new RenameCommandClass(this);
                }

                return renameCommand;
            }
        }

        private class DeleteCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public DeleteCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return FileList.ListView.SelectedItem != null && !string.IsNullOrWhiteSpace(FileList.Folder);
            }

            private string JoinItems(string delimiter, int max)
            {
                StringBuilder sb = new StringBuilder();

                int Count = FileList.ListView.SelectedItems.Count;

                for (int i = 0; i < max && i < Count; ++i)
                {
                    if (FileList.ListView.SelectedItems[i] is ControlFolderListItemView.Item item)
                    {
                        sb.Append(item.name).Append(delimiter);
                    }
                }

                return sb.ToString();
            }

            public void Execute(object parameter)
            {
                int Count = FileList.ListView.SelectedItems.Count;

                if (MessageBox.Show(
                    string.Format(
                        "{0}\n\n{1}{2}",
                        (string)App.Current.FindResource("BeSureToDelete"),
                        JoinItems("\n", 5),
                        Count > 5 ? string.Format((string)App.Current.FindResource("AndSomeMore"), Count) : ""
                        ),
                    (string)App.Current.FindResource("Prompt"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                ) == MessageBoxResult.Yes
                    )
                {
                    JObject msg = new JObject();
                    msg["folder"] = FileList.Folder;
                    msg["storage"] = FileList.FolderList.CurrentStorage;

                    JArray names = new JArray();
                    foreach (object obj in FileList.ListView.SelectedItems)
                    {
                        if (obj is ControlFolderListItemView.Item item && (item.type == "dir" || item.type == "file"))
                        {
                            names.Add(item.name);
                        }
                    }

                    msg["names"] = names;

                    FileList.Client.SendMsg(msg, ControlFolderListPhoneRootItem.MSG_TYPE_FILE_DELETE);
                }
            }
        }

        private ICommand deleteCommand = null;
        public ICommand DeleteCommand
        {
            get
            {
                if (deleteCommand == null)
                {
                    deleteCommand = new DeleteCommandClass(this);
                }

                return deleteCommand;
            }
        }

        private class RefreshCommandClass : ICommand
        {
            private readonly ControlFileList FileList;
            public RefreshCommandClass(ControlFileList FileList)
            {
                this.FileList = FileList;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                FileList.FolderList.RefreshCurrentSelected();
            }
        }

        private ICommand refreshCommand = null;
        public ICommand RefreshCommand
        {
            get
            {
                if (refreshCommand == null)
                {
                    refreshCommand = new RefreshCommandClass(this);
                }

                return refreshCommand;
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
                !(list.SelectedItem is ControlFolderListItemView.Item item) ||
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
                !(list.SelectedItem is ControlFolderListItemView.Item item)
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

    [ValueConversion(typeof(long), typeof(String))]
    public class LongToHumanReadableSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is long v))
                return "";

            return Utils.ToHumanReadableSize(v);
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

    [ValueConversion(typeof(long), typeof(DateTime))]
    public class FileIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ControlFolderListItemView.Item item))
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
