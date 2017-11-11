using System.Collections.Generic;
using System.Linq;

namespace DhtCrawler.BitTorrent
{
    public class TorrentFile
    {
        private long _fileSize;

        public long FileSize
        {
            get
            {
                if (_fileSize > 0)
                    return _fileSize;
                if (Files == null || Files.Count <= 0)
                    return 0;
                _fileSize = Files.Sum(f => f.FileSize);
                return _fileSize;
            }
            set => _fileSize = value;
        }

        public string Name { get; set; }
        public IList<TorrentFile> Files { get; set; }
    }
}
