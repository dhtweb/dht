using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhtCrawler.Service.Model
{
    public class TorrentFileModel
    {
        public string Name { get; set; }
        [JsonIgnore]
        public int FileNum
        {
            get
            {
                if (Files == null || Files.Count <= 0)
                {
                    return 1;
                }
                return Files.Sum(f => f.FileNum);
            }
        }

        private long _size;
        /// <summary>
        /// 相关联的文件数量（单位kb）
        /// </summary>
        public long FileSize
        {
            get
            {
                if (_size != 0)
                    return _size;
                return (_size = Files?.Sum(f => f.FileSize) ?? 0);
            }
            set => _size = value;
        }
        public IList<TorrentFileModel> Files { get; set; }
    }
}
