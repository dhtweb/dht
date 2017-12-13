using System.Collections.Generic;
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
                                            return 0";

        private const string UnRegisterLua = @"local hash=redis.call('HGET',@point,@msgId)
                                            if(hash)
                                            then
                                                redis.call('HDEL',@point,@msgId)
                                            end
                                            return hash";
        private readonly IList<LoadedLuaScript> _registerScript;
        private readonly IList<LoadedLuaScript> _unRegisterScript;
        private readonly IDatabase _database;
        private int _index;

        public RedisMessageMap(string serverUrl)
        {
            var client = ConnectionMultiplexer.Connect(serverUrl);
            var points = client.GetEndPoints();
            _database = client.GetDatabase();
            var rScript = LuaScript.Prepare(RegisterLua);
            var uScript = LuaScript.Prepare(UnRegisterLua);
            _registerScript = new LoadedLuaScript[points.Length];
            _unRegisterScript = new LoadedLuaScript[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var server = client.GetServer(point);
                _registerScript[i] = rScript.Load(server);
                _unRegisterScript[i] = uScript.Load(server);
            }
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = _registerScript[(int)(nodeId % _registerScript.Count)];
            lock (this)
            {
                _index++;
                if (_index >= _bucketArray.Length)
                {
                    _index = 0;
                }
                msgId = _bucketArray[_index];
            }
            var result = srcript.Evaluate(_database, new { point = nodeId, hash = infoHash, msgId = ((byte[])msgId) });
            if (result.IsNull)
            {
                msgId = null;
                return false;
            }
            var resultInt = (int)result;
            if (resultInt == 1)
            {
                return true;
            }
            msgId = null;
            return false;
        }

        protected override bool RequireGetPeersRegisteredInfo(TransactionId msgId, DhtNode node, out byte[] infoHash)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = _unRegisterScript[(int)(nodeId % _registerScript.Count)];
            var result = srcript.Evaluate(_database, new { point = nodeId, msgId = ((byte[])msgId) });
            if (result.IsNull)
            {
                infoHash = null;
                return false;
            }
            var hash = (byte[])result;
            infoHash = hash;
            return true;
        }

        protected override Task<(bool IsOk, TransactionId MsgId)> RegisterGetPeersMessageAsync(byte[] infoHash, DhtNode node)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = _registerScript[(int)(nodeId % _registerScript.Count)];
            var localIndex = _index;
            localIndex++;
            if (localIndex >= _bucketArray.Length)
            {
                localIndex = 0;
            }
            var msgId = _bucketArray[localIndex];
            return srcript.EvaluateAsync(_database,
                new { point = nodeId, hash = infoHash, msgId = ((byte[])msgId) }).ContinueWith(
                t =>
                {
                    if (t.IsFaulted || t.IsCanceled)
                        return (false, null);
                    var result = t.Result;
                    if (!result.IsNull)
                    {
                        var resultInt = (int)result;
                        if (resultInt == 1)
                        {
                            return (true, msgId);
                        }
                    }
                    return (false, null);
                });

        }

        protected override Task<(bool IsOk, byte[] InfoHash)> RequireGetPeersRegisteredInfoAsync(TransactionId msgId, DhtNode node)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = _unRegisterScript[(int)(nodeId % _registerScript.Count)];
            return srcript.EvaluateAsync(_database, new { point = nodeId, msgId = ((byte[])msgId) }).ContinueWith(
                t =>
                {
                    if (t.IsCanceled || t.IsFaulted)
                        return (false, null);
                    var result = t.Result;
                    if (result.IsNull)
                    {
                        return (false, null);
                    }
                    var hash = (byte[])result;
                    return (true, hash);
                });
        }
    }
}
