using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace DhtCrawler.Common.Web.Mvc.Extends
{
    public static class RequestExtend
    {
        public static bool IsSpider(this HttpRequest request)
        {
            return request.Headers["User-Agent"].Any(header =>
            {
                return header.IndexOf("Googlebot", StringComparison.OrdinalIgnoreCase) > -1 || header.IndexOf("bingbot", StringComparison.OrdinalIgnoreCase) > -1 || header.IndexOf("Baiduspider", StringComparison.OrdinalIgnoreCase) > -1;
            });
        }
    }
}
