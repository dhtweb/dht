using DhtCrawler.Service;
using DhtCrawler.Service.Index;
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
            services.AddMvc();
            services.AddTransient(provider =>
            {
                var connection = new NpgsqlConnection(Configuration["postgresql.url"]);
                return new InfoHashRepository(connection);
            });
            services.AddTransient(provider =>
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
                routes.MapRoute("list", "list/{keyword}/{index}", new { controller = "list", action = "index", index = 1 }, new { keyword = "\\S+", index = "\\d+" });
                routes.MapRoute("detail", "infohash/{hash}.html", new { controller = "list", action = "detail" }, new { hash = "^[A-Za-z0-9]{40}$" });
            });
        }
    }
}
