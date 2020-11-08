using Newtonsoft.Json.Linq;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;  
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;

namespace FnSync
{
    public interface IControlFolderListItemView : INotifyPropertyChanged
    {
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
        string Name { get; }
        string Path { get; }
    }

    public class ControlFolderListDummy : IControlFolderListItemView
    {
        public static readonly ControlFolderListDummy Dummy = new ControlFolderListDummy();

        private static readonly string DummyName = (string)App.Current.FindResource("LoadingContent");

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public string Name => DummyName;
        public string Path => "";

        private ControlFolderListDummy() { }
    }

    public abstract class ControlFolderListItemView : IControlFolderListItemView, IDisposable
    {
        public class Item
        {
            public string type { get; set; }
            public string path { get; set; }
            public string name { get; set; }
            public bool haschild { get; set; }
            public long size { get; set; }
            public long last { get; set; }

            public static int Comparison(Item x, Item y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }

                if (x == null && y != null)
                {
                    return -1;
                }

                if (x != null && y == null)
                {
                    return 1;
                }

                if (x.type != y.type)
                {
                    return x.type == "file" ? 1 : -1;
                }
                else
                {
                    return x.name.CompareTo(y.name);
                }
            }

            public static void ImcrementalUpdate(IList<Item> Target, IList<Item> From)
            {
                if (!From.Any())
                {
                    Target.Clear();
                }
                else
                {
                    int ti = 0;
                    int fi = 0;

                    while (ti < Target.Count && fi < From.Count)
                    {
                        Item t = Target[ti];
                        Item f = From[fi];

                        int comparing = Item.Comparison(t, f);
                        if (comparing < 0)
                        {
                            Target.RemoveAt(ti);
                            continue;
                        }
                        else if (comparing > 0)
                        {
                            Target.Insert(ti, f);
                        }

                        ++ti;
                        ++fi;
                    }

                    while (ti < Target.Count)
                    {
                        Target.RemoveAt(ti);
                    }

                    while (fi < From.Count)
                    {
                        Target.Add(From[fi]);
                        ++fi;
                    }
                }
            }
        }

        public ObservableCollection<IControlFolderListItemView> Children { get; } = new ObservableCollection<IControlFolderListItemView>();

        public bool Dummied => Children.Count == 1 && Children[0] == ControlFolderListDummy.Dummy;
        private bool isExpanded = false;
        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (isExpanded != value)
                {
                    isExpanded = value;
                    if (value)
                    {
                        if (Dummied)
                        {
                            RequestChildren();
                        }
                    }
                    else
                    {
                        CollapseAction();
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
                }
            }
        }

        public virtual void CollapseAction()
        {

        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    if (value)
                    {
                        RequestChildren();
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                }
            }
        }

        public string Name { get; protected set; }
        public string Path { get; protected set; }
        protected ControlFolderListItemView Parent { get; }
        public ControlFolderListPhoneRootItem Root { get; protected set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RemoveDummy()
        {
            if (Dummied)
            {
                Children.Clear();
            }
        }

        protected virtual void OnSelected(string path)
        {

        }

        protected void ClearAndDummy()
        {
            Children.Clear();
            Children.Add(ControlFolderListDummy.Dummy);
        }

        protected abstract void RequestChildren();

        public void LoadChildren(IList<Item> folders)
        {
            Children.Clear();

            foreach (Item f in folders)
            {
                if (f.type != "file")
                    Children.Add(new ControlFolderListPhoneFolderItem(this, Root, f.name, f.haschild));
            }

            if (Children.Count == 1)
            {
                Children[0].IsExpanded = true;
            }
        }

        public abstract void Dispose();

        public abstract void Refresh();

        public ControlFolderListItemView(ControlFolderListItemView Parent, ControlFolderListPhoneRootItem Root, bool Dummied)
        {
            this.Parent = Parent;
            this.Root = Root;
            if (Dummied)
            {
                ClearAndDummy();
            }
        }
    }

    public class ControlFolderListPhoneRootItem : ControlFolderListItemView
    {
        public const string MSG_TYPE_GET_STORAGE = "get_storage";
        public const string MSG_TYPE_STORAGE_LIST = "storage_list";
        public const string MSG_TYPE_FOLDER_CONTENT_CHANGED = "file_content_changed";
        public const string MSG_TYPE_FILE_RENAME = "file_rename";
        public const string MSG_TYPE_FILE_RENAME_SUCCEED = "file_rename_succeed";
        public const string MSG_TYPE_FILE_DELETE = "file_delete";
        public const string MSG_TYPE_LIST_FOLDER = "list_folder";
        public const string MSG_TYPE_FOLDER_CONTENT = "folder_content";

        internal readonly Dictionary<string, ControlFolderListItemView> RequestMap = new Dictionary<string, ControlFolderListItemView>();
        internal readonly Dictionary<string, IList<Item>> RequestCache = new Dictionary<string, IList<Item>>();

        public PhoneClient Client { get; protected set; } = null;
        public ControlFolderList ThisControl { get; }


        public ControlFolderListPhoneRootItem(PhoneClient client, ControlFolderList control) : base(null, null, true)
        {
            base.Root = this;
            this.Name = client.Name;
            this.Path = "/";
            this.ThisControl = control;
            ResetWith(client);
        }

        private void StorageListCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;


        }

        private void FolderContentCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            string path = (string)msg["path"];

            if (!RequestMap.ContainsKey(path))
                return;

            JArray list = (JArray)msg["files"];

            List<Item> folders = list.ToObject<List<Item>>();

            folders.Sort(Item.Comparison);

            RequestCache[path] = folders;

            ControlFolderListItemView view = RequestMap[path];
            view.LoadChildren(folders);

            if (view.IsSelected)
            {
                ThisControl?.FireSelectionChanged(this, client, path, folders);
            }
        }

        private void ReconnectedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            ResetWith(client);
        }
        private void ContentChangedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            string path = (string)msg["folder"];
            RequestMap[path]?.Refresh();
        }

        public void RequestChildren(ControlFolderListItemView view)
        {
            string ViewPath = view.Path;

            if (RequestCache.ContainsKey(ViewPath))
            {
                if (Dummied)
                {
                    view.LoadChildren(RequestCache[ViewPath]);
                }

                OnSelected(ViewPath);
            }
            else
            {
                ThisControl?.FireSelectionChanged(this, Client, ViewPath, null);

                RequestMap[ViewPath] = view;

                JObject req = new JObject()
                {
                    ["path"] = ViewPath
                };

                Client.SendMsg(req, MSG_TYPE_LIST_FOLDER);
            }
        }

        protected override void RequestChildren()
        {
            RequestChildren(this);
        }

        public override void Refresh()
        {
            string id = this.Client.Id;

            PhoneClient NewClient = AlivePhones.Singleton[id];

            if (NewClient != null && NewClient.IsAlive)
            {
                this.ResetWith(NewClient);
            }
            else
            {
                MessageBox.Show(
                    (string)App.Current.FindResource("DeviceIsOffline"),
                    (string)App.Current.FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
            }
        }

        public void ResetWith(PhoneClient client)
        {
            if (this.Client != null)
            {
                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    MSG_TYPE_FOLDER_CONTENT,
                    FolderContentCallback
                );

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                    ReconnectedCallback
                );

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    MSG_TYPE_FOLDER_CONTENT_CHANGED,
                    ContentChangedCallback
                );

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    MSG_TYPE_STORAGE_LIST,
                    StorageListCallback
                );

            }

            this.Client = client;
            RequestCache.Clear();
            RequestMap.Clear();

            if (this.Client != null)
            {
                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    MSG_TYPE_FOLDER_CONTENT,
                    FolderContentCallback,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                    ReconnectedCallback,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    MSG_TYPE_FOLDER_CONTENT_CHANGED,
                    ContentChangedCallback,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    MSG_TYPE_STORAGE_LIST,
                    StorageListCallback,
                    true
                );

                ClearAndDummy();

                RequestChildren();

                /*
                if (IsExpanded)
                {
                    IsExpanded = false;
                }

                IsExpanded = true;
                */
            }
        }

        protected override void OnSelected(string path)
        {
            if (RequestCache.ContainsKey(path))
            {
                ThisControl?.FireSelectionChanged(this, Client, path, RequestCache[path]);
            }
        }

        public override void Dispose()
        {
            ResetWith(null);
        }
    }

    public class ControlFolderListPhoneStorageItem : ControlFolderListItemView
    {
        public ControlFolderListPhoneStorageItem(ControlFolderListPhoneRootItem Phone, string Name, string Path) : base(Phone, Phone, true)
        {
            this.Name = Name;
            this.Path = Path;
        }

        public override void Dispose()
        {

        }

        public override void Refresh()
        {

        }

        protected override void RequestChildren()
        {

        }
    }

    public class ControlFolderListPhoneFolderItem : ControlFolderListItemView
    {
        public ControlFolderListPhoneFolderItem(ControlFolderListItemView Parent, ControlFolderListPhoneRootItem Root, string Name, bool HasChildFolder) : base(Parent, Root, HasChildFolder)
        {
            this.Name = Name;
            this.Path = Parent.Path + Name + "/";
        }

        public override void CollapseAction()
        {
            /*
            base.CollapseAction();
            if (!Dummied && Children.Any())
            {
                foreach (ControlFolderListItemView c in Children)
                {
                    c.CollapseAction();
                }

                Root.RequestMap.Remove(Path);
                ClearAndDummy();
            }
            */
        }

        public override void Dispose()
        {
            foreach (IControlFolderListItemView child in Children)
            {
                if (child is ControlFolderListItemView c)
                {
                    c.Dispose();
                }
            }

            Root.RequestMap.Remove(Path);
            Root.RequestCache.Remove(Path);
            ClearAndDummy();
        }

        public override void Refresh()
        {
            this.Dispose();

            RequestChildren();

            /*
            if (IsExpanded)
            {
                IsExpanded = false;
            }

            IsExpanded = true;
            */
        }

        protected override void OnSelected(string path)
        {
            if (Root.RequestCache.ContainsKey(path))
            {
                Root.ThisControl?.FireSelectionChanged(Root, Root.Client, path, Root.RequestCache[path]);
            }
            else
            {
                Root.ThisControl?.FireSelectionChanged(Root, Root.Client, null, null);
            }
        }

        protected override void RequestChildren()
        {
            Root.RequestChildren(this);
        }
    }

    public partial class ControlFolderList : UserControlExtension, INotifyPropertyChanged
    {
        private readonly List<ControlFolderListPhoneRootItem> Roots = new List<ControlFolderListPhoneRootItem>();

        public string CurrentPath { get; protected set; } = null;
        public ObservableCollection<ControlFolderListItemView.Item> ContentItems { get; protected set; }
        public PhoneClient CurrentClient { get; protected set; } = null;
        public ControlFolderListPhoneRootItem CurrentRoot { get; protected set; } = null;

        public event PropertyChangedEventHandler PropertyChanged;

        private class RefreshCommandClass : ICommand
        {
            public static readonly RefreshCommandClass RefreshCommand = new RefreshCommandClass();

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                if (!(parameter is TreeViewItem item))
                    return;

                object dataContext = item.DataContext;

                if (dataContext is ControlFolderListItemView view)
                {
                    view.Refresh();
                }
            }
        }

        public ICommand RefreshCommand => RefreshCommandClass.RefreshCommand;

        public void FireSelectionChanged(ControlFolderListPhoneRootItem Root, PhoneClient client, string path, IList<ControlFolderListItemView.Item> items)
        {
            bool FullyLoad = this.CurrentPath != path;
            this.CurrentPath = path;

            if (FullyLoad)
            {
                if (items == null)
                {
                    this.ContentItems = null;
                }
                else
                {
                    this.ContentItems = new ObservableCollection<ControlFolderListItemView.Item>(items);
                }
            }
            else
            {
                if (items != null)
                {
                    if (this.ContentItems == null)
                    {
                        this.ContentItems = new ObservableCollection<ControlFolderListItemView.Item>();
                    }

                    ControlFolderListItemView.Item.ImcrementalUpdate(this.ContentItems, items);
                }
            }

            if (CurrentClient != client)
            {
                CurrentClient = client;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentClient"));
            }

            this.CurrentRoot = Root;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPath"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentItems"));
        }

        public ControlFolderList()
        {
            InitializeComponent();
        }

        protected override void OnClosing()
        {
            foreach (ControlFolderListPhoneRootItem r in Roots)
            {
                r.Dispose();
            }
        }

        protected override void LoadDataContext()
        {
            List<PhoneClient> Clients = new List<PhoneClient>(1);

            if (DataContext is PhoneClient client)
            {
                Clients.Add(client);
            }
            else if (DataContext is IEnumerable<PhoneClient> cs)
            {
                Clients.AddRange(cs);
            }
            else
            {
                return;
            }

            foreach (PhoneClient c in Clients)
            {
                Roots.Add(new ControlFolderListPhoneRootItem(c, this));
            }

            ListView.ItemsSource = null;
            ListView.ItemsSource = Roots;

            if (Roots.Count == 1)
            {
                Roots[0].IsExpanded = true;
                Roots[0].IsSelected = true;
            }
        }

        private void ItemMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item != null)
            {
                item.IsSelected = true;
                e.Handled = true;
            }
        }

        public void RefreshCurrentSelected()
        {
            if (!(ListView.SelectedItem is ControlFolderListItemView itemView))
                return;

            itemView.Refresh();
        }

        public void ToSubfolder(string Subfolder)
        {
            if (CurrentRoot == null)
                return;

            ControlFolderListItemView itemView = CurrentRoot.RequestMap[CurrentPath];
            if (itemView == null)
                return;

            itemView.IsExpanded = true;

            foreach (ControlFolderListItemView child in itemView.Children)
            {
                if (child.Name == Subfolder)
                {
                    child.IsExpanded = true;
                    child.IsSelected = true;
                    break;
                }
            }
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (!(sender is TreeViewItem item))
                return;

            item.BringIntoView();
            e.Handled = true;
        }
    }
}
