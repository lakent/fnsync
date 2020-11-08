using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace FnSync
{
    public class SavedPhones : Dictionary<String, SavedPhones.Phone>
    {
        public static readonly string ConfigRoot = GetConfigRootFolder();
        public static readonly string DevicesSubfolder = GetDeviceSubfolder();

        static string GetConfigRootFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FnSync\\";
            Directory.CreateDirectory(path);
            return path;
        }

        static string GetDeviceSubfolder()
        {
            string path = ConfigRoot + "Devices\\";
            Directory.CreateDirectory(path);
            return path;
        }

        static string GetDeviceFolder(string id)
        {
            return $"{DevicesSubfolder}{id}\\";
        }

        public static readonly SavedPhones Singleton = new SavedPhones();

        public class Phone : JsonConfigFile, IDisposable
        {
            public static string GetConfigFile(String id)
            {
                return GetDeviceFolder(id) + CONFIG;
            }

            private const String CONFIG = "config.txt";

            public readonly String Folder;
            private readonly EncryptionManager Encryption;

            public string Id { get; private set; }

            public HistoryWriter NotificationWriter { get; protected set; } = null;
            public SmallFileCache SmallFiles { get; protected set; } = null;

            private string code = null;
            public string Code
            {
                get { return code; }
                set
                {
                    code = value;
                    this["code"] = value;
                }
            }

            private string name = null;
            public string Name
            {
                get { return name; }
                set
                {
                    name = value;
                    this["name"] = value;

                    if (!BatchUpdating)
                    {
                        PhoneMessageCenter.Singleton.Raise(
                            Id,
                            PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                            value,
                            AlivePhones.Singleton[Id]
                        );
                    }
                }
            }

            private string lastIp = null;
            public string LastIp
            {
                get { return lastIp; }
                set
                {
                    lastIp = value;
                    this["latest_ip"] = value;
                }
            }

            private bool lockScreenOnDisconnect = false;
            public bool LockScreenOnDisconnect
            {
                get { return lockScreenOnDisconnect; }
                set
                {
                    lockScreenOnDisconnect = value;
                    this["lock_screen_on_disconnect"] = value;
                }
            }

            public DateTime FirstConnectedTime { get; private set; }

            public bool Alive => AlivePhones.Singleton.Contains(Id) && AlivePhones.Singleton[Id].IsAlive;

            protected override string ReadFromFile(string file)
            {
                byte[] bytes = File.ReadAllBytes(file);
                return Encryption.DecryptToString(bytes, 0, bytes.Length, false);
            }

            public Phone(string id) : base(GetConfigFile(id), true)
            {
                this.Id = id;

                try
                {
                    Folder = GetDeviceFolder(id);
                    Encryption = new EncryptionManager(id);

                    Load();

                    this.FirstConnectedTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(this["created_at"])).LocalDateTime;
                    this.name = OptString("name", "(Unknown)");
                    this.code = (string)this["code"];
                    this.lockScreenOnDisconnect = OptBool("lock_screen_on_disconnect", false);

                    if (String.IsNullOrEmpty(this.code))
                    {
                        throw new ArgumentException();
                    }

                    this.lastIp = OptString("latest_ip", null);
                }
                catch (Exception e)
                {
                    DisposeAndRemove();
                    throw e;
                }
            }

            public Phone(string id, string code, string name, String ip) : base(GetConfigFile(id), true)
            {
                if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(code) || String.IsNullOrEmpty(ip))
                {
                    throw new ArgumentException();
                }

                this.Id = id;

                Folder = GetDeviceFolder(id);
                Directory.CreateDirectory(Folder);

                Encryption = new EncryptionManager(id);

                Load();

                this.FirstConnectedTime = DateTime.Now;
                this["created_at"] = ((DateTimeOffset)this.FirstConnectedTime).ToUnixTimeMilliseconds();

                BatchUpdating = true;
                this.Code = code;
                this.Name = name ?? "(Unknown)";
                this.LastIp = ip;
                this.LockScreenOnDisconnect = false;
                BatchUpdating = false;

                Update();
            }

            public void InitNotificationLogger()
            {
                if (NotificationWriter == null)
                {
                    NotificationWriter = new HistoryWriter(Id);
                }
            }
            public void InitSmallFileCache()
            {
                if (SmallFiles == null)
                {
                    SmallFiles = new SmallFileCache(Path.GetTempPath() + Id + "\\");
                }
            }

            public override void Update()
            {
                byte[] bytes = Encryption.EncryptString(ToStringNoFormat());
                Utils.WriteAllBytes(ConfigFile, bytes);
            }

            public void DisposeAndRemove()
            {
                PhoneMessageCenter.Singleton.Raise(
                    Id,
                    PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                    null,
                    null
                );

                Dispose();

                try
                {
                    Directory.Delete(Folder, true);
                }
                catch (Exception e) { }
            }

            public void Dispose()
            {
                NotificationWriter?.Dispose();
                SmallFiles?.Dispose();
            }
        }

        public class HistoryWriter : IDisposable
        {
            private readonly String Id;
            private StreamWriter writer = null;
            private string DateString = null;

            private readonly string Folder = null;

            private string GetHistoryFilePath()
            {
                Directory.CreateDirectory(Folder);
                return $"{Folder}{DateString}.log";
            }

            private void PrepareWriter(long ThisTime)
            {
                string ThisDateString = DateTimeOffset.FromUnixTimeMilliseconds(ThisTime).LocalDateTime.ToString("yyyy_MM_dd");
                if (!ThisDateString.Equals(DateString) || writer == null)
                {
                    DateString = ThisDateString;
                    writer?.Close();

                    writer = new StreamWriter(
                        new FileStream(GetHistoryFilePath(), FileMode.Append, FileAccess.Write, FileShare.Read),
                        Encoding.UTF8
                        )
                    {
                        AutoFlush = true
                    };
                }
            }

            public HistoryWriter(string id)
            {
                this.Id = id;
                Folder = $"{GetDeviceFolder(id)}history\\";

                PhoneMessageCenter.Singleton.Register(
                    id,
                    PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION,
                    LogWriterDelegate,
                    false,
                    10
                    );

                PhoneMessageCenter.Singleton.Register(
                    id,
                    Casting.MSG_TYPE_TEXT_CAST,
                    LogWriterDelegate,
                    false,
                    10
                    );

            }

            public void Write(JObject msg)
            {
                msg.Remove("icon");
                //notification.Remove(PhoneClient.MSG_TYPE_KEY);
                msg.Remove(PhoneClient.MSG_ID_KEY);

                string item = msg.ToString(Newtonsoft.Json.Formatting.None);

                PrepareWriter((long)msg["time"]);
                writer?.WriteLine(item);
            }

            private void LogWriterDelegate(string id, string msgType, object msgObject, PhoneClient client)
            {
                if (!(msgObject is JObject msg)) return;
                Write(msg);
            }

            public void Dispose()
            {
                PhoneMessageCenter.Singleton.Unregister(
                    this.Id,
                    PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION,
                    LogWriterDelegate
                    );
                PhoneMessageCenter.Singleton.Unregister(
                    this.Id,
                    Casting.MSG_TYPE_TEXT_CAST,
                    LogWriterDelegate
                    );

                writer?.Close();
                writer = null;
            }
        }

        public class HistoryReader : IDisposable
        {
            private readonly String Id;
            private readonly string Folder = null;

            private string Current = null;
            private StreamReader reader = null;

            public HistoryReader(string id)
            {
                this.Id = id;
                Folder = $"{GetDeviceFolder(id)}history\\";
                Directory.CreateDirectory(Folder);
            }

            public List<String> AllDates
            {
                get
                {
                    string[] fulls = Directory.GetFiles(Folder, "*.log");
                    List<string> ret = new List<string>(fulls.Length);
                    foreach (string item in fulls)
                    {
                        ret.Add(Path.GetFileName(item));
                    }

                    return ret;
                }
            }

            public void SetCurrent(string file)
            {
                Current = $"{Folder}{file}";
                reader?.Close();
                reader = new StreamReader(
                    new FileStream(
                        Current,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Write
                        ),
                    Encoding.UTF8
                    );
            }

            public JObject ReadLine()
            {
                string line = reader?.ReadLine();
                if (line == null)
                {
                    return null;
                }

                JObject ret = JObject.Parse(line);

                return ret;
            }

            public void Clear()
            {
                string[] fulls = Directory.GetFiles(Folder, "*.log");
                foreach (string item in fulls)
                {
                    try
                    {
                        File.Delete(item);
                    }
                    catch (Exception e) { }
                }
            }

            public void Dispose()
            {
                reader?.Close();
                reader = null;
            }
        }

        public ObservableCollection<Phone> PhoneList { get; private set; }

        private SavedPhones() : base()
        {
            try
            {
                string[] subds = Directory.GetDirectories(DevicesSubfolder, "*", SearchOption.TopDirectoryOnly);

                foreach (var item in subds)
                {
                    try
                    {
                        string id = Path.GetFileName(item);
                        Phone phone = new Phone(id);
                        base.Add(id, phone);
                    }
                    catch (Exception e) { }
                }

                PhoneList = new ObservableCollection<Phone>(base.Values);
            }
            catch (Exception)
            {
                PhoneList = new ObservableCollection<Phone>();
            }
        }

        public new void Add(String key, Phone value)
        {
            throw new NotSupportedException();
        }

        public new Phone this[string key]
        {
            get
            {
                if (base.ContainsKey(key))
                {
                    return base[key];
                }
                else
                {
                    return null;
                }
            }
        }

        public void AddOrUpdate(string id, string code, string name, String ip)
        {
            if (this.ContainsKey(id))
            {
                Phone phone = this[id];

                phone.BatchUpdating = true;
                phone.Code = code;
                phone.Name = name;
                phone.LastIp = ip;
                phone.BatchUpdating = false;
            }
            else
            {
                Phone New = new Phone(id, code, name, ip);
                base[id] = New;

                PhoneList.Add(New);
            }
        }

        public new void Remove(String key)
        {
            this.Remove(this[key]);
        }

        public void Remove(Phone phone)
        {
            if (phone == null)
            {
                return;
            }

            base.Remove(phone.Id);

            PhoneList.Remove(phone);

            phone.DisposeAndRemove();
        }

        public void DisposeAll()
        {
            foreach(Phone phone in this.Values)
            {
                phone.Dispose();
            }
        }
    }
}
