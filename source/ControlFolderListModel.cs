using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FnSync.Model.ControlFolderList
{
    public class ItemTypeJsonConverter : JsonConverter<ItemType>
    {
        public override ItemType ReadJson(JsonReader reader, Type objectType, ItemType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = reader.Value as string;

            if (value == "file")
            {
                return ItemType.File;
            }
            else if (value == "dir")
            {
                return ItemType.Directory;
            }
            else if (value == "storage")
            {
                return ItemType.Storage;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public override void WriteJson(JsonWriter writer, ItemType value, JsonSerializer serializer)
        {
            switch (value)
            {
                case ItemType.File:
                    writer.WriteValue("file");
                    break;

                case ItemType.Directory:
                    writer.WriteValue("dir");
                    break;

                case ItemType.Storage:
                    writer.WriteValue("storage");
                    break;

                default:
                    writer.WriteNull();
                    break;
            }
        }
    }

    [JsonConverter(typeof(ItemTypeJsonConverter))]
    public enum ItemType
    {
        File,
        Directory,
        Storage
    }

    public class PhoneFileInfo
    {
        /* All lower case for easily deserializing */
        public ItemType type { get; set; }
        public string path { get; set; } /* Relative Path */
        public string name { get; set; }
        public bool haschild { get; set; }
        public long size { get; set; }
        public long last { get; set; }

        public static int Comparison(PhoneFileInfo x, PhoneFileInfo y)
        {
            /* File Order */

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
                return x.type == ItemType.File ? 1 : -1;
            }
            else
            {
                int NameRelaton = x.name.CompareTo(y.name);
                if (NameRelaton != 0)
                {
                    return NameRelaton;
                }
                else
                {
                    return (int)(x.size - y.size);
                }
            }
        }

        public static void ImcrementalUpdate(IList<PhoneFileInfo> Target, IList<PhoneFileInfo> From)
        {
            if (!From.Any())
            {
                Target.Clear();
            }
            else
            {
                int ti = 0; /* `Target` Index */
                int fi = 0; /* `From` Index */

                while (ti < Target.Count && fi < From.Count)
                {
                    PhoneFileInfo t = Target[ti];
                    PhoneFileInfo f = From[fi];

                    int comparing = PhoneFileInfo.Comparison(t, f);
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

    public interface IFileModel : INotifyPropertyChanged
    {
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
        bool IsRequesting { get; }
        string Name { get; }
        string Path { get; }
    }

    public class Placeholder : IFileModel
    {
        /* Placeholder for non-file item, such as Loading, No Permission and Device Offline  */
        public static readonly Placeholder Dummy =
            new Placeholder((string)Application.Current.FindResource("LoadingContent"));
        public static readonly Placeholder NoPermission =
            new Placeholder((string)Application.Current.FindResource("NoFilePermission"));
        public static readonly Placeholder PhoneOffline =
            new Placeholder((string)Application.Current.FindResource("DeviceIsOffline"));

        public event PropertyChangedEventHandler PropertyChanged;

        public IList<PhoneFileInfo> AllChildrenInfo { get; } = new List<PhoneFileInfo>();

        public bool IsExpanded { get => false; set { } }
        public bool IsSelected { get => false; set { } }
        public bool IsRequesting => false;

        public string Name { get; }
        public string Path => "";


        private Placeholder(string name)
        {
            this.Name = name;
        }
    }

    public abstract class FileBaseModel : IFileModel, IDisposable
    {
        public const string MSG_TYPE_GET_STORAGE = "get_storage";
        public const string MSG_TYPE_STORAGE_LIST = "storage_list";
        public const string MSG_TYPE_FOLDER_CONTENT_CHANGED = "file_content_changed";
        public const string MSG_TYPE_FILE_RENAME = "file_rename";
        public const string MSG_TYPE_FILE_DELETE = "file_delete";
        public const string MSG_TYPE_LIST_FOLDER = "list_folder";
        public const string MSG_TYPE_FOLDER_CONTENT = "folder_content";
        public const string MSG_TYPE_FILE_MANAGER_NO_PERMISSION = "file_manager_no_permission";
        public const string MSG_TYPE_REFRESH_MEDIA_STORE = "refresh_media_store";

        internal bool NeedRequested = true;

        public ObservableCollection<IFileModel> Children { get; } = new ObservableCollection<IFileModel>();
        public ObservableCollection<PhoneFileInfo> AllChildrenInfo { get; } = new ObservableCollection<PhoneFileInfo>();

        public bool Dummied => Children.Count == 1 && ReferenceEquals(Children[0], Placeholder.Dummy);
        private bool isExpanded = false;
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (Children.Count == 0)
                {
                    return;
                }

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

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
                }
            }
        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    if (value)
                    {
                        Root.Selected = this;
                        if (Dummied || NeedRequested)
                        {
                            RequestChildren();
                        }
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                }
            }
        }

        private bool isRequesting = false;
        public bool IsRequesting
        {
            get => isRequesting;
            private set
            {
                if (isRequesting != value)
                {
                    isRequesting = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRequesting"));
                }
            }
        }

        public string Name { get; protected set; }
        public string Path { get; protected set; }
        public int FileCount { get; protected set; } = 0;
        public int FolderCount { get; protected set; } = 0;
        public FileBaseModel Parent { get; protected set; }
        public RootModel Root { get; protected set; }
        public abstract StorageModel Storage { get; protected set; }

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
            Children.Add(Placeholder.Dummy);
            AllChildrenInfo.Clear();
        }

        protected async void RequestChildren()
        {
            PhoneClient client = Root.Client;
            if (!client.IsAlive)
            {
                return;
            }

            if (IsRequesting)
            {
                return;
            }

            IsRequesting = true;
            NeedRequested = true;

            if (Root == this)
            { // Root Node
                try
                {
                    object msgObj = await client.OneShot(
                        null, MSG_TYPE_GET_STORAGE, null,
                        MSG_TYPE_STORAGE_LIST, 60000
                        );

                    ProcessStorageList(msgObj);
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {

                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    // Root.Client = e.Current;
                }
                catch (TimeoutException)
                {
                }

            }
            else
            {
                JObject req = new JObject()
                {
                    ["path"] = Path,
                    ["storage"] = Storage.Path
                };

                try
                {
                    object msgObj = await client.OneShot(
                        req, MSG_TYPE_LIST_FOLDER, null,
                        MSG_TYPE_FOLDER_CONTENT, 60000
                        );

                    ProcessFolderContent(msgObj);
                }
                catch (PhoneMessageCenter.DisconnectedException)
                {

                }
                catch (PhoneMessageCenter.OldClientHolderException e)
                {
                    // Root.Client = e.Current;
                }
                catch (TimeoutException)
                {
                }
            }

            IsRequesting = false;
            NeedRequested = false;

            Root.ModelCache[Path] = new WeakReference<FileBaseModel>(this);
        }

        protected void ProcessStorageList(object msgObject)
        {
            if (!(msgObject is JObject msg))
            {
                return;
            }

            JArray list = (JArray)msg["storage"];
            List<PhoneFileInfo> storage = list.ToObject<List<PhoneFileInfo>>();

            LoadChildrenItems(storage);
        }

        protected void ProcessFolderContent(object msgObject)
        {
            if (!(msgObject is JObject msg))
            {
                return;
            }

            JArray list = (JArray)msg["files"];
            List<PhoneFileInfo> folders = list.ToObject<List<PhoneFileInfo>>();

            folders.Sort(PhoneFileInfo.Comparison);
            LoadChildrenItems(folders);
        }

        public void LoadChildrenItems(IList<PhoneFileInfo> FolderInfoList)
        {
            Children.Clear();

            FolderCount = 0;
            FileCount = 0;

            foreach (PhoneFileInfo Info in FolderInfoList)
            {
                switch (Info.type)
                {
                    case ItemType.Directory:
                        Children.Add(
                            new FolderModel(
                                this, Root, Storage,
                                Info.name, Info.path, Info.haschild
                            )
                            );
                        ++FolderCount;
                        break;

                    case ItemType.Storage:
                        Children.Add(new StorageModel(Root, Info.name, Info.path, Info.haschild));
                        ++FolderCount;
                        break;

                    case ItemType.File:
                        ++FileCount;
                        break;

                    default:
                        break;
                }
            }

            PhoneFileInfo.ImcrementalUpdate(AllChildrenInfo, FolderInfoList);

            if (Children.Count == 1)
            {
                Children[0].IsExpanded = true;
            }
        }

        public abstract void Dispose();

        public abstract void Refresh();

        public FileBaseModel(
            FileBaseModel Parent, RootModel Root, StorageModel Storage, bool NeedDummy
            )
        {
            this.Parent = Parent;
            this.Root = Root;
            this.Storage = Storage;

            if (NeedDummy)
            {
                ClearAndDummy();
            }
        }
    }

    public class RootModel : FileBaseModel
    {
        public PhoneClient Client { get; internal set; } = null;

        private readonly WeakReference<FileBaseModel> selected = new WeakReference<FileBaseModel>(null);
        internal FileBaseModel Selected
        {
            get => selected.TryGetTarget(out FileBaseModel model) ? model : null;
            set => selected.SetTarget(value);
        }

        public override StorageModel Storage
        {
            get => throw new NotSupportedException();
            protected set { /* Nothing */ }
        }

        internal readonly Dictionary<string, WeakReference<FileBaseModel>> ModelCache =
            new Dictionary<string, WeakReference<FileBaseModel>>();

        public RootModel(PhoneClient client) :
            base(null, null, null, true)
        {
            base.Root = this;
            this.Name = client.Name;
            this.Path = "";
            ResetWith(client);
            this.IsExpanded = true;
        }

        private void ReconnectedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            OnClientNameChanged(client.Name);
            ResetWith(client);
        }

        private void DisconnectedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            Children.Clear();
            Children.Add(Placeholder.PhoneOffline);
        }

        private void ContentChangedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg))
                return;

            FileBaseModel Selected = this.Selected;

            if (Selected == null)
            {
                return;
            }

            string path = (string)msg["folder"];
            string storage = (string)msg["storage"];

            if (Selected.Storage.Path != storage)
            {
                return;
            }

            if (Selected.Path == path)
            {
                Selected.Refresh();
            }
            else
            {
                if (!Root.ModelCache.TryGetValue(path, out WeakReference<FileBaseModel> modelRef) ||
                    !modelRef.TryGetTarget(out FileBaseModel model)
                    )
                {
                    return;
                }

                model.NeedRequested = true;
                //model.Refresh();
            }
        }

        private void OnClientNameChanged(string NewName)
        {
            this.Name = NewName;
            CallPropertyChanged(this, new PropertyChangedEventArgs("Name"));
        }

        private void ClientNameChangedCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is string name))
            {
                return;
            }

            OnClientNameChanged(name);
        }

        private void NoPermissionCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            Children.Clear();
            Children.Add(Placeholder.NoPermission);
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

        private void ResetWith(PhoneClient client)
        {
            if (this.Client != null)
            {
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
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                    ClientNameChangedCallback
                );

                PhoneMessageCenter.Singleton.Unregister(
                    Client.Id,
                    MSG_TYPE_FILE_MANAGER_NO_PERMISSION,
                    NoPermissionCallback
                );
            }

            this.Client = client;
            ModelCache.Clear();

            if (this.Client != null)
            {
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
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                    ClientNameChangedCallback,
                    true
                );

                PhoneMessageCenter.Singleton.Register(
                    Client.Id,
                    MSG_TYPE_FILE_MANAGER_NO_PERMISSION,
                    NoPermissionCallback,
                    true
                );

                ClearAndDummy();

                RequestChildren();
            }
        }

        public override void Dispose()
        {
            ResetWith(null);
        }
    }

    public class StorageModel : FolderModel
    {
        public StorageModel(RootModel Phone, string Name, string Path, bool HasChildFolder) :
            base(Phone, Phone, null, Name, Path, HasChildFolder)
        {
            this.Path = Path.AppendIfNotEnding("/");
            this.Storage = this;
        }

        public override bool Equals(object obj)
        {
            return obj is StorageModel other &&

                this.Root.Client.Id == other.Root.Client.Id &&
                this.Path == other.Path;
        }

        public override int GetHashCode()
        {
            return this.Root.Client.Id.GetHashCode() ^ this.Path.GetHashCode();
        }
    }

    public class FolderModel : FileBaseModel
    {
        public override StorageModel Storage { get; protected set; }

        public FolderModel(
            FileBaseModel Parent, RootModel Root,
            StorageModel Storage, string Name, string Path,
            bool HasChildFolder
            ) :
            base(Parent, Root, Storage, HasChildFolder)
        {
            this.Name = Name;

            if (Parent != null)
            {
                this.Path = Parent.Path.AppendIfNotEnding("/") + Path.AppendIfNotEnding("/");
            }
        }

        public override void Dispose()
        {
            /*
            foreach (IFolderModel child in Children)
            {
                if (child is FolderBaseModel c)
                {
                    c.Dispose();
                }
            }

            ClearAndDummy();
            */
        }

        public override void Refresh()
        {
            this.Dispose();
            RequestChildren();
        }
    }
}

