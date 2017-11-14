using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace DhtCrawler.Service.Model
{
    public class InfoHashModel : BaseModel<string>
    {
        /// <summary>
        /// InfoHash
        /// </summary>
        [BsonIgnore]
        public string InfoHash { get; set; }
        public string Name { get; set; }
        public int FileNum { get; set; }
        /// <summary>
        /// 相关联的文件大小（单位kb）
        /// </summary>
        public uint FileSize { get; set; }
        public int DownNum { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
        public bool IsDown { get; set; }
        public override string Id
        {
            get => InfoHash;
            set => InfoHash = value;
        }
    }
}
