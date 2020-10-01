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
        private MainConfig(): base(SavedPhones.ConfigRoot + "\\main.config") { }

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

    }
}
