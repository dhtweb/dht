using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Directory = Lucene.Net.Store.Directory;

namespace DhtCrawler.Common.Index
{
    public abstract class BaseSearchService<T> where T : class
    {
        protected abstract string IndexDir { get; }
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
        /// <returns></returns>
        protected abstract T GetModel(Document doc);

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

        /// <summary>
        /// 设置关键词高亮
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        protected string SetHighKeyWord(string keyword, string content)
        {
            ////创建HTMLFormatter,参数为高亮单词的前后缀 
            //var formatter = new PanGu.HighLight.SimpleHTMLFormatter("<font color='red'>", "</font>");
            ////设置每个摘要段的字符数 
            //var lighter = new PanGu.HighLight.Highlighter(formatter, new Segment()) { FragmentSize = 180 };//创建 Highlighter ，输入HTMLFormatter 和 盘古分词对象Semgent 
            //return lighter.GetBestFragment(keyword, content);//获取最匹配的摘要段 
            return content;
        }

        /// <summary>
        /// 重建索引
        /// </summary>
        public void ReBuildIndex(Action<T> onBuild)
        {
            using (FSDirectory directory = FSDirectory.Open(IndexDir)) // 取得索引存储的文件夹
            {
                if (!DirectoryReader.IndexExists(directory)) //判断是否存在索引文件夹
                {
                    System.IO.Directory.CreateDirectory(IndexDir);
                }
                if (IndexWriter.IsLocked(directory)) //判断是否被锁定，如果锁定则解锁
                {
                    IndexWriter.Unlock(directory);
                }
                var list = GetAllModels();
                if (!Enumerable.Any(list)) return;
                using (var writer = new IndexWriter(directory,
                    new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                    {
                        IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                        OpenMode = OpenMode.CREATE
                    })) //创建索引写入者
                {
                    writer.DeleteAll();
                    foreach (var item in list)
                    {
                        var doc = GetDocument(item);
                        writer.AddDocument(doc);
                        onBuild?.Invoke(item);
                    }
                    writer.Commit();
                }
            }
        }

        public void MultipleThreadReBuildIndex()
        {
            using (var directory = GetIndexDirectory(IndexDir)) // 取得索引存储的文件夹
            {
                var list = GetAllModels();
                if (!Enumerable.Any(list)) return;
                var indexLocker = new object();
                using (var parentWriter = new IndexWriter(directory,
                    new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer))) //创建索引写入者
                {
                    parentWriter.DeleteAll();
                    Parallel.ForEach(list,
                        () =>
                        {
                            var subIndex = Path.Combine(IndexDir, Guid.NewGuid().ToString());
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
                                            parentWriter.AddIndexes(reader);
                                        }
                                    }
                                    writer.DeleteAll();
                                }
                            }
                        });
                    parentWriter.Commit();
                }
            }
        }

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

        public void DeleteIndex(T model)
        {
            using (var indexDirectory = FSDirectory.Open(IndexDir))
            {
                using (var writer = new IndexWriter(indexDirectory, new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                {
                    IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                    OpenMode = OpenMode.APPEND
                }))
                {
                    writer.DeleteDocuments(GetTargetTerm(model));
                    writer.Commit();
                }
            }
        }
        /// <summary>
        /// 添加索引
        /// </summary>
        /// <param name="model"></param>
        public void AddIndex(T model)
        {
            using (var indexDirectory = FSDirectory.Open(IndexDir))
            {
                var doc = GetDocument(model);
                using (var writer = new IndexWriter(indexDirectory, new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                {
                    IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                    OpenMode = OpenMode.APPEND
                }))
                {
                    writer.AddDocument(doc);
                    writer.Commit();
                }
            }
        }
        /// <summary>
        /// 更新索引
        /// </summary>
        /// <param name="model"></param>
        public void UpdateIndex(T model)
        {
            using (var indexDirectory = FSDirectory.Open(IndexDir))
            {
                var doc = GetDocument(model);
                using (var writer = new IndexWriter(indexDirectory, new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                {
                    IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                    OpenMode = OpenMode.APPEND
                }))
                {
                    writer.UpdateDocument(GetTargetTerm(model), doc);
                    writer.Commit();
                }
            }
        }
        /// <summary>
        /// 批量更新索引
        /// </summary>
        /// <param name="models"></param>
        public void UpdateIndex(IEnumerable<T> models)
        {
            using (var indexDirectory = FSDirectory.Open(IndexDir))
            {
                using (var writer = new IndexWriter(indexDirectory, new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, KeyWordAnalyzer)
                {
                    IndexDeletionPolicy = new KeepOnlyLastCommitDeletionPolicy(),
                    OpenMode = OpenMode.APPEND
                }))
                {
                    foreach (var model in models)
                    {
                        var doc = GetDocument(model);
                        writer.UpdateDocument(GetTargetTerm(model), doc);
                    }
                    writer.Commit();
                }
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
            using (var indexDirectory = FSDirectory.Open(IndexDir))
            {
                using (var reader = DirectoryReader.Open(indexDirectory))//获取索引只读对象
                {
                    var searcher = new IndexSearcher(reader);
                    var query = getQuery();
                    var sort = getSort();
                    int start = (index - 1) * size, end = index * size;
                    var docs = searcher.Search(query, null, end, sort);
                    total = docs.TotalHits;
                    var models = new LinkedList<T>();
                    for (int i = start; i < total & i < end; i++)
                    {
                        var docNum = docs.ScoreDocs[i].Doc;
                        var doc = searcher.Doc(docNum);
                        models.AddLast(GetModel(doc));
                    }
                    return models.ToArray();
                }
            }
        }
    }
}