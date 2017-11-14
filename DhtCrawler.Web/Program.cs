using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace DhtCrawler.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args).UseKestrel(kso =>
                {
                    kso.Listen(IPAddress.Any, 80);
                })
                .UseStartup<Startup>()
                .Build();
    }
}
