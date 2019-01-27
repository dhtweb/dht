using System;
using System.Threading;
using System.Threading.Tasks;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using DhtCrawler.Service.Repository;
using log4net;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class IndexController : Controller
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(IndexController));
        private static readonly object IndexLocker = new object();
        private readonly IndexSearchService _indexSearchService;
        private readonly InfoHashRepository _infoHashRepository;
        private readonly StatisticsInfoRepository _statisticsInfoRepository;

        public IndexController(IndexSearchService indexSearchService, InfoHashRepository infoHashRepository, StatisticsInfoRepository statisticsInfoRepository)
        {
            _indexSearchService = indexSearchService;
            _infoHashRepository = infoHashRepository;
            _statisticsInfoRepository = statisticsInfoRepository;
        }

        public IActionResult IncrementBuild()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!Monitor.TryEnter(IndexLocker))
                    {
                        return;
                    }
                    log.Info("开始构建索引");
                    _indexSearchService.IncrementBuild();
                    log.Info("构建索引完成");
                    _infoHashRepository.GetTorrentNumAsync().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            log.Error("获取已下载种子数失败", t.Exception);
                            return;
                        }
                        _statisticsInfoRepository.InsertOrUpdate(new StatisticsInfoModel() { DataKey = "TorrentNum", Num = t.Result, UpdateTime = DateTime.Now });
                    });

                }
                catch (Exception ex)
                {
                    log.Error("索引构建出错", ex);
                }
                finally
                {
                    if (Monitor.IsEntered(IndexLocker))
                    {
                        Monitor.Exit(IndexLocker);
                    }
                }
            }, TaskCreationOptions.LongRunning);
            return new EmptyResult();
        }

        public IActionResult Build()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!Monitor.TryEnter(IndexLocker))
                    {
                        return;
                    }
                    _indexSearchService.ReBuildIndex();
                }
                finally
                {
                    if (Monitor.IsEntered(IndexLocker))
                    {
                        Monitor.Exit(IndexLocker);
                    }
                }
            }, TaskCreationOptions.LongRunning);
            return new EmptyResult();
        }
    }
}
