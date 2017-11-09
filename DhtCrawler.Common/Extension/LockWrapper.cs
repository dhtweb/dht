using System;

namespace DhtCrawler.Common.Extension
{
    public class LockWrapper : IDisposable
    {
        private Action _leave;
        public LockWrapper(Action enter, Action leave)
        {
            enter();
            this._leave = leave;
        }

        public void Dispose()
        {
            _leave();
        }
    }
}
