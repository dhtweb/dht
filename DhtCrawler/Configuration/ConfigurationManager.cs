using System;
using System.Collections.Generic;
using System.IO;
using DhtCrawler.Common;
using Newtonsoft.Json.Linq;

namespace DhtCrawler.Configuration
{
    public class ConfigurationManager : Dictionary<string, object>
    {
        public static ConfigurationManager Default
        {
            get
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.json");
                if (!File.Exists(path))
                    return new ConfigurationManager();
                return File.ReadAllText(path).ToObjectFromJson<ConfigurationManager>();
            }
        }

        public bool GetBool(string key, bool defVal = false)
        {
            if (ContainsKey(key))
            {
                return Convert.ToBoolean(this[key]);
            }
            return defVal;
        }

        public int GetInt(string key, int defVal = 0)
        {
            if (ContainsKey(key))
            {
                return Convert.ToInt32(this[key]);
            }
            return defVal;
        }

        public string GetString(string key)
        {
            if (ContainsKey(key))
                return (string)this[key];
            return string.Empty;
        }

        public ConfigurationManager GetSection(string key)
        {
            if (ContainsKey(key))
            {
                if (this[key] is ConfigurationManager manager)
                    return manager;
                var section = (JObject)this[key];
                manager = new ConfigurationManager();
                foreach (var kv in section)
                {
                    switch (kv.Value.Type)
                    {
                        case JTokenType.Integer:
                            manager.Add(kv.Key, kv.Value.Value<int>());
                            break;
                        case JTokenType.String:
                            manager.Add(kv.Key, kv.Value.Value<string>());
                            break;
                    }
                }
                this[key] = manager;
                return manager;
            }
            return new ConfigurationManager();
        }
    }
}
