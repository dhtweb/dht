using System;
using System.Collections.Generic;
using System.Linq;
using DhtCrawler.Common.Filters;
using DhtCrawler.Common.Index;
using DhtCrawler.Common.Index.Analyzer;
using DhtCrawler.Common.Index.Utils;
using DhtCrawler.Common.Utils;
using DhtCrawler.Service.Model;
using DhtCrawler.Service.Repository;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace DhtCrawler.Service.Index
{
    public class IndexSearchService : BaseSearchService<InfoHashModel>
    {
        private readonly InfoHashRepository _infoHashRepository;
        private readonly IFilter<string> _wordFilter;
        private readonly Sort _defaultSort;
        private readonly Sort _timeSort;
        private readonly Sort _hotSort;
        public IndexSearchService(string indexDir, IFilter<string> wordFilter, InfoHashRepository infoHashRepository) : base(indexDir)
        {
            _infoHashRepository = infoHashRepository;
            _wordFilter = wordFilter;
            _defaultSort = new Sort(SortField.FIELD_SCORE, new SortField("CreateTime", SortFieldType.INT64, true));
            _timeSort = new Sort(new SortField("CreateTime", SortFieldType.INT64, true));
            _hotSort = new Sort(new SortField("DownNum", SortFieldType.INT32, true));
        }

        protected override Analyzer KeyWordAnalyzer => new JieBaAnalyzer(0, AnalyzerUtils.DefaultMaxWordLength);

        protected override Document GetDocument(InfoHashModel item)
        {
            log.InfoFormat("处理数据:{0}", item.Id.ToString());
            if (_wordFilter.Contain(item.Name))
            {
                log.InfoFormat("高危内容，参数:{0}", item.InfoHash);
                return null;
            }
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
                    else
                    {
                        if (!names.Add(file.Name))
                            continue;
                        if (_wordFilter.Contain(file.Name))
                        {
                            log.InfoFormat("高危内容，参数:{0}", item.InfoHash);
                            return null;
                        }
                    }
                }
                doc.AddTextField("Files", string.Join(",", names), Field.Store.YES);
            }
            return doc;
        }

        protected override InfoHashModel GetModel(Document doc, ISet<string> keyWords)
        {
            var item = new InfoHashModel
            {
                InfoHash = doc.Get("InfoHash"),
                DownNum = doc.GetField("DownNum").GetInt32ValueOrDefault(),
                FileNum = doc.GetField("FileNum").GetInt32ValueOrDefault(),
                FileSize = doc.GetField("FileSize").GetInt64ValueOrDefault(),
                CreateTime = new DateTime(doc.GetField("CreateTime").GetInt64ValueOrDefault())
            };
            var name = doc.Get("Name");
            if (!name.IsBlank())
            {
                var newName = SetHighKeyWord(name, keyWords);
                item.Name = newName;
                if (name.Length != newName.Length)
                {
                    return item;
                }
            }
            var files = doc.Get("Files");
            if (files.IsBlank())
                return item;
            item.ShowFiles = new List<string>();
            var lines = files.Split(',');
            foreach (var line in lines)
            {
                var newline = SetHighKeyWord(line, keyWords);
                if (newline.Length == line.Length)
                    continue;
                item.ShowFiles.Add(newline);
                if (item.ShowFiles.Count > 2)
                {
                    break;
                }
            }
            return item;
        }

        protected override IEnumerable<InfoHashModel> GetAllModels()
        {
            return _infoHashRepository.GetAllFullInfoHashModels();
        }

        protected override Term GetTargetTerm(InfoHashModel item)
        {
            return new Term("InfoHash", item.InfoHash);
        }

        public IList<InfoHashModel> GetList(int index, int size, out int count, string keyword, int sort = 1)
        {
            return base.Search(index, size, out count, () =>
            {
                var query = new BooleanQuery();//搜索条件
                var highWords = new string[0];
                if (!string.IsNullOrEmpty(keyword))//关键字搜索
                {
                    highWords = SplitString(keyword);
                    var searchKeys = highWords.Where(w => keyword.Length <= 1 || w.Length > 1).ToArray();
                    if (searchKeys.Length == 0)
                    {
                        searchKeys = highWords;
                    }
                    Term[] nameTerms = new Term[searchKeys.Length], fileTerms = new Term[searchKeys.Length];
                    for (var i = 0; i < searchKeys.Length; i++)
                    {
                        var key = searchKeys[i];
                        nameTerms[i] = new Term("Name", key);
                        fileTerms[i] = new Term("Files", key);
                    }
                    var nameQuery = new MultiPhraseQuery() { nameTerms };
                    nameQuery.Boost = 100f;
                    query.Add(nameQuery, Occur.SHOULD);
                    query.Add(new MultiPhraseQuery() { fileTerms }, Occur.SHOULD);
                }
                if (query.Clauses.Count <= 0)
                    query.Add(new MatchAllDocsQuery(), Occur.MUST);
                return (query, highWords);
            }, () =>
            {
                switch (sort)
                {
                    //时间
                    case 2:
                        return _timeSort;
                    //下载数
                    case 3:
                        return _hotSort;
                }
                return _defaultSort;
            });
        }

        public void IncrementBuild(DateTime? start)
        {
            var list = _infoHashRepository.GetAllFullInfoHashModels(start);
            var batch = new List<InfoHashModel>(1000);
            var size = 0;
            foreach (var item in list)
            {
                batch.Add(item);
                if (batch.Count > 1000)
                {
                    UpdateIndex(batch);
                    size += batch.Count;
                    batch.Clear();
                    log.InfoFormat("已更新{0}条数据", size);
                }
            }
            if (batch.Count > 0)
            {
                UpdateIndex(batch);
                size += batch.Count;
                batch.Clear();
                log.InfoFormat("已更新{0}条数据", size);
            }
        }
    }
}
