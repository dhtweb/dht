using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Common.Web.Mvc.Result
{
    public class ZipResult : ActionResult
    {
        private string directoryPath;
        private string downloadName;
        public ZipResult(string directoryPath, string downloadName)
        {
            this.directoryPath = directoryPath;
            this.downloadName = downloadName;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"this path {directoryPath} not found");
            var response = context.HttpContext.Response;
            response.ContentType = "application/octet-stream";
            response.Headers["Content-Disposition"] = "attachment;filename=" + downloadName;
            var queue = new Queue<string>(new[] { directoryPath });
            using (var zipFile = new ZipArchive(response.Body, ZipArchiveMode.Create))
            {
                while (queue.Count > 0)
                {
                    var directory = queue.Dequeue();
                    foreach (var dirPath in Directory.GetDirectories(directory))
                    {
                        queue.Enqueue(dirPath);
                    }
                    var files = Directory.GetFiles(directory);
                    foreach (var filePath in files)
                    {
                        var zipItemName = filePath.Substring(directoryPath.Length);
                        var zipItem = zipFile.CreateEntry(zipItemName);
                        using (var fileStrem = File.OpenRead(filePath))
                        {
                            using (var itemStream = zipItem.Open())
                            {
                                await fileStrem.CopyToAsync(itemStream);
                            }
                        }
                    }
                }
            }
        }
    }
}
