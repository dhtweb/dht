using System;
using System.Collections.Generic;

namespace DhtCrawler.Service.Model
{
    public class InfoHashUpdateModel
    {
        public long Id { get; set; }
        public string InfoHash { get; set; }
        public string Name { get; set; }
        public int? FileNum { get; set; }
        public long? FileSize { get; set; }
        public int? DownNum { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
        public bool? IsDown { get; set; }
        public bool? IsDanger { get; set; }
        public DateTime? CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }
    }
}
