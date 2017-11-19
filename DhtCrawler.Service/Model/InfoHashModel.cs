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

        public string ShowFileSize
        {
            get
            {
                const int kbSize = 1024;
                const int mbSize = kbSize * 1024;
                const int gbSize = mbSize * 1024;
                var size = FileSize * 1.0;
                if (size < kbSize)//1K
                {
                    return size + "B";
                }
                if (size < mbSize)//小于1M
                {
                    return (size / kbSize).ToString("F") + "KB";
                }
                if (size < gbSize)//小于1G
                {
                    return (size / mbSize).ToString("F") + "MB";
                }
                return (size / gbSize).ToString("F") + "GB";
            }
        }

        public int DownNum { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
        public bool IsDown { get; set; }
        public bool IsDanger { get; set; }
    }
}
