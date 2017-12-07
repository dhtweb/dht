using System.IO;
using System.Threading.Tasks;
using DhtCrawler.Common.Utils;
using StackExchange.Redis;

namespace DhtCrawler.DHT.Message
{
    public class RedisMessageMap : AbstractMessageMap
    {
        private const string RegisterLua = @"local flag=redis.call('SADD',@hash,@point)
                                            if(flag~=1)
                                            then
                                                return 0
                                            end
                                            redis.call('EXPIRE',@hash,1800)
                                            local result=redis.call('HSETNX',@point,@msgId,@hash)
                                            if(result)
                                            then
                                                redis.call('EXPIRE',@point,1800)
                                                return 1
                                            end
                                            return -1";

        private const string UnRegisterLua = @"local hash=redis.call('HGET',@point,@msgId)
                                            if(hash)
                                            then
                                                redis.call('HDEL',@point,@msgId)
                                            end
                                            return hash";
        private readonly IDatabase database;
        private readonly LuaScript registerScript;
        private readonly LuaScript unRegisterScript;
        private string _storePath;
        private int _index;
        private object[] locks;
        public RedisMessageMap(string serverUrl)
        {
            var client = ConnectionMultiplexer.Connect(serverUrl);
            database = client.GetDatabase();
            registerScript = LuaScript.Prepare(RegisterLua);
            unRegisterScript = LuaScript.Prepare(UnRegisterLua);
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeId = (ulong)node.CompactEndPoint().ToInt64();
            var tryTimes = 0;
            msgId = null;
            while (tryTimes < 3)
            {
                tryTimes++;
                lock (this)
                {
                    _index++;
                    if (_index >= _bucketArray.Length)
                        _index = 0;
                }
                msgId = _bucketArray[_index];
                var path = nodeId << 16 | (uint)(msgId[0] << 8) | msgId[1];
                var filePath = Path.Combine(_storePath, path + ".bin");
                var syncObj = locks[path % (ulong)locks.Length];
                lock (syncObj)
                {
                    if (File.Exists(filePath))
                    {
                        continue;
                    }
                    File.WriteAllBytes(filePath, infoHash);
                    return true;
                }
            }
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            var nodeId = (ulong)node.CompactEndPoint().ToInt64();
            var path = nodeId << 16 | (uint)(msgId[0] << 8) | msgId[1];
            var filePath = Path.Combine(_storePath, path + ".bin");
            var syncObj = locks[path % (ulong)locks.Length];
            lock (syncObj)
            {
                if (File.Exists(filePath))
                {
                    infoHash = File.ReadAllBytes(filePath);
                    File.Delete(filePath);
                    return true;
                }
                infoHash = null;
                return false;
            }
        }

        protected override Task<bool> RequireGetPeersRegisteredInfoAsync(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {

            return base.RequireGetPeersRegisteredInfoAsync(msgId, node, out infoHash);
        }
    }
}
