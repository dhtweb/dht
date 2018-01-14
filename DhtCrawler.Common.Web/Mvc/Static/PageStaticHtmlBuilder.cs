using System;
using System.IO;
using System.Threading.Tasks;
using DhtCrawler.Common.Queue;
using Microsoft.AspNetCore.Builder;
using log4net;

namespace DhtCrawler.Common.Web.Mvc.Static
{
    public static class PageStaticHtmlBuilder
    {
        public static IApplicationBuilder UsePageHtmlStatic(this IApplicationBuilder app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            var queue = app.ApplicationServices.GetService(typeof(IQueue<PageStaticHtmlItem>));
            if (queue == null)
                throw new InvalidOperationException($"UnableToFindServices {nameof(queue)}");
            return app.UsePageHtmlStatic(async item =>
            {
                var filePath = Path.GetFileName(item.RequestPath);
                if (File.Exists(filePath))
                    return;
                using (var writer = File.OpenWrite(filePath))
                {
                    await writer.WriteAsync(item.Content, 0, item.Content.Length);
                }
            });
        }

        public static IApplicationBuilder UsePageHtmlStatic(this IApplicationBuilder app, Func<PageStaticHtmlItem, Task> action)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (app == null)
                throw new ArgumentNullException(nameof(action));
            var service = app.ApplicationServices.GetService(typeof(IQueue<PageStaticHtmlItem>));
            if (service == null)
                throw new InvalidOperationException($"UnableToFindServices {nameof(service)}");
            var queue = (IQueue<PageStaticHtmlItem>)service;
            var logger = LogManager.GetLogger(typeof(PageStaticHtmlBuilder));
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var item = await queue.DequeueAsync();
                        await action(item);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("静态文件写入错误", ex);
                    }
                }
            });
            return app;
        }
    }
}