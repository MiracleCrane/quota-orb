using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace QuotaOrb
{
    public static class DispatcherWork
    {
        // Startup can run before WPF installs a SynchronizationContext. Always use
        // the window owner's Dispatcher explicitly, even when the caller is on it.
        public static void Run<T>(Dispatcher owner, Func<T> work, Action<T> complete, Action fail)
        {
            Task.Run(work).ContinueWith(delegate(Task<T> result) {
                if (owner.HasShutdownStarted || owner.HasShutdownFinished) {
                    if (result.IsFaulted) { var observed = result.Exception; }
                    return;
                }
                owner.BeginInvoke(new Action(delegate {
                    if (result.IsFaulted) { var observed = result.Exception; fail(); }
                    else if (!result.IsCanceled) complete(result.Result);
                }));
            });
        }
    }
}
