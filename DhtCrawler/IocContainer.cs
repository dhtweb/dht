using System;
using System.Collections.Concurrent;

namespace DhtCrawler
{
    public static class IocContainer
    {
        private static readonly ConcurrentDictionary<Type, object> Instances = new ConcurrentDictionary<Type, object>();

        public static bool RegisterType<T>(T implInstance)
        {
            return RegisterType(typeof(T), implInstance);
        }

        public static bool RegisterType(Type type, object implInstance)
        {
            return Instances.TryAdd(type, implInstance);
        }
        public static T GetService<T>()
        {
            var key = typeof(T);
            if (Instances.TryGetValue(key, out var instance))
            {
                return (T)instance;
            }
            return default(T);
        }

    }
}
