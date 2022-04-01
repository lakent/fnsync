using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class MainConfig: JsonConfigFile
    {
        public static readonly MainConfig Config = new MainConfig();

        private MainConfig(): base(SavedPhones.ConfigRoot + "\\main.config")
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

                // Don't assign null
            };

            foreach(var item in DefaultValues)
            {
                if( this[item.Key] == null)
                {
                    this[item.Key] = item.Value;
                }
            }
        }

        public string ThisId { 
            get {
                return (string)this["ThisId"];
            }
            set
            {
                this["ThisId"] = value;
            }
        }

        public bool ConnectOnStartup
        {
            get
            {
                return (bool)this["ConnectOnStartup"];
            }
            set
            {
                this["ConnectOnStartup"] = value;
            }
        }
        public bool HideOnStartup
        {
            get
            {
                return (bool)this["HideOnStartup"];
            }
            set
            {
                this["HideOnStartup"] = value;
            }
        }
        public bool HideNotificationOnStartup
        {
            get
            {
                return (bool)this["HideNotificationOnStartup"];
            }
            set
            {
                this["HideNotificationOnStartup"] = value;
            }
        }
        public bool DontToastConnected
        {
            get
            {
                return (bool)this["DontToastConnected"];
            }
            set
            {
                this["DontToastConnected"] = value;
            }
        }
        public bool ClipboardSync
        {
            get
            {
                return (bool)this["ClipboardSync"];
            }
            set
            {
                this["ClipboardSync"] = value;
            }
        }

        public bool TextCastAutoCopy
        {
            get
            {
                return (bool)this["TextCastAutoCopy"];
            }
            set
            {
                this["TextCastAutoCopy"] = value;
            }
        }

        public int FixedListenPort
        {
            get
            {
                return (int)this["FixedListenPort"];
            }
            set
            {
                this["FixedListenPort"] = Math.Min(Math.Max(0, value), 65535);
            }
        }

        public string AdditionalIPs { 
            get {
                return (string)this["AdditionalIPs"];
            }
            set
            {
                this["AdditionalIPs"] = value;
            }
        }
    }
}
