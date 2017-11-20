using System;
using System.Collections.Generic;
using System.Linq;
using DhtCrawler.Common.Index;
using DhtCrawler.Common.Index.Analyzer;
using DhtCrawler.Service.Model;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace DhtCrawler.Service.Index
{
    public class IndexSearchService : BaseSearchService<InfoHashModel>
    {
        private InfoHashRepository _infoHashRepository;
        public IndexSearchService(string indexDir, InfoHashRepository infoHashRepository)
        {
            IndexDir = indexDir;
            _infoHashRepository = infoHashRepository;
        }

        private string[] SplitString(string keyword)
        {
            var seg = new JiebaSegmenter();
            return seg.CutForSearch(keyword).ToArray();
        }
        protected override string IndexDir { get; }

        protected override Analyzer KeyWordAnalyzer => new JieBaAnalyzer();

        protected override Document GetDocument(InfoHashModel item)
        {
            var doc = new Document();
            doc.AddStringField("InfoHash", item.InfoHash, Field.Store.YES);
            doc.AddTextField("Name", item.Name, Field.Store.YES);
            doc.AddInt32Field("DownNum", item.DownNum, Field.Store.YES);
            doc.AddInt32Field("FileNum", item.FileNum, Field.Store.YES);
            doc.AddInt64Field("FileSize", item.FileSize, Field.Store.YES);
            doc.AddInt64Field("CreateTime", item.CreateTime.Ticks, Field.Store.YES);
            doc.AddInt32Field("IsDanger", item.IsDanger ? 1 : 0, Field.Store.NO);
            if (item.Files != null && item.Files.Count > 0)
            {
                var names = new HashSet<string>();
                var queues = new Queue<TorrentFileModel>(item.Files);
                while (queues.Count > 0)
                {
                    var file = queues.Dequeue();
                    if (file.Files != null && file.Files.Count > 0)
                    {
                        foreach (var fileFile in file.Files)
                        {
                            queues.Enqueue(fileFile);
                        }
                    }
                    names.Add(file.Name);
                }
                doc.AddTextField("Files", string.Join(",", names), Field.Store.NO);
            }
            return doc;
        }

        protected override InfoHashModel GetModel(Document doc)
        {
            var item = new InfoHashModel
            {
                InfoHash = doc.Get("InfoHash"),
                Name = doc.Get("Name"),
                DownNum = doc.GetField("DownNum").GetInt32ValueOrDefault(),
                FileNum = doc.GetField("FileNum").GetInt32ValueOrDefault(),
                FileSize = doc.GetField("FileSize").GetInt64ValueOrDefault(),
                CreateTime = new DateTime(doc.GetField("CreateTime").GetInt64ValueOrDefault())
            };
            return item;
        }

        protected override IEnumerable<InfoHashModel> GetAllModels()
        {
            return _infoHashRepository.GetAllInfoHashModels();
        }

        protected override Term GetTargetTerm(InfoHashModel model)
        {
            return new Term("ModelId", model.InfoHash);
        }

        public IList<InfoHashModel> GetList(int index, int size, out int count, string keyword)
        {
            return base.Search(index, size, out count, () =>
            {
                var query = new BooleanQuery();//搜索条件
                if (!string.IsNullOrEmpty(keyword))
                //关键字搜索
                {
                    var splitKey = SplitString(keyword);
                    foreach (var key in splitKey)
                    {
                        query.Add(new TermQuery(new Term("Name", key)), Occur.SHOULD);
                        query.Add(new TermQuery(new Term("Files", key)), Occur.SHOULD);
                        // { new[] { new Term("Name", key), new Term("Files", key) } }
                    }
                }
                if (query.Clauses.Count <= 0)
                    query.Add(new MatchAllDocsQuery(), Occur.MUST);
                return query;
            }, () => new Sort(new SortField("CreateTime", SortFieldType.INT64, true)));
        }
    }
}
