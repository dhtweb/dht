using System;

namespace DhtCrawler.Extension
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
