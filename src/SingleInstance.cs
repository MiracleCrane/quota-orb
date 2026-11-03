using System;
using System.Threading;

namespace QuotaOrb
{
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex mutex;
        internal bool OwnsMutex { get; private set; }

        internal SingleInstance(string name)
        {
            mutex = new Mutex(false, name);
            OwnsMutex = Acquire(0);
        }

        internal bool NotifyOrTakeOver(EventWaitHandle startRequest, EventWaitHandle acknowledged, int timeoutMilliseconds)
        {
            if (OwnsMutex) return true;
            acknowledged.Reset();
            startRequest.Set();
            // The old instance may already be shutting down and unable to
            // consume the request. In that case this process takes its place.
            try { OwnsMutex = WaitHandle.WaitAny(new WaitHandle[] { acknowledged, mutex }, timeoutMilliseconds) == 1; }
            catch (AbandonedMutexException) { OwnsMutex = true; }
            return OwnsMutex;
        }

        private bool Acquire(int timeoutMilliseconds)
        {
            try { return mutex.WaitOne(timeoutMilliseconds); }
            catch (AbandonedMutexException) { return true; }
        }

        public void Dispose()
        {
            if (OwnsMutex) { mutex.ReleaseMutex(); OwnsMutex = false; }
            mutex.Dispose();
        }
    }
}
