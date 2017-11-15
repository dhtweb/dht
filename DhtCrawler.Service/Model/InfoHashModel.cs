using System.Collections.Generic;

namespace DhtCrawler.Service.Model
{
    public class InfoHashModel : BaseModel<ulong>
    {
        public override ulong Id { get; set; }
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
    }
}
