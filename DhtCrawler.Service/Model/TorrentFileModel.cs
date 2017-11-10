namespace DhtCrawler.Service.Model
{
    public class TorrentFileModel
    {
        public string FileName { get; set; }
        /// <summary>
        /// 相关联的文件数量（单位kb）
        /// </summary>
        public uint FileSize { get; set; }
    }
}
