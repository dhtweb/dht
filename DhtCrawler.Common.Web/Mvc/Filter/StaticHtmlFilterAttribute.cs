using System;
using System.IO;
using System.Threading.Tasks;
using DhtCrawler.Common.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DhtCrawler.Common.Web.Mvc.Filter
{
    public class StaticHtmlFilterAttribute : ResultFilterAttribute
    {
        private IHostingEnvironment _hosting;
        public StaticHtmlFilterAttribute(IHostingEnvironment hosting)
        {
            _hosting = hosting;
        }
        public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;
            var bufferStream = new BufferArrayStream(response.Body);
            response.Body = bufferStream;
            await next();
            var paths = request.Path.ToUriComponent().Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            var filePath = new string[paths.Length + 1];
            filePath[0] = _hosting.WebRootPath;
            for (var i = 0; i < paths.Length; i++)
            {
                filePath[i + 1] = paths[i];
            }
            string path = Path.Combine(filePath);
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Directory.Exists)
                fileInfo.Directory.Create();
            var byteArray = bufferStream.GetBufferContent();
            using (var file = fileInfo.Open(FileMode.OpenOrCreate))
            {
                await file.WriteAsync(byteArray, 0, byteArray.Length);
            }
        }
    }
}