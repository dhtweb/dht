using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DhtCrawler.Common.Index.Utils;
using DhtCrawler.Common.Utils;
using DhtCrawler.Service.Model;
using DhtCrawler.Service.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class KeyWordController : Controller
    {
        private KeyWordRepository _keyWordRepository;
        private InfoHashRepository _infoHashRepository;
        public KeyWordController(KeyWordRepository keyWordRepository, InfoHashRepository infoHashRepository)
        {
            _keyWordRepository = keyWordRepository;
            _infoHashRepository = infoHashRepository;
        }

        public async Task BuildAll()
        {
            var infoHash = _infoHashRepository.GetAllFullInfoHashModels();
            var wordDic = new Dictionary<string, int>();
            var queue = new Queue<string>();
            var fileQueue = new Queue<IList<TorrentFileModel>>();
            var num = 0;
            foreach (var hashModel in infoHash)
            {
                queue.Enqueue(hashModel.Name);
                if (!hashModel.Files.IsEmpty())
                {
                    fileQueue.Enqueue(hashModel.Files);
                    while (fileQueue.Count > 0)
                    {
                        var fileList = fileQueue.Dequeue();
                        foreach (var file in fileList)
                        {
                            queue.Enqueue(file.Name);
                            if (!file.Files.IsEmpty())
                            {
                                fileQueue.Enqueue(file.Files);
                            }
                        }
                    }
                }
                while (queue.Count > 0)
                {
                    var name = queue.Dequeue();
                    var titleWords = name.CutForSearch().Union(name.Cut()).Union(name.Cut(false)).Where(w => w.Length > 1 && w.Length < AnalyzerUtils.DefaultMaxWordLength);
                    foreach (var word in titleWords)
                    {
                        if (!wordDic.ContainsKey(word))
                        {
                            wordDic[word] = 0;
                        }
                        wordDic[word]++;
                    }
                }
                num++;
                if (num % 500 == 0)
                {
                    await Response.WriteAsync(num.ToString() + Environment.NewLine);
                    await _keyWordRepository.InsertOrUpdateAsync(wordDic.Select(kv => new KeyWordModel() { Word = kv.Key, Num = kv.Value }));
                    wordDic.Clear();
                }
            }
            if (wordDic.Count > 0)
            {
                await Response.WriteAsync(num.ToString() + Environment.NewLine);
                await _keyWordRepository.InsertOrUpdateAsync(wordDic.Select(kv => new KeyWordModel() { Word = kv.Key, Num = kv.Value }));
                wordDic.Clear();
            }
        }
    }
}
