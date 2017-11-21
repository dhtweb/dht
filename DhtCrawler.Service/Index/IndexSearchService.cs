﻿using System;
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
            return seg.CutForSearch(keyword).Select(w => w.ToLower()).ToArray();
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

        protected override InfoHashModel GetModel(Document doc, Query query)
        {
            var item = new InfoHashModel
            {
                InfoHash = doc.Get("InfoHash"),
                Name = SetHighKeyWord(doc.Get("Name"), "Name", query),//doc.Get("Name"),//
                DownNum = doc.GetField("DownNum").GetInt32ValueOrDefault(),
                FileNum = doc.GetField("FileNum").GetInt32ValueOrDefault(),
                FileSize = doc.GetField("FileSize").GetInt64ValueOrDefault(),
                CreateTime = new DateTime(doc.GetField("CreateTime").GetInt64ValueOrDefault())
            };
            return item;
        }

        protected override IEnumerable<InfoHashModel> GetAllModels()
        {
            return _infoHashRepository.GetAllFullInfoHashModels();
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
                if (!string.IsNullOrEmpty(keyword))//关键字搜索
                {
                    var splitKey = SplitString(keyword);
                    Console.WriteLine(string.Join(",", splitKey));
                    Term[] nameTerms = new Term[splitKey.Length], fileTerms = new Term[splitKey.Length];
                    for (var i = 0; i < splitKey.Length; i++)
                    {
                        var key = splitKey[i];
                        nameTerms[i] = new Term("Name", key);
                        fileTerms[i] = new Term("Name", key);
                    }
                    query.Add(new MultiPhraseQuery() { nameTerms }, Occur.SHOULD);
                    query.Add(new MultiPhraseQuery() { fileTerms }, Occur.SHOULD);
                }
                if (query.Clauses.Count <= 0)
                    query.Add(new MatchAllDocsQuery(), Occur.MUST);
                return query;
            }, () => new Sort(SortField.FIELD_SCORE, new SortField("CreateTime", SortFieldType.INT64, true)));
        }
    }
}