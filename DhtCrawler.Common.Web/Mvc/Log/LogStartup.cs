using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Builder;
using System;
using System.IO;
using System.Reflection;

namespace DhtCrawler.Common.Web.Mvc.Log
{
    public static class LogStartup
    {
        public static IApplicationBuilder UseLog4Net(this IApplicationBuilder app, string configPath = "log4net.config")
        {
            if (!File.Exists(configPath))
                throw new ArgumentNullException("config file is not exists");
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.ConfigureAndWatch(logRepository, new FileInfo(configPath));
            return app;
        }
    }
}
