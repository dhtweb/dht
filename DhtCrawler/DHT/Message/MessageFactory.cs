using System;
using System.Collections.Concurrent;
using System.Threading;
using DhtCrawler.Utils;

namespace DhtCrawler.DHT.Message
{
    public class MessageFactory
    {
        public static readonly BlockingCollection<MessageId> Bucket = new BlockingCollection<MessageId>();
        public static readonly ConcurrentDictionary<MessageId, Tuple<DhtMessage, DateTime>> MessageMap = new ConcurrentDictionary<MessageId, Tuple<DhtMessage, DateTime>>();

        static MessageFactory()
        {
            var id = new byte[2];
            for (int i = 0; i <= byte.MaxValue; i++)
            {
                id[0] = (byte)i;
                for (int j = 0; j <= byte.MaxValue; j++)
                {
                    id[1] = (byte)j;
                    Bucket.Add(id.CopyArray());
                }
            }
            new Thread(() =>
                {
                    while (true)
                    {
                        foreach (var item in MessageMap)
                        {
                            var tuple = item.Value;
                            if ((DateTime.Now - tuple.Item2).TotalSeconds > 30)
                            {
                                MessageMap.TryRemove(item.Key, out var rm);
                                Bucket.Add(item.Key);
                            }
                        }
                        Thread.Sleep(30);
                    }
                })
            { IsBackground = true }.Start();
        }

        public static void RegisterMessage(DhtMessage message)
        {
            while (true)
            {
                var messageId = Bucket.Take();
                if (!MessageMap.TryAdd(messageId, new Tuple<DhtMessage, DateTime>(message, DateTime.Now))) continue;
                message.MessageId = messageId;
                return;
            }
        }

        public static DhtMessage UnRegisterMessageId(DhtMessage message)
        {
            if (!MessageMap.TryRemove(message.MessageId, out var obj))
                return null;
            Bucket.Add(message.MessageId);
            return obj.Item1;
        }
    }
}
