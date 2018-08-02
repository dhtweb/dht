using System.Reflection;
using log4net;
using Lucene.Net.Util;

namespace DhtCrawler.Common.Index
{
    class Log4NetInfoStream : InfoStream
    {
        private ILog _log;

        public Log4NetInfoStream()
        {
            _log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "lucene");
        }
        public override void Message(string component, string message)
        {
            _log.InfoFormat("Lucene {0},Message:{1}", component, message);
        }

        public override bool IsEnabled(string component)
        {
            return true;
        }
    }
}
