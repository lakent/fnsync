using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace FnSync
{
    class MainConfig : JsonConfigFile, INotifyPropertyChanged
    {
        public static readonly MainConfig Config = new MainConfig();

        public event PropertyChangedEventHandler PropertyChanged;

        private MainConfig() : base(SavedPhones.ConfigRoot + "\\main.config")
        {
            Dictionary<string, JToken> DefaultValues = new Dictionary<string, JToken>()
            {
                ["ThisId"] = "",
                ["ConnectOnStartup"] = true,
                ["HideOnStartup"] = true,
                ["HideNotificationOnStartup"] = false,
                ["DontToastConnected"] = false,
                ["ClipboardSync"] = true,
                ["TextCastAutoCopy"] = true,
                ["FixedListenPort"] = 0,
                ["AdditionalIPs"] = "",
                ["FileDefaultSaveFolder"] = "",

                // Don't assign null
            };

            foreach (KeyValuePair<string, JToken> item in DefaultValues)
            {
                if (this[item.Key] == null)
                {
                    this[item.Key] = item.Value;
                }
            }
        }

        public string ThisId
        {
            get => (string)this["ThisId"];
            set
            {
                this["ThisId"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ThisId"));
            }
        }

        public bool ConnectOnStartup
        {
            get
            => (bool)this["ConnectOnStartup"];
            set
            {
                this["ConnectOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ConnectOnStartup"));
            }

        }
        public bool HideOnStartup
        {
            get
            => (bool)this["HideOnStartup"];
            set
            {
                this["HideOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HideOnStartup"));
            }
        }
        public bool HideNotificationOnStartup
        {
            get
            => (bool)this["HideNotificationOnStartup"];
            set
            {
                this["HideNotificationOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HideNotificationOnStartup"));
            }
        }
        public bool DontToastConnected
        {
            get => (bool)this["DontToastConnected"];
            set
            {
                this["DontToastConnected"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DontToastConnected"));
            }
        }
        public bool ClipboardSync
        {
            get => (bool)this["ClipboardSync"];
            set
            {
                ClipboardManager.Singleton.MonitorClipboardOn = value;
                this["ClipboardSync"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ClipboardSync"));
            }
        }

        public bool TextCastAutoCopy
        {
            get => (bool)this["TextCastAutoCopy"];
            set
            {
                this["TextCastAutoCopy"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TextCastAutoCopy"));
            }
        }

        public int FixedListenPort
        {
            get => (int)this["FixedListenPort"];
            set
            {
                this["FixedListenPort"] = Math.Min(Math.Max(0, value), 65535);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FixedListenPort"));
            }
        }

        public string AdditionalIPs
        {
            get => (string)this["AdditionalIPs"];
            set
            {
                this["AdditionalIPs"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AdditionalIPs"));
            }
        }

        public string FileDefaultSaveFolder
        {
            get => (string)this["FileDefaultSaveFolder"];
            set
            {
                this["FileDefaultSaveFolder"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FileDefaultSaveFolder"));
            }
        }

    }
}

