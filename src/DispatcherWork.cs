using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace QuotaOrb
{
    public sealed class DispatcherTicks
    {
        private readonly Dispatcher owner;
        private readonly Action tick;
        private readonly object gate = new object();
        private DispatcherOperation pending;
        public DispatcherTicks(Dispatcher owner, Action tick) { this.owner = owner; this.tick = tick; }
        public DispatcherPriority PendingPriority { get { lock (gate) return pending == null ? DispatcherPriority.Inactive : pending.Priority; } }
        public void Request(bool foreground)
        {
            lock (gate) {
                if (owner.HasShutdownStarted || owner.HasShutdownFinished) return;
                if (pending != null && pending.Status == DispatcherOperationStatus.Pending) {
                    if (foreground) pending.Priority = DispatcherPriority.Input;
                    return;
                }
                pending = owner.BeginInvoke(new Action(delegate {
                    lock (gate) pending = null;
                    tick();
                }), foreground ? DispatcherPriority.Input : DispatcherPriority.Background);
            }
        }
    }

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
