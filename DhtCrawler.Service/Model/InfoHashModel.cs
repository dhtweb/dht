﻿using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhtCrawler.Service.Model
{
    public class InfoHashModel : BaseModel<long>
    {
        public override long Id { get; set; }
        public string InfoHash { get; set; }
        public string Name { get; set; }

        private int _fileNum;
        [JsonIgnore]
        public int FileNum
        {
            get
            {
                if (_fileNum > 1)
                {
                    return _fileNum;
                }
                if (Files == null || Files.Count <= 0)
                {
                    return 1;
                }
                return (_fileNum = Files.Sum(f => f.FileNum));
            }
            set => _fileNum = value;
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

        [JsonIgnore]
        public string ShowFileSize
        {
            get
            {
                var size = FileSize * 1.0;
                if (size < TorrentFileModel.KbSize)//1K
                {
                    return size + "B";
                }
                if (size < TorrentFileModel.MbSize)//小于1M
                {
                    return (size / TorrentFileModel.KbSize).ToString("F2") + "KB";
                }
                if (size < TorrentFileModel.GbSize)//小于1G
                {
                    return (size / TorrentFileModel.MbSize).ToString("F2") + "MB";
                }
                return (size / TorrentFileModel.GbSize).ToString("F2") + "GB";
            }
        }

        public int DownNum { get; set; }
        public IList<TorrentFileModel> Files { get; set; }
        public IList<string> ShowFiles { get; set; }
        public bool HasFile { get; set; }
        public bool IsDanger { get; set; }
        [JsonIgnore]
        public ISet<string> KeyWords { get; set; }
    }
}
