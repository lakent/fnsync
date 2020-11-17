using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FnSync
{
    public interface IControlFolderListItemView : INotifyPropertyChanged
    {
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
        string Name { get; }
        string Path { get; }
    }

    public class ControlFolderListPlaceholder : IControlFolderListItemView
    {
        public static readonly ControlFolderListPlaceholder Dummy = new ControlFolderListPlaceholder((string)App.Current.FindResource("LoadingContent"));
        public static readonly ControlFolderListPlaceholder NoPermission = new ControlFolderListPlaceholder((string)App.Current.FindResource("NoFilePermission"));
        public static readonly ControlFolderListPlaceholder PhoneOffline = new ControlFolderListPlaceholder((string)App.Current.FindResource("DeviceIsOffline"));

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public string Name { get; }
        public string Path => "";

        private ControlFolderListPlaceholder(string name)
        {
            this.Name = name;
        }
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

        public bool Dummied => Children.Count == 1 && Children[0] == ControlFolderListPlaceholder.Dummy;
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
        public ControlFolderListItemView Parent { get; protected set; }
        public ControlFolderListPhoneRootItem Root { get; protected set; }
        public ControlFolderListPhoneStorageItem Storage { get; protected set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void CallPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            this.PropertyChanged?.Invoke(sender, args);
        }

        protected void RemoveDummy()
        {
            if (Dummied)
            {
                Children.Clear();
            }
        }

        protected void ClearAndDummy()
        {
            Children.Clear();
            Children.Add(ControlFolderListPlaceholder.Dummy);
        }

        protected abstract void RequestChildren();

        private static readonly string NumberOfItems = (string)App.Current.FindResource("NumberOfItems");

        private static string MakeItemsPrompt(int FolderCount, int FileCount)
        {
            return String.Format(NumberOfItems, FolderCount, FileCount);
        }

        public void ProcessChildrenItems(IList<Item> folders)
        {
            Children.Clear();

            int FolderCount = 0;
            int FileCount = 0;

            foreach (Item f in folders)
            {
                if (f.type == "dir")
                {
                    Children.Add(new ControlFolderListPhoneFolderItem(this, Root, Storage, f.name, f.haschild));
                    ++FolderCount;
                }

                else if (f.type == "storage")
                {
                    Children.Add(new ControlFolderListPhoneStorageItem(Root, f.name, f.path, f.haschild));
                    ++FolderCount;
                }
                else if (f.type == "file")
                {
                    ++FileCount;
                }
            }

            Root.NumberOfItemsPrompts.Add(folders, MakeItemsPrompt(FolderCount, FileCount));

            if (Children.Count == 1)
            {
                Children[0].IsExpanded = true;
            }
        }

        public abstract void Dispose();

        public abstract void Refresh();

        public ControlFolderListItemView(ControlFolderListItemView Parent, ControlFolderListPhoneRootItem Root, ControlFolderListPhoneStorageItem Storage, bool Dummied)
        {
            this.Parent = Parent;
            this.Root = Root;
            this.Storage = Storage;
            if (Dummied)
            {
                ClearAndDummy();
            }
        }
    }

    internal static class ControlFolderListRequestMapExtension
    {
        public static void Put(this Dictionary<Tuple<string, string>, ControlFolderListItemView> map, string Storage, string Path, ControlFolderListItemView view)
        {
            Tuple<string, string> tuple = new Tuple<string, string>(Storage, Path);
            map[tuple] = view;
        }

        public static bool PutIfNotExist(this Dictionary<Tuple<string, string>, ControlFolderListItemView> map, string Storage, string Path, ControlFolderListItemView view)
        {
            Tuple<string, string> tuple = new Tuple<string, string>(Storage, Path);
            if (map.ContainsKey(tuple))
            {
                return false;
            }

            map[tuple] = view;
            return true;
        }

        public static ControlFolderListItemView Get(this Dictionary<Tuple<string, string>, ControlFolderListItemView> map, string Storage, string Path)
        {
            Tuple<string, string> tuple = new Tuple<string, string>(Storage, Path);
            return map.ContainsKey(tuple) ? map[tuple] : null;
        }

        public static bool ContainsKey(this Dictionary<Tuple<string, string>, ControlFolderListItemView> map, string Storage, string Path)
        {
            Tuple<string, string> tuple = new Tuple<string, string>(Storage, Path);
            return map.ContainsKey(tuple);
        }

        public static void Remove(this Dictionary<Tuple<string, string>, ControlFolderListItemView> map, string Storage, string Path)
        {
            Tuple<string, string> tuple = new Tuple<string, string>(Storage, Path);
            map.Remove(tuple);
        }
    }

    public class ControlFolderListPhoneRootItem : ControlFolderListItemView
    {
        public const string MSG_TYPE_GET_STORAGE = "get_storage";
        public const string MSG_TYPE_STORAGE_LIST = "storage_list";
        public const string MSG_TYPE_FOLDER_CONTENT_CHANGED = "file_content_changed";
        public const string MSG_TYPE_FILE_RENAME = "file_rename";
        public const string MSG_TYPE_FILE_DELETE = "file_delete";
        public const string MSG_TYPE_LIST_FOLDER = "list_folder";
        public const string MSG_TYPE_FOLDER_CONTENT = "folder_content";
        public const string MSG_TYPE_FILE_MANAGER_NO_PERMISSION = "file_manager_no_permission";

        internal readonly Dictionary<Tuple<string, string>, ControlFolderListItemView> RequestMap = new Dictionary<Tuple<string, string>, ControlFolderListItemView>();
        internal readonly Dictionary<string, IList<Item>> RequestCache = new Dictionary<string, IList<Item>>();
        internal readonly ConditionalWeakTable<IList<Item>, string> NumberOfItemsPrompts = new ConditionalWeakTable<IList<Item>, string>();

        public static readonly IList<Item> Empty = new ReadOnlyCollection<Item>(new List<Item>());

        public PhoneClient Client { get; protected set; } = null;
        public ControlFolderList ThisControl { get; }

        public ControlFolderListPhoneRootItem(PhoneClient client, ControlFolderList control) : base(null, null, ControlFolderListPhoneStorageItem.VOID, true)
        {
            base.Root = this;
            this.Name = client.Name;
            this.Path = "";
            this.ThisControl = control;
            ResetWith(client);
        }

        private void StorageListCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            JArray list = (JArray)msg["storage"];
            List<Item> storage = list.ToObject<List<Item>>();

            RequestCache[this.Path] = storage;

            ControlFolderListItemView view = RequestMap.Get(Storage.Path, this.Path);

            if (view == null)
            {
                return;
            }

            view.ProcessChildrenItems(storage);

            if (view.Children.Count == 1)
            {
                view.Children[0].IsSelected = true;
            }
            else
            {
                if (view.IsSelected)
                {
                    ThisControl?.FireSelectionChanged(this, Storage.Path, client, this.Path, storage);
                }
            }
        }

        private void FolderContentCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            string path = (string)msg["path"];
            string storage = (string)msg["storage"];
            ControlFolderListItemView view = RequestMap.Get(storage, path);

            if (view == null)
                return;

            JArray list = (JArray)msg["files"];

            List<Item> folders = list.ToObject<List<Item>>();

            folders.Sort(Item.Comparison);

            RequestCache[path] = folders;

            view.ProcessChildrenItems(folders);

            if (view.IsSelected)
            {
                ThisControl?.FireSelectionChanged(this, storage, client, path, folders);
            }
        }

        private void ReconnectedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            ResetWith(client);
            OnNameChanged(client.Name);
        }

        private void DisconnectedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            Children.Clear();
            RequestCache.Clear();
            RequestMap.Clear();

            Children.Add(ControlFolderListPlaceholder.PhoneOffline);
            ThisControl.FireSelectionChanged(this, this.Storage.Path, Client, Path, Empty);
            ThisControl.Prompt = ControlFolderListPlaceholder.PhoneOffline.Name;
        }

        private void ContentChangedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            string path = (string)msg["folder"];
            string storage = (string)msg["storage"];
            RequestMap.Get(storage, path)?.Refresh();
        }

        private void OnNameChanged(string NewName)
        {
            this.Name = NewName;
            CallPropertyChanged(this, new PropertyChangedEventArgs("Name"));
        }

        private void NameChangedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is string name))
                return;

            OnNameChanged(name);
        }

        private void NoPermissionCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            Children.Clear();
            RequestCache.Clear();
            RequestMap.Clear();

            Children.Add(ControlFolderListPlaceholder.NoPermission);
            ThisControl.FireSelectionChanged(this, this.Storage.Path, Client, Path, Empty);
            ThisControl.Prompt = ControlFolderListPlaceholder.NoPermission.Name;
        }

        public void RequestChildren(ControlFolderListItemView view)
        {
            if (!Client.IsAlive)
            {
                return;
            }

            string ViewPath = view.Path;

            if (RequestCache.ContainsKey(ViewPath))
            {
                IList<Item> Cache = RequestCache[ViewPath];

                if (Dummied)
                {
                    view.ProcessChildrenItems(Cache);
                }

                if (view.IsSelected)
                {
                    ThisControl?.FireSelectionChanged(this, view.Storage.Path, Client, ViewPath, Cache);
                }
            }
            else
            {
                if (!RequestMap.PutIfNotExist(view.Storage.Path, ViewPath, view))
                {
                    return;
                }

                if (view.IsSelected)
                {
                    ThisControl?.FireSelectionChanged(this, view.Storage.Path, Client, ViewPath, null);
                    ThisControl.Prompt = ControlFolderListPlaceholder.Dummy.Name;
                }

                if (view == this)
                { // Root Node
                    Client.SendMsg(MSG_TYPE_GET_STORAGE);
                }
                else
                {
                    JObject req = new JObject()
                    {
                        ["path"] = ViewPath,
                        ["storage"] = view.Storage.Path
                    };

                    Client.SendMsg(req, MSG_TYPE_LIST_FOLDER);
                }
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
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                    DisconnectedCallback
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

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                    NameChangedCallback
                );

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    MSG_TYPE_FILE_MANAGER_NO_PERMISSION,
                    NoPermissionCallback
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
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                    DisconnectedCallback,
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

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                    NameChangedCallback,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    MSG_TYPE_FILE_MANAGER_NO_PERMISSION,
                    NoPermissionCallback,
                    true
                );

                ClearAndDummy();

                if (IsExpanded)
                {
                    RequestChildren();
                }

                /*
                if (IsExpanded)
                {
                    IsExpanded = false;
                }

                IsExpanded = true;
                */
            }
        }

        public override void Dispose()
        {
            ResetWith(null);
        }
    }

    public class ControlFolderListPhoneStorageItem : ControlFolderListPhoneFolderItem
    {
        public static readonly ControlFolderListPhoneStorageItem VOID = new ControlFolderListPhoneStorageItem(null, null, "", false);

        public ControlFolderListPhoneStorageItem(ControlFolderListPhoneRootItem Phone, string Name, string Path, bool HasChildFolder) : base(Phone, Phone, null, Name, true)
        {
            this.Path = Path;
            this.Storage = this;
        }
    }

    public class ControlFolderListPhoneFolderItem : ControlFolderListItemView
    {
        public ControlFolderListPhoneFolderItem(
            ControlFolderListItemView Parent,
            ControlFolderListPhoneRootItem Root,
            ControlFolderListPhoneStorageItem Storage,
            string Name,
            bool HasChildFolder
            ) : base(Parent, Root, Storage, HasChildFolder)
        {
            this.Name = Name;

            if (Parent != null)
            {
                this.Path = Parent.Path + Name + "/";
            }
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

            Root.RequestMap.Remove(Storage.Path, Path);
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

        protected override void RequestChildren()
        {
            Root.RequestChildren(this);
        }
    }

    public partial class ControlFolderList : UserControlExtension, INotifyPropertyChanged
    {
        private readonly List<ControlFolderListPhoneRootItem> Roots = new List<ControlFolderListPhoneRootItem>();

        public string CurrentPath { get; protected set; } = null;
        public IList<ControlFolderListItemView.Item> ContentItems { get; protected set; }
        public PhoneClient CurrentClient { get; protected set; } = null;
        public ControlFolderListPhoneRootItem CurrentRoot { get; protected set; } = null;
        public string CurrentStorage { get; protected set; } = null;

        private string prompt = null;
        public string Prompt
        {
            get
            {
                return prompt;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    prompt = null;
                }
                else
                {
                    prompt = value;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Prompt"));
            }
        }

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

        public void FireSelectionChanged(
            ControlFolderListPhoneRootItem Root,
            string Storage,
            PhoneClient client,
            string path,
            IList<ControlFolderListItemView.Item> items
            )
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
            this.CurrentStorage = Storage;

            if (items != null)
            {
                if (Root.NumberOfItemsPrompts.TryGetValue(items, out string p))
                {
                    Prompt = p;
                }
                else
                {
                    Prompt = null;
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPath"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentItems"));
        }

        public ControlFolderList()
        {
            InitializeComponent();
        }

        protected override void OnClosing()
        {
            ListView.ItemsSource = null;

            CurrentPath = null;
            ContentItems = null;
            CurrentClient = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentClient"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPath"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentItems"));

            foreach (ControlFolderListPhoneRootItem r in Roots)
            {
                r.Dispose();
            }

            IconUtil.ClearCache();

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
                foreach (PhoneClient c in cs)
                {
                    if (c.IsAlive)
                        Clients.Add(c);
                }
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

            ControlFolderListItemView itemView =
                CurrentRoot.RequestMap.Get(CurrentStorage, CurrentPath);
            if (itemView == null)
                return;

            itemView.IsExpanded = true;

            foreach (ControlFolderListItemView child in itemView.Children)
            {
                if (child.Name == Subfolder)
                {
                    itemView.IsSelected = false;
                    child.IsExpanded = true;
                    child.IsSelected = true;
                    break;
                }
            }
        }

        public void ToUpfolder()
        {
            if (CurrentRoot == null)
                return;

            ControlFolderListItemView itemView =
                CurrentRoot.RequestMap.Get(CurrentStorage, CurrentPath);
            if (itemView == null)
                return;

            ControlFolderListItemView parent = itemView.Parent;

            if (parent != null)
            {
                itemView.IsSelected = false;
                parent.IsExpanded = true;
                parent.IsSelected = true;
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
