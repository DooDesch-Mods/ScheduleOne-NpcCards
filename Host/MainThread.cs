using System;
using System.Collections.Concurrent;

namespace NpcCards.Host
{
    /// <summary>Work handed over from worker threads (downloads, Personnel's change event), run in Core.OnUpdate.</summary>
    internal static class MainThread
    {
        private static readonly ConcurrentQueue<(string what, Action run)> Queue = new ConcurrentQueue<(string, Action)>();

        internal static void Post(string what, Action run)
        {
            if (run != null) Queue.Enqueue((what, run));
        }

        internal static void Drain()
        {
            int guard = 0;
            while (guard++ < 32 && Queue.TryDequeue(out var item))
            {
                try { item.run(); }
                catch (Exception e) { Core.Log.Error($"[main] {item.what} failed on the main thread: {e}"); }
            }
        }
    }
}
