using System.Threading.Tasks;
using DhtCrawler.Common.IO;
using DhtCrawler.Common.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DhtCrawler.Common.Web.Mvc.Static
{
    public class StaticHtmlFilterAttribute : ResultFilterAttribute
    {
        private IQueue<PageStaticHtmlItem> _queue;
        public StaticHtmlFilterAttribute(IQueue<PageStaticHtmlItem> queue)
        {
            _queue = queue;
        }
        public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;
            var bufferStream = new BufferArrayStream(response.Body);
            response.Body = bufferStream;
            await next();
            if (context.Result is ViewResult)
            {
                var byteArray = bufferStream.GetBufferContent();
                _queue.Enqueue(new PageStaticHtmlItem() { RequestPath = request.Path.ToUriComponent(), Content = byteArray });
            }
        }
    }
}