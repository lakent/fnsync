using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FnSync
{
    class JsonConfigFile: IEnumerable<KeyValuePair<string, JToken>>
    {
        protected readonly String ConfigFile;
        private JObject Config = null;

        private bool batchUpdating = false;
        public bool BatchUpdating
        {
            get { return batchUpdating; }
            set
            {
                if (value != batchUpdating)
                {
                    batchUpdating = value;
                    if (!value)
                    {
                        Update();
                    }
                }
            }
        }

        public JToken this[string key]
        {
            get
            {
                return this.Config[key];
            }
            set
            {
                if (value == null)
                {
                    this.Config.Remove(key);
                }
                else
                {
                    if (!this.Config.ContainsKey(key) || !JToken.DeepEquals(this.Config[key], value))
                    {
                        this.Config[key] = value;
                        Update();
                    }
                }
            }
        }

        public JsonConfigFile(string file): this(file, false) { }

        protected JsonConfigFile(string file, bool LoadLater)
        {
            this.ConfigFile = file;

            if (!LoadLater)
            {
                Load();
            }
        }

        protected void Load()
        {
            if (File.Exists(ConfigFile))
            {
                this.Config = JObject.Parse(ReadFromFile(ConfigFile));
            }
            else
            {
                this.Config = new JObject();
            }
        }

        protected virtual string ReadFromFile(string file)
        { 
            return File.ReadAllText(file, Encoding.UTF8);
        }

        public virtual void Update()
        {
            File.WriteAllText(ConfigFile, Config.ToString(Newtonsoft.Json.Formatting.None));
        }

        public void RemoveFile()
        {
            if (File.Exists(ConfigFile))
            {
                File.Delete(ConfigFile);
            }
        }

        public string OptString(string key, string defval)
        {
            return Config.OptString(key, defval);
        }

        public bool OptBool (string key, bool defval)
        {
            return Config.OptBool(key, defval);
        }

        public override string ToString()
        {
            return Config.ToString();
        }

        public string ToStringNoFormat()
        {
            return Config.ToString(Newtonsoft.Json.Formatting.None);
        }

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator()
        {
            return this.Config.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
