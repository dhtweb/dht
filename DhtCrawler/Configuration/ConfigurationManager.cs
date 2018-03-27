using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IList<object> GetList(string key)
        {
            if (ContainsKey(key))
                return (IList<object>)this[key];
            return new object[0];
        }

        public ConfigurationManager GetSection(string key)
        {
            if (ContainsKey(key))
            {
                if (this[key] is ConfigurationManager manager)
                    return manager;
                var section = (JObject)this[key];
                manager = section.ToObject<ConfigurationManager>();
                this[key] = manager;
                foreach (var k in manager.Keys.ToArray())
                {
                    var val = manager[k];
                    if (val is JArray)
                    {
                        var list = new List<object>();
                        foreach (var it in (JArray)val)
                        {
                            list.Add(it.ToObject<Dictionary<string, object>>());
                        }
                        manager[k] = list;
                    }
                }
                return manager;
            }
            return new ConfigurationManager();
        }
    }
}
