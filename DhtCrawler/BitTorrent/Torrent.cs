using System.Collections.Generic;
using System.Linq;

namespace DhtCrawler.BitTorrent
{
    public class Torrent
    {
        public string InfoHash { get; set; }
        public string Name { get; set; }
        private long _size;

        public long FileSize
        {
            get { return _size <= 0 ? (_size = Files?.Sum(f => f.FileSize) ?? 0) : _size; }
            set => _size = value;
        }

        public IList<TorrentFile> Files { get; set; }
    }
}
