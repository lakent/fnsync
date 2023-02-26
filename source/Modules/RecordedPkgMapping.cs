using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class RecordedPkgMapping : JsonConfigFile
    {
        private const string CONFIG_FILE = "RecordedPkgMapping.txt";

        public static readonly RecordedPkgMapping Singleton = new();

        private RecordedPkgMapping() : base(SavedPhones.ConfigRoot + CONFIG_FILE)
        {
        }

        public void Record(JObject msg)
        {
            string? pkgname = (string?)msg["pkgname"];
            string? appName = msg.OptString("appname", null);

            if (pkgname == null || appName == null)
            {
                return;
            }

            this[pkgname] = appName;
        }
    }
}
