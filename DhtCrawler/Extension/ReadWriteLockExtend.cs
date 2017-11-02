using System;
using System.Threading;

namespace DhtCrawler.Extension
{
    public static class ReadWriteLockExtend
    {
        public static IDisposable EnterRead(this ReaderWriterLockSlim rwLockSlim)
        {
            return new LockWrapper(rwLockSlim.EnterReadLock, rwLockSlim.ExitReadLock);
        }

        public static IDisposable EnterWrite(this ReaderWriterLockSlim rwLockSlim)
        {
            return new LockWrapper(rwLockSlim.EnterWriteLock, rwLockSlim.ExitWriteLock);
        }
    }
}
