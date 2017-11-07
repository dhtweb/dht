using System;

namespace DhtCrawler.Common.RateLimit
{
    public interface IRateLimit
    {
        bool Require(int count, out TimeSpan waitTime);
    }
}
