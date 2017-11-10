using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace DhtCrawler.Service.Model
{
    public class InfoHashModel
    {
        /// <summary>
        /// InfoHash
        /// </summary>
        [BsonId]
        public string InfoHash { get; set; }
        public string Name { get; set; }
        public int FileNum { get; set; }
        /// <summary>
        /// 相关联的文件数量（单位kb）
        /// </summary>
        public uint FileSize { get; set; }
        public int DownNum { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
    }
}
