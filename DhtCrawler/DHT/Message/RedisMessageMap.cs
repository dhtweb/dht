using System;
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
        private readonly IList<LoadedLuaScript> registerScript;
        private readonly IList<LoadedLuaScript> unRegisterScript;
        private readonly IDatabase database;
        private string _storePath;
        private int _index;
        private object[] locks;
        public RedisMessageMap(string serverUrl)
        {
            var client = ConnectionMultiplexer.Connect(serverUrl);
            var points = client.GetEndPoints();
            database = client.GetDatabase();
            var rScript = LuaScript.Prepare(RegisterLua);
            var uScript = LuaScript.Prepare(UnRegisterLua);
            registerScript = new LoadedLuaScript[points.Length];
            unRegisterScript = new LoadedLuaScript[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var server = client.GetServer(point);
                registerScript[i] = rScript.Load(server);
                unRegisterScript[i] = uScript.Load(server);
            }
        }

        protected override bool RegisterGetPeersMessage(byte[] infoHash, DhtNode node, out TransactionId msgId)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = registerScript[(int)(nodeId % registerScript.Count)];
            lock (this)
            {
                _index++;
                if (_index >= _bucketArray.Length)
                {
                    _index = 0;
                }
                msgId = _bucketArray[_index];
            }
            var result = srcript.Evaluate(database, new { point = nodeId, hash = infoHash.ToHex(), msgId = ((byte[])msgId).ToHex() });
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
            var srcript = unRegisterScript[(int)(nodeId % registerScript.Count)];
            var result = srcript.Evaluate(database, new { point = nodeId, msgId = ((byte[])msgId).ToHex() });
            if (result.IsNull)
            {
                infoHash = null;
                return false;
            }
            var hash = (string)result;
            infoHash = hash.HexStringToByteArray();
            return true;
        }

        protected override Task<(bool IsOk, TransactionId MsgId)> RegisterGetPeersMessageAsync(byte[] infoHash, DhtNode node)
        {
            var nodeId = node.CompactEndPoint().ToInt64();
            var srcript = registerScript[(int)(nodeId % registerScript.Count)];
            var localIndex = _index;
            localIndex++;
            if (localIndex >= _bucketArray.Length)
            {
                localIndex = 0;
            }
            var msgId = _bucketArray[localIndex];
            return srcript.EvaluateAsync(database,
                new { point = nodeId, hash = infoHash.ToHex(), msgId = ((byte[])msgId).ToHex() }).ContinueWith(
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
            var srcript = unRegisterScript[(int)(nodeId % registerScript.Count)];
            return srcript.EvaluateAsync(database, new { point = nodeId, msgId = ((byte[])msgId).ToHex() }).ContinueWith(
                t =>
                {
                    if (t.IsCanceled || t.IsFaulted)
                        return (false, null);
                    var result = t.Result;
                    if (result.IsNull)
                    {
                        return (false, null);
                    }
                    var hash = (string)result;
                    return (true, hash.HexStringToByteArray());
                });
        }
    }
}
