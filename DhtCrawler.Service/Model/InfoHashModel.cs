using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhtCrawler.Service.Model
{
    public class InfoHashModel : BaseModel<ulong>
    {
        public override ulong Id { get; set; }
        public string InfoHash { get; set; }
        public string Name { get; set; }
        [JsonIgnore]
        public int FileNum
        {
            get
            {
                if (Files == null || Files.Count <= 0)
                {
                    return IsDown ? 1 : 0;
                }
                return Files.Sum(f => f.FileNum);
            }
        }

        private long _fileSize;
        /// <summary>
        /// 相关联的文件大小
        /// </summary>
        public long FileSize
        {
            set => _fileSize = value;
            get
            {
                return _fileSize == 0 ? (_fileSize = Files?.Sum(f => f.FileSize) ?? 0) : _fileSize;
            }
        }
        public int DownNum { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
        public bool IsDown { get; set; }
    }
}
