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
using Lucene.Net.Util;
using Directory = Lucene.Net.Store.Directory;
using log4net;

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

        private static readonly ConcurrentDictionary<string, FSDirectory> FsDirectoryDic = new ConcurrentDictionary<string, FSDirectory>();
        private static readonly ConcurrentDictionary<string, IndexWriter> IndexWriterDic = new ConcurrentDictionary<string, IndexWriter>();

        private IndexWriter _writer;
        private SearcherManager _searcherManager;
        //private IndexSearcher _searcher;
        private volatile FSDirectory _currentDirectory;
        private FSDirectory IndexDirectory
        {
            get
            {
                if (_currentDirectory != null)
                    return _currentDirectory;
                return _currentDirectory = FsDirectoryDic.GetOrAdd(IndexDir, dir =>
                 {
                     lock (FsDirectoryDic)
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
                 });
            }
        }
        protected readonly ILog log;
        protected string IndexDir { get; }
        protected virtual int BatchCommitNum => 10000;
        protected abstract Lucene.Net.Analysis.Analyzer KeyWordAnalyzer { get; }
        protected BaseSearchService(string indexDir)
        {
            this.log = LogManager.GetLogger(GetType());
            this.IndexDir = indexDir;
            this._writer = IndexWriterDic.GetOrAdd(indexDir, dic =>
             {
                 lock (IndexWriterDic)
                 {
                     if (IndexWriterDic.TryGetValue(indexDir, out var writer))
                     {
                         return writer;
                     }
                     return new IndexWriter(IndexDirectory,
                         new IndexWriterConfig(LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                         {
                             OpenMode = OpenMode.CREATE_OR_APPEND
                         });
                 }
             });
            this._searcherManager = new SearcherManager(_writer, true, null);
        }
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
            if (keyword.Length <= 1)
            {
                return new[] { keyword };
            }
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
                var length = token.StartIndex - index;
                if (length > 0)
                {
                    newContent.Append(content.Substring(index, length));
                }
                else if (length < 0 && index < token.EndIndex)
                {
                    token.Word = index < token.EndIndex ? content.Substring(index, token.EndIndex - index) : "";
                }
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
            int size = 0, total = 0;
            lock (_writer)
            {
                _writer.DeleteAll();
                foreach (var item in list)
                {
                    var doc = GetDocument(item);
                    if (doc == null)
                    {
                        continue;
                    }
                    _writer.AddDocument(doc);
                    onBuild?.Invoke(item);
                    size++;
                    if (size >= BatchCommitNum)
                    {
                        total += size;
                        _writer.Commit();
                        size = 0;
                        log.InfoFormat("已更新{0}条数据", total);
                    }
                }
                if (size > 0)
                {
                    total += size;
                    _writer.Commit();
                    log.InfoFormat("已更新{0}条数据", total);
                }
            }
        }

        public void MultipleThreadReBuildIndex()
        {
            try
            {
                lock (_writer)
                {
                    var tasks = new Task[10];
                    var subDics = new IndexWriter[tasks.Length];
                    for (var i = 0; i < subDics.Length; i++)
                    {
                        var path = Path.Combine(IndexDir, Guid.NewGuid().ToString("N"));
                        var directory = GetIndexDirectory(path);
                        subDics[i] = new IndexWriter(directory,
                                new IndexWriterConfig(LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                                {
                                    IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                                    OpenMode = OpenMode.CREATE_OR_APPEND
                                });

                    }
                    var queue = new ConcurrentQueue<T>();
                    var list = GetAllModels();
                    foreach (var item in list)
                    {
                        queue.Enqueue(item);
                        if (queue.Count < 10000)
                            continue;
                        for (var i = 0; i < tasks.Length; i++)
                        {
                            tasks[i] = Task.Factory.StartNew(index =>
                            {
                                var writer = (IndexWriter)index;
                                while (queue.TryDequeue(out var it))
                                {
                                    var doc = GetDocument(it);
                                    if (doc == null)
                                    {
                                        continue;
                                    }
                                    writer.AddDocument(doc);
                                }
                                writer.Commit();
                            }, subDics[i]).ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    Console.WriteLine(t.Exception);
                                }
                            });
                        }
                        Task.WhenAll(tasks);
                    }
                    if (queue.Count > 0)
                    {
                        for (var i = 0; i < tasks.Length; i++)
                        {
                            tasks[i] = Task.Factory.StartNew(index =>
                            {
                                var writer = (IndexWriter)index;
                                while (queue.TryDequeue(out var it))
                                {
                                    var doc = GetDocument(it);
                                    if (doc == null)
                                    {
                                        continue;
                                    }
                                    writer.AddDocument(doc);
                                }
                                writer.Commit();
                            }, subDics[i]).ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    Console.WriteLine(t.Exception);
                                }
                            });
                        }
                        Task.WhenAll(tasks);
                    }
                    _writer.DeleteAll();
                    foreach (var writer in subDics)
                    {
                        try
                        {
                            using (var reader = writer.GetReader(false))
                            {
                                _writer.AddIndexes(reader);
                            }
                            _writer.Commit();
                            writer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            log.Error("合并索引失败", ex);
                        }
                        finally
                        {
                            if (IndexWriter.IsLocked(writer.Directory))
                            {
                                IndexWriter.Unlock(writer.Directory);
                            }
                        }
                    }
                    var rmPaths = System.IO.Directory.GetDirectories(IndexDir);
                    foreach (var rmPath in rmPaths)
                    {
                        System.IO.Directory.Delete(rmPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("索引构建失败", ex);
            }

        }

        public void DeleteIndex(T model)
        {
            lock (_writer)
            {
                _writer.DeleteDocuments(GetTargetTerm(model));
                _writer.Commit();
            }
        }
        /// <summary>
        /// 添加索引
        /// </summary>
        /// <param name="model"></param>
        public void AddIndex(T model)
        {
            var doc = GetDocument(model);
            if (doc == null)
            {
                DeleteIndex(model);
                return;
            }
            lock (_writer)
            {
                _writer.AddDocument(doc);
                _writer.Commit();
            }
        }
        /// <summary>
        /// 更新索引
        /// </summary>
        /// <param name="model"></param>
        public void UpdateIndex(T model)
        {
            var doc = GetDocument(model);
            if (doc == null)
            {
                DeleteIndex(model);
                return;
            }
            lock (_writer)
            {
                _writer.UpdateDocument(GetTargetTerm(model), doc);
                _writer.Commit();
            }
        }
        /// <summary>
        /// 批量更新索引
        /// </summary>
        /// <param name="models"></param>
        public void UpdateIndex(IEnumerable<T> models)
        {
            lock (_writer)
            {
                foreach (var model in models)
                {
                    var doc = GetDocument(model);
                    if (doc == null)
                    {
                        _writer.DeleteDocuments(GetTargetTerm(model));
                    }
                    else
                    {
                        _writer.UpdateDocument(GetTargetTerm(model), doc);
                    }
                }
                _writer.Commit();
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
        public IList<T> Search(int index, int size, out int total, Func<(Query, string[])> getQuery, Func<Sort> getSort)
        {
            IndexSearcher searcher = null;
            try
            {
                _searcherManager.MaybeRefresh();
                searcher = _searcherManager.Acquire();
                var query = getQuery();
                var sort = getSort();
                int start = (index - 1) * size, end = index * size;
                var docs = searcher.Search(query.Item1, null, end, sort);
                total = docs.TotalHits;
                end = Math.Min(end, total);
                if (start >= end)
                {
                    start = Math.Max(0, end - size);
                }
                var models = new T[end - start];
                var keywords = new HashSet<string>(query.Item2, StringComparer.OrdinalIgnoreCase);
                for (int i = start; i < total && i < end; i++)
                {
                    var docNum = docs.ScoreDocs[i].Doc;
                    var doc = searcher.Doc(docNum);
                    models[i - start] = GetModel(doc, keywords);
                }
                return models;
            }
            finally
            {
                if (searcher != null)
                {
                    _searcherManager.Release(searcher);
                    searcher = null;
                }
            }
        }

        public void Dispose()
        {
            _searcherManager.Dispose();
            _searcherManager = null;
            if (IndexWriterDic.TryRemove(IndexDir, out var writer))
            {
                writer.Dispose();
                _writer = null;
            }
        }
    }
}