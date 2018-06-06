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
        private readonly StatisticsInfoRepository _statisticsInfoRepository;
        private readonly IServiceProvider _serviceProvider;

        public IndexController(IndexSearchService indexSearchService, StatisticsInfoRepository statisticsInfoRepository, IServiceProvider serviceProvider)
        {
            _indexSearchService = indexSearchService;
            _statisticsInfoRepository = statisticsInfoRepository;
            _serviceProvider = serviceProvider;
        }

        public async Task<IActionResult> IncrementBuild()
        {
            await _statisticsInfoRepository.GetInfoById("LastIndexTime").ContinueWith(t =>
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
            });
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
                    _indexSearchService.ReBuildIndex(null);
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
