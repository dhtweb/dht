using Newtonsoft.Json;

namespace DhtCrawler.Common
{
    public static class JsonHelper
    {
        public static string ToJson<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }
}
