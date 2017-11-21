using Newtonsoft.Json;

namespace DhtCrawler.Common
{
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings DefaultSetting = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
        public static string ToJson<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj, DefaultSetting);
        }

        public static T ToObjectFromJson<T>(this string json)
        {
            return string.IsNullOrWhiteSpace(json) ? default(T) : JsonConvert.DeserializeObject<T>(json);
        }
    }
}
