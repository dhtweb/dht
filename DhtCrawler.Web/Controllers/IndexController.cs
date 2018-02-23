using System;
using System.Threading;
using System.Threading.Tasks;
using DhtCrawler.Service.Index;
using log4net;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class IndexController : Controller
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(IndexController));
        private static readonly object IndexLocker = new object();
        private IndexSearchService _indexSearchService;
        public IndexController(IndexSearchService indexSearchService)
        {
            _indexSearchService = indexSearchService;
        }

        public IActionResult IncrementBuild(DateTime updateTime)
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
                    _indexSearchService.IncrementBuild(updateTime);
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
            return new EmptyResult();
        }

        public IActionResult MBuild()
        {
            Task.Factory.StartNew(() =>
            {
                _indexSearchService.MultipleThreadReBuildIndex();
            }, TaskCreationOptions.LongRunning);
            return Content("over");
        }
    }
}
