using System;
using System.IO;
using System.Threading.Tasks;
using DhtCrawler.Common.Db;
using DhtCrawler.Common.Queue;
using DhtCrawler.Common.Web.Mvc.Static;
using DhtCrawler.Service;
using DhtCrawler.Service.Index;
using DhtCrawler.Service.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            Task.Run(async () =>
            {
                var wordQueue = app.ApplicationServices.GetService<IQueue<string>>();
                while (true)
                {
                    var searchWord = await wordQueue.DequeueAsync();
                }
            });
            Task.Run(async () =>
            {
                var visitQueue = app.ApplicationServices.GetService<IQueue<VisitedModel>>();
                while (true)
                {
                    var item = await visitQueue.DequeueAsync();

                }
            });
        }
    }
}
