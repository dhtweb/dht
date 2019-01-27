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
using System.Collections.Generic;
using System.Linq;

namespace DhtCrawler.Service.Index
{
    public class IndexSearchService : BaseSearchService<InfoHashModel>
    {
        private readonly InfoHashRepository _infoHashRepository;
        private readonly IFilter<string> _wordFilter;
        private readonly IFilter<string> _rankWords;
        private readonly Sort _defaultSort;
        private readonly Sort _timeSort;
        private readonly Sort _hotSort;
        public IndexSearchService(string indexDir, IFilter<string> wordFilter, IFilter<string> rankWords, InfoHashRepository infoHashRepository) : base(indexDir)
        {
            _infoHashRepository = infoHashRepository;
            _wordFilter = wordFilter;
            _rankWords = rankWords;
            _defaultSort = new Sort(SortField.FIELD_SCORE, new SortField("CreateTime", SortFieldType.INT64, true));
            _timeSort = new Sort(new SortField("CreateTime", SortFieldType.INT64, true));
            _hotSort = new Sort(new SortField("DownNum", SortFieldType.INT32, true));
        }

        protected override Analyzer KeyWordAnalyzer => new JieBaAnalyzer(0, AnalyzerUtils.DefaultMaxWordLength);

        protected override Document GetDocument(InfoHashModel item)
        {
            if (item.IsDanger || _wordFilter.Contain(item.Name))
            {
                log.InfoFormat("高危内容，参数:{0}", item.InfoHash);
                return null;
            }
            var doc = new Document();
            var nameField = doc.AddTextField("Name", item.Name, Field.Store.NO);
            nameField.Boost = _rankWords.Contain(item.Name) ? 0.1F : 10F;
            doc.AddStringField("Id", item.Id.ToString(), Field.Store.YES);
            doc.AddInt32Field("DownNum", item.DownNum, Field.Store.NO);
            doc.AddInt64Field("CreateTime", item.CreateTime.Ticks, Field.Store.NO);
            if (item.Files != null && item.Files.Count > 0)
            {
                var flag = false;
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
                        if (file.Name.IsBlank() || !names.Add(file.Name))
                            continue;
                        if (_wordFilter.Contain(file.Name))
                        {
                            log.InfoFormat("高危内容，参数:{0}", item.InfoHash);
                            return null;
                        }

                        if (!flag)
                        {
                            flag = _rankWords.Contain(file.Name);
                        }
                    }
                }
                var fileField = doc.AddTextField("Files", string.Join(",", names), Field.Store.NO);
                fileField.Boost = flag ? 0.1F : 9F;
            }
            log.InfoFormat("处理数据:{0}", item.Id.ToString());
            return doc;
        }

        protected override InfoHashModel GetModel(Document doc, ISet<string> keyWords)
        {
            var idStr = doc.GetField("Id").GetStringValue();
            if (!long.TryParse(idStr, out var infoId))
            {
                return new InfoHashModel();
            }
            var item = _infoHashRepository.GetInfoHashDetail(infoId);
            if (item == null)
            {
                return new InfoHashModel();
            }
            if (!item.Name.IsBlank())
            {
                var newName = SetHighKeyWord(item.Name, keyWords);
                if (item.Name.Length != newName.Length)
                {
                    item.Name = newName;
                    return item;
                }
            }

            if (item.Files == null || item.Files.Count <= 0)
            {
                return item;
            }

            item.ShowFiles = new List<string>();
            var fileQueue = new Queue<TorrentFileModel>(item.Files);
            while (fileQueue.Count > 0)
            {
                var file = fileQueue.Dequeue();
                var newline = SetHighKeyWord(file.Name, keyWords);
                if (newline.Length != file.Name.Length)
                {
                    item.ShowFiles.Add(newline);
                    if (item.ShowFiles.Count > 2)
                    {
                        break;
                    }
                }

                if (file.Files == null || item.Files.Count <= 0)
                    continue;
                foreach (var model in file.Files)
                {
                    fileQueue.Enqueue(model);
                }
            }
            item.Files = null;
            return item;
        }

        protected override IEnumerable<InfoHashModel> GetAllModels()
        {
            return _infoHashRepository.GetAllFullInfoHashModels();
        }

        protected override Term GetTargetTerm(InfoHashModel item)
        {
            return new Term("Id", item.Id.ToString());
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
                    BooleanQuery nameQuery = new BooleanQuery() { Boost = 100f }, flieQuery = new BooleanQuery();
                    foreach (var key in highWords)
                    {
                        var isSingle = key.Length == 1 && ('0' <= key[0] && key[0] <= '9' || 'a' <= key[0] && key[0] <= 'z' || 'A' <= key[0] && key[0] <= 'Z');
                        nameQuery.Add(new TermQuery(new Term("Name", key)), isSingle ? Occur.SHOULD : Occur.MUST);
                        flieQuery.Add(new TermQuery(new Term("Files", key)), isSingle ? Occur.SHOULD : Occur.MUST);
                    }
                    query.Add(nameQuery, Occur.SHOULD);
                    query.Add(flieQuery, Occur.SHOULD);
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

        public override void UpdateIndex(ICollection<InfoHashModel> models)
        {
            base.UpdateIndex(models);
            _infoHashRepository.RemoveSyncInfo(models.Select(m => m.Id).ToArray());
        }

        public void IncrementBuild()
        {
            var list = _infoHashRepository.GetSyncFullInfoHashModels();
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
