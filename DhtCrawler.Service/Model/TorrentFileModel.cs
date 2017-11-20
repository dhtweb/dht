using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhtCrawler.Service.Model
{
    public class TorrentFileModel
    {
        public const int KbSize = 1024;
        public const int MbSize = KbSize * 1024;
        public const int GbSize = MbSize * 1024;
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
        [JsonIgnore]
        public string ShowFileSize
        {
            get
            {
                var size = FileSize * 1.0;
                if (size < KbSize)//1K
                {
                    return size + "B";
                }
                if (size < MbSize)//小于1M
                {
                    return (size / KbSize).ToString("F") + "KB";
                }
                if (size < GbSize)//小于1G
                {
                    return (size / MbSize).ToString("F") + "MB";
                }
                return (size / GbSize).ToString("F") + "GB";
            }
        }
        public IList<TorrentFileModel> Files { get; set; }
    }
}
