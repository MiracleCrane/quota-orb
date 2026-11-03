using System;
using System.Threading;
using QuotaOrb;

internal static class InstanceTests
{
    internal static void Run(Action<bool, string> check)
    {
        string name = "Local\\QuotaOrb.Test." + Guid.NewGuid().ToString("N");
        using (var request = new EventWaitHandle(false, EventResetMode.AutoReset))
        using (var acknowledged = new EventWaitHandle(false, EventResetMode.ManualReset))
        using (var ready = new ManualResetEvent(false))
        using (var release = new ManualResetEvent(false))
        {
            Exception failure = null; bool primaryAcquired = false;
            var primary = new Thread(delegate() {
                try { using (var instance = new SingleInstance(name)) {
                    primaryAcquired = instance.OwnsMutex; ready.Set(); release.WaitOne();
                } } catch (Exception error) { failure = error; ready.Set(); }
            });
            primary.Start();
            try {
                check(ready.WaitOne(2000) && failure == null && primaryAcquired, "The initial instance must own the mutex.");
                using (var secondary = new SingleInstance(name)) {
                    check(!secondary.OwnsMutex, "Repeated starts must not create a second orb.");
                    check(!secondary.NotifyOrTakeOver(request, acknowledged, 80) && request.WaitOne(0), "A live instance must receive the new start request.");
                }
                using (var secondary = new SingleInstance(name)) {
                    var handler = new Thread(delegate() { if (request.WaitOne(2000)) acknowledged.Set(); });
                    handler.Start();
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    check(!secondary.NotifyOrTakeOver(request, acknowledged, 2000) && elapsed.ElapsedMilliseconds < 1500,
                        "A handled start must return promptly instead of leaving a replacement waiting behind manual shutdown.");
                    check(handler.Join(2000), "The acknowledgement fixture must finish.");
                }
                using (var replacement = new SingleInstance(name)) {
                    var exiting = new Thread(delegate() { request.WaitOne(2000); release.Set(); });
                    exiting.Start();
                    check(replacement.NotifyOrTakeOver(request, acknowledged, 2000), "A start racing with shutdown must take ownership after the old instance exits.");
                    check(exiting.Join(2000), "The shutdown race fixture must finish.");
                }
            } finally { release.Set(); primary.Join(2000); }
            check(failure == null, "The old instance must release its mutex cleanly.");
        }
        using (var abandoned = new Mutex(false, name)) {
            var owner = new Thread(delegate() { abandoned.WaitOne(); });
            owner.Start(); owner.Join();
            using (var replacement = new SingleInstance(name))
                check(replacement.OwnsMutex, "A leftover mutex handle from an exited owner must not suppress startup.");
        }
    }
}
