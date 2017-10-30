using System.Collections.Generic;
using DhtCrawler.Encode;

namespace DhtCrawler.DHT
{
    public class DhtMessage
    {
        private SortedDictionary<string, object> _message;

        public DhtMessage()
        {
            _message = new SortedDictionary<string, object>();
        }

        public DhtMessage(IDictionary<string, object> dictionary)
        {
            _message = new SortedDictionary<string, object>(dictionary);
        }

        public CommandType CommandType
        {
            get
            {
                if (!_message.TryGetValue("q", out object type))
                {
                    return CommandType.UnKnow;
                }
                switch ((string)type)
                {
                    case "ping":
                        return CommandType.Ping;
                    case "find_node":
                        return CommandType.Find_Node;
                    case "get_peers":
                        return CommandType.Get_Peers;
                    case "announce_peer":
                        return CommandType.Announce_Peer;
                }
                return CommandType.UnKnow;
            }
            set
            {
                _message["q"] = value.ToString().ToLower();
            }
        }

        public string MessageId
        {
            get
            {
                if (_message.TryGetValue("t", out object msgId))
                {
                    return (string)msgId;
                }
                return string.Empty;
            }
            set
            {
                _message["t"] = value;
            }
        }

        public MessageType MesageType
        {
            get
            {
                if (!_message.TryGetValue("y", out object type))
                {
                    return MessageType.UnKnow;
                }
                switch ((string)type)
                {
                    case "e":
                        return MessageType.Exception;
                    case "q":
                        return MessageType.Request;
                    case "r":
                        return MessageType.Response;
                }
                return MessageType.UnKnow;
            }
            set
            {
                switch (value)
                {
                    case MessageType.Exception:
                    case MessageType.UnKnow:
                        _message["y"] = "e";
                        break;
                    case MessageType.Request:
                        _message["y"] = "q";
                        break;
                    case MessageType.Response:
                        _message["y"] = "r";
                        break;
                }
            }
        }

        public IDictionary<string, object> Data
        {
            get
            {
                var key = MessageType.Request == this.MesageType ? "a" : "r";
                if (!_message.TryGetValue(key, out object dic))
                {
                    dic = new SortedDictionary<string, object>();
                    _message.Add(key, dic);
                }
                return (IDictionary<string, object>)dic;
            }
            set
            {
                var key = MessageType.Request == this.MesageType ? "a" : "r";
                if (value == null || value.Count <= 0)
                {
                    _message.Remove(key);
                    return;
                }
                _message[key] = value;
            }
        }

        private List<object> _errorList;
        public IList<object> Errors
        {
            get
            {
                if (!_message.TryGetValue("e", out object list))
                {
                    list = new List<object>(2);
                    _message.Add("e", list);
                }
                return (List<object>)list;
            }
        }

        public byte[] BEncodeBytes()
        {
            return BEncoder.EncodeObject(_message);
        }
    }
}
