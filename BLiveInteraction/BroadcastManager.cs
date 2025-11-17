using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TShockAPI;

namespace BLiveInteract
{
    public enum MsgPriority { High, Normal, Low }

    internal sealed class BroadcastManager : IDisposable
    {
        private readonly ConcurrentQueue<(string msg, Color color, MsgPriority prio)> _q = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly int _maxPerSecond;
        private int _tokens;
        private readonly object _lock = new();

        public BroadcastManager(int maxPerSecond = 10)
        {
            _maxPerSecond = Math.Max(1, maxPerSecond);
            _tokens = _maxPerSecond;
            _ = LoopAsync();
        }

        public void Enqueue(string msg, Color color, MsgPriority prio = MsgPriority.Normal)
        {
            _q.Enqueue((msg, color, prio));
        }

        private async Task LoopAsync()
        {
            var ct = _cts.Token;
            var refill = Stopwatch.StartNew();

            while (!ct.IsCancellationRequested)
            {
                if (refill.ElapsedMilliseconds >= 1000)
                {
                    lock (_lock) { _tokens = _maxPerSecond; }
                    refill.Restart();
                }

                while (_tokens > 0 && _q.TryDequeue(out var item))
                {
                    TSPlayer.All.SendMessage(item.msg, item.color);
                    lock (_lock) { _tokens--; }
                }

                await Task.Delay(50, ct);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}