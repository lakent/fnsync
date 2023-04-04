using FnSync.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace FnSync
{
    class MainConfig : JsonConfigFile, INotifyPropertyChanged
    {
        public static readonly MainConfig Config = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public readonly Lazy<Dictionary<string, TrayDoubleClickChoice>> trayDoubleClickChoiceMap = new(() =>
            new()
            {
                {
                    "OpenMainWindow",
                    new TrayDoubleClickChoice((string)Application.Current.FindResource("OpenMainWindow"), "OpenMainWindow")
                },
                {
                    "FileManager",
                    new TrayDoubleClickChoice((string)Application.Current.FindResource("FileManager"), "FileManager")
                },
                {
                    "TriggerClipboardSync",
                    new TrayDoubleClickChoice((string)Application.Current.FindResource("TriggerClipboardSync"), "TriggerClipboardSync")
                },
            }
        );
        public ICollection<TrayDoubleClickChoice> TrayDoubleClickChoices => trayDoubleClickChoiceMap.Value.Values;

        private MainConfig() : base(SavedPhones.ConfigRoot + "\\main.config")
        {
            Dictionary<string, JToken> DefaultValues = new()
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
                ["SaveFileAutomatically"] = false,
                ["FileDefaultSaveFolder"] = "",
                ["TrayDoubleClickAction"] = "OpenMainWindow",
                ["TriggerClipboardSync"] = "Ctrl + Shift + D",

                // Don't assign null
            };

            foreach (KeyValuePair<string, JToken> item in DefaultValues)
            {
                if (this[item.Key] == null)
                {
                    this[item.Key] = item.Value;
                }
            }

            this.trayDoubleClickAction = trayDoubleClickChoiceMap.Value[
                this.OptString("TrayDoubleClickAction") ?? "OpenMainWindow"
                ];

            this.triggerClipboardSync = new(this.OptString("TriggerClipboardSync") ?? "Ctrl + Shift + D");
        }

        public string ThisId
        {
            get => (string)this["ThisId"]!;
            set
            {
                this["ThisId"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThisId)));
            }
        }

        public bool ConnectOnStartup
        {
            get => (bool)this["ConnectOnStartup"]!;
            set
            {
                this["ConnectOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectOnStartup)));
            }

        }
        public bool HideOnStartup
        {
            get => (bool)this["HideOnStartup"]!;
            set
            {
                this["HideOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideOnStartup)));
            }
        }
        public bool HideNotificationOnStartup
        {
            get => (bool)this["HideNotificationOnStartup"]!;
            set
            {
                this["HideNotificationOnStartup"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideNotificationOnStartup)));
            }
        }
        public bool DontToastConnected
        {
            get => (bool)this["DontToastConnected"]!;
            set
            {
                this["DontToastConnected"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DontToastConnected)));
            }
        }
        public bool ClipboardSync
        {
            get => (bool)this["ClipboardSync"]!;
            set
            {
                ClipboardManager.Singleton.MonitorClipboardOn = value;
                this["ClipboardSync"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClipboardSync)));
            }
        }

        public bool TextCastAutoCopy
        {
            get => (bool)this["TextCastAutoCopy"]!;
            set
            {
                this["TextCastAutoCopy"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextCastAutoCopy)));
            }
        }

        public int FixedListenPort
        {
            get => (int)this["FixedListenPort"]!;
            set
            {
                this["FixedListenPort"] = Math.Min(Math.Max(0, value), 65535);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FixedListenPort)));
            }
        }

        public string AdditionalIPs
        {
            get => (string)this["AdditionalIPs"]!;
            set
            {
                this["AdditionalIPs"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdditionalIPs)));
            }
        }

        public bool SaveFileAutomatically
        {
            get => (bool)this["SaveFileAutomatically"]!;
            set
            {
                this["SaveFileAutomatically"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveFileAutomatically)));
            }
        }

        public string FileDefaultSaveFolder
        {
            get => (string)this["FileDefaultSaveFolder"]!;
            set
            {
                this["FileDefaultSaveFolder"] = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileDefaultSaveFolder)));
            }
        }

        private TrayDoubleClickChoice trayDoubleClickAction;
        public TrayDoubleClickChoice TrayDoubleClickAction
        {
            get => trayDoubleClickAction;
            set
            {
                this.trayDoubleClickAction = value;
                this["TrayDoubleClickAction"] = value.Value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrayDoubleClickAction)));
            }
        }

        private Hotkey triggerClipboardSync;
        public Hotkey TriggerClipboardSync
        {
            get => triggerClipboardSync;
            set
            {
                this.triggerClipboardSync = value;
                this["TriggerClipboardSync"] = value.ToString();
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TriggerClipboardSync)));
            }
        }
    }
}

