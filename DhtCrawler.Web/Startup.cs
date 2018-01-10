using System;
using System.IO;
using System.Threading.Tasks;
using DhtCrawler.Common.Db;
using DhtCrawler.Common.Queue;
using DhtCrawler.Common.Web.Mvc.Static;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using DhtCrawler.Service.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DhtCrawler.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddMvc();
            services.AddSingleton<IQueue<PageStaticHtmlItem>, DefaultQueue<PageStaticHtmlItem>>();
            services.AddSingleton<IQueue<string>, DefaultQueue<string>>();
            services.AddSingleton<IQueue<VisitedModel>, DefaultQueue<VisitedModel>>();
            services.AddSingleton(provider => new StaticHtmlFilterAttribute(provider.GetService<IQueue<PageStaticHtmlItem>>()));
            services.AddSingleton(new DbFactory(Configuration["postgresql.url"], NpgsqlFactory.Instance));
            services.AddTransient<InfoHashRepository>();
            services.AddTransient<KeyWordRepository>();
            services.AddTransient<SearchWordRepository>();
            services.AddTransient<VisitedHistoryRepository>();
            services.AddSingleton(provider =>
            {
                var infoHashRepo = provider.GetService<InfoHashRepository>();
                return new IndexSearchService(Configuration["index.dir"], infoHashRepo);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute("list", "list/{keyword}/{index}", new { controller = "list", action = "list", index = 1 }, new { keyword = "\\S+", index = "\\d+" });
                routes.MapRoute("last", "last/{date}/{index}", new { controller = "list", action = "lastlist", index = 1 }, new { date = @"\d{4}-\d{2}-\d{2}", index = "\\d+" });
                routes.MapRoute("lastest", "last/{index}", new { controller = "list", action = "lastestlist", index = 1 }, new { index = "\\d+" });
                routes.MapRoute("detail", "infohash/{hash}.html", new { controller = "list", action = "detail" }, new { hash = "^[A-Za-z0-9]{40}$" });
            });
            app.UsePageHtmlStatic(async pageItem =>
            {
                var basePath = env.WebRootPath;
                var filePath = pageItem.RequestPath.Trim('/');
                var path = Path.Combine(basePath, filePath);
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }
                using (var writer = fileInfo.OpenWrite())
                {
                    await writer.WriteAsync(pageItem.Content, 0, pageItem.Content.Length);
                }
            });
            var logger = (ILogger)app.ApplicationServices.GetService(typeof(ILogger));
            System.Console.WriteLine("get logger over");
            Task.Run(async () =>
                       {
                           var wordQueue = app.ApplicationServices.GetService<IQueue<string>>();
                           var searchRepo = app.ApplicationServices.GetService<SearchWordRepository>();
                           while (true)
                           {
                               try
                               {
                                   var searchWord = await wordQueue.DequeueAsync();
                                   var item = new SearchWordModel() { Word = searchWord, Num = 1 };
                                   await searchRepo.InsertOrUpdateAsync(item);
                               }
                               catch (Exception ex)
                               {
                                   logger.LogError(ex, "记录用户搜索关键字失败");
                               }

                           }
                       }).ContinueWith(t =>
            {
                if (t.Exception != null)
                    logger.LogError(t.Exception, "记录用户搜索关键字启动失败");
            });
            Task.Run(async () =>
            {
                var visitQueue = app.ApplicationServices.GetService<IQueue<VisitedModel>>();
                var visiteRepo = app.ApplicationServices.GetService<VisitedHistoryRepository>();
                while (true)
                {
                    try
                    {
                        var item = await visitQueue.DequeueAsync();
                        await visiteRepo.InsertOrUpdateAsync(item);
                    }
                    catch (System.Exception ex)
                    {
                        logger.LogError(ex, "记录用户浏览历史失败");
                    }
                }
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                    logger.LogError(t.Exception, "记录用户浏览历史任务启动失败");
            });
        }
    }
}
