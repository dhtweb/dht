using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DhtCrawler.Common.Index.Analyzer;
using DhtCrawler.Common.Index.Utils;
using JiebaNet.Segmenter;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Directory = Lucene.Net.Store.Directory;

namespace DhtCrawler.Common.Index
{
    public abstract class BaseSearchService<T> : IDisposable where T : class
    {
        private static Directory GetIndexDirectory(string subIndex)
        {
            var directory = FSDirectory.Open(subIndex);
            if (!DirectoryReader.IndexExists(directory))//判断是否存在索引文件夹
            {
                System.IO.Directory.CreateDirectory(subIndex);
            }
            if (IndexWriter.IsLocked(directory))//判断是否被锁定，如果锁定则解锁
            {
                IndexWriter.Unlock(directory);
            }
            return directory;
        }

        private static readonly ConcurrentDictionary<string, FSDirectory> FSDirectoryDic = new ConcurrentDictionary<string, FSDirectory>();
        private static readonly ConcurrentDictionary<string, IndexWriter> IndexWriterDic = new ConcurrentDictionary<string, IndexWriter>();

        private volatile IndexWriter _writer;
        private IndexWriter IndexWriter
        {
            get
            {
                if (_writer != null)
                    return _writer;
                return IndexWriterDic.GetOrAdd(IndexDir, dic =>
                     {
                         lock (IndexWriterDic)
                         {
                             if (_writer != null)
                                 return _writer;
                             _writer = new IndexWriter(IndexDirectory,
                                 new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                                 {
                                     OpenMode = OpenMode.CREATE_OR_APPEND
                                 });
                             return _writer;
                         }
                     });
            }
        }

        private volatile FSDirectory _currentDirectory;
        private FSDirectory IndexDirectory
        {
            get
            {
                if (_currentDirectory != null)
                    return _currentDirectory;
                return _currentDirectory = FSDirectoryDic.GetOrAdd(IndexDir, dir =>
                 {
                     lock (FSDirectoryDic)
                     {
                         if (_currentDirectory != null)
                             return _currentDirectory;
                         _currentDirectory = FSDirectory.Open(IndexDir);
                         if (!DirectoryReader.IndexExists(_currentDirectory)) //判断是否存在索引文件夹
                         {
                             System.IO.Directory.CreateDirectory(IndexDir);
                         }
                         return _currentDirectory;
                     }
                 });  // 取得索引存储的文件夹
            }
        }
        protected abstract string IndexDir { get; }
        protected virtual int BatchCommitNum => 10000;
        protected abstract Lucene.Net.Analysis.Analyzer KeyWordAnalyzer { get; }

        /// <summary>
        /// 获取lucene文档对象
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        protected abstract Document GetDocument(T item);
        /// <summary>
        /// 由lucence获取实际对象
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="keyWords"></param>
        /// <returns></returns>
        protected abstract T GetModel(Document doc, ISet<string> keyWords);

        /// <summary>
        /// 获取重建索引的所有数据
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<T> GetAllModels();

        /// <summary>
        /// 获取更新/删除时所有需要的Term
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        protected abstract Term GetTargetTerm(T model);

        protected virtual string[] SplitString(string keyword)
        {
            var seg = new JiebaSegmenter();
            return seg.Cut(keyword, true).Union(seg.Cut(keyword)).Where(word => !string.IsNullOrWhiteSpace(word)).Select(w => w.ToLower()).Distinct().ToArray();
        }

        /// <summary>
        /// 设置关键词高亮
        /// </summary>
        /// <param name="content"></param>
        /// <param name="field"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        protected string SetHighKeyWord(string content, string field, Query query)
        {
            var formatter = new SimpleHTMLFormatter("<span class='highlight'>", "</span>");
            var keywords = new HashSet<Term>();
            query.ExtractTerms(keywords);
            var lighter = new Highlighter(formatter, new QueryScorer(query));
            return lighter.GetBestFragment(new JiebaMergeTokenizer(keywords.Select(k => k.Text()), new StringReader(content)), content);
        }

        /// <summary>
        /// 设置关键词高亮
        /// </summary>
        /// <param name="content"></param>
        /// <param name="keyWordSet"></param>
        /// <returns></returns>
        protected virtual string SetHighKeyWord(string content, ISet<string> keyWordSet)
        {
            if (keyWordSet == null || keyWordSet.Count <= 0)
                return content;
            const string preTag = "<span class='highlight'>", endTag = "</span>";
            var newContent = new StringBuilder(content.Length);
            var seg = new JiebaSegmenter();
            int index = 0;
            var contentWords = seg.TokenizeAll(content).Where(t => keyWordSet.Contains(t.Word)).ToArray();
            var resultToken = contentWords.MergeTokenList();
            foreach (var token in resultToken)
            {
                newContent.Append(content.Substring(index, token.StartIndex - index));
                newContent.Append(preTag);
                newContent.Append(token.Word);
                newContent.Append(endTag);
                index = Math.Max(index, token.EndIndex);
            }
            if (index <= content.Length - 1)
            {
                newContent.Append(content.Substring(index));
            }
            return newContent.ToString();
        }

        /// <summary>
        /// 重建索引
        /// </summary>
        public void ReBuildIndex(Action<T> onBuild)
        {
            var list = GetAllModels();
            int size = 0;
            lock (IndexWriter)
            {
                IndexWriter.DeleteAll();
                foreach (var item in list)
                {
                    var doc = GetDocument(item);
                    IndexWriter.AddDocument(doc);
                    onBuild?.Invoke(item);
                    size++;
                    if (size >= BatchCommitNum)
                    {
                        IndexWriter.Commit();
                        size = 0;
                    }
                }
                if (size > 0)
                    IndexWriter.Commit();
            }
        }

        public void MultipleThreadReBuildIndex()
        {
            var list = GetAllModels();
            var rmPaths = new List<string>();
            lock (IndexWriter)
            {
                var indexLocker = new object();
                IndexWriter.DeleteAll();
                Parallel.ForEach(list, new ParallelOptions() { MaxDegreeOfParallelism = 10 },
                    () =>
                    {
                        var subIndex = Path.Combine(IndexDir, Guid.NewGuid().ToString());
                        rmPaths.Add(subIndex);
                        var subDirectory = GetIndexDirectory(subIndex);
                        return new IndexWriter(subDirectory,
                            new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                            {
                                IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                                OpenMode = OpenMode.CREATE
                            });
                    },
                    (item, state, writer) =>
                    {
                        if (state.IsExceptional)
                            state.Break();
                        var doc = GetDocument(item);
                        writer.AddDocument(doc);
                        return writer;
                    }, writer =>
                    {
                        using (writer.Directory)
                        {
                            using (writer)
                            {
                                lock (indexLocker)
                                {
                                    using (var reader = writer.GetReader(true))
                                    {
                                        IndexWriter.AddIndexes(reader);
                                    }
                                }
                            }
                        }
                    });
                IndexWriter.Commit();
                foreach (var rmPath in rmPaths)
                {
                    System.IO.Directory.Delete(rmPath);
                }
            }
        }

        public void DeleteIndex(T model)
        {
            lock (IndexWriter)
            {
                IndexWriter.DeleteDocuments(GetTargetTerm(model));
                IndexWriter.Commit();
            }
        }
        /// <summary>
        /// 添加索引
        /// </summary>
        /// <param name="model"></param>
        public void AddIndex(T model)
        {
            var doc = GetDocument(model);
            lock (IndexWriter)
            {
                IndexWriter.AddDocument(doc);
                IndexWriter.Commit();
            }
        }
        /// <summary>
        /// 更新索引
        /// </summary>
        /// <param name="model"></param>
        public void UpdateIndex(T model)
        {
            var doc = GetDocument(model);
            lock (IndexWriter)
            {
                IndexWriter.UpdateDocument(GetTargetTerm(model), doc);
                IndexWriter.Commit();
            }
        }
        /// <summary>
        /// 批量更新索引
        /// </summary>
        /// <param name="models"></param>
        public void UpdateIndex(IEnumerable<T> models)
        {
            lock (IndexWriter)
            {
                foreach (var model in models)
                {
                    var doc = GetDocument(model);
                    IndexWriter.UpdateDocument(GetTargetTerm(model), doc);
                }
                IndexWriter.Commit();
            }
        }
        /// <summary>
        /// 对索引文档进行搜索
        /// </summary>
        /// <param name="index"></param>
        /// <param name="size"></param>
        /// <param name="total"></param>
        /// <param name="getQuery"></param>
        /// <param name="getSort"></param>
        /// <returns></returns>
        public IList<T> Search(int index, int size, out int total, Func<Query> getQuery, Func<Sort> getSort)
        {
            using (var reader = DirectoryReader.Open(IndexDirectory))//获取索引只读对象
            {
                var searcher = new IndexSearcher(reader);
                var query = getQuery();
                var sort = getSort();
                int start = (index - 1) * size, end = index * size;
                var docs = searcher.Search(query, null, end, sort);
                total = docs.TotalHits;
                end = Math.Min(end, total);
                var models = new List<T>();
                var queryTerms = new HashSet<Term>();
                query.ExtractTerms(queryTerms);
                var keywords = new HashSet<string>(queryTerms.Select(k => k.Text()), StringComparer.OrdinalIgnoreCase);
                for (int i = start; i < total && i < end; i++)
                {
                    var docNum = docs.ScoreDocs[i].Doc;
                    var doc = searcher.Doc(docNum);
                    models.Add(GetModel(doc, keywords));
                }
                return models;
            }
        }

        public void Dispose()
        {
            if (!IndexWriterDic.TryRemove(IndexDir, out var writer))
                return;
            writer.Dispose();
        }
    }
}