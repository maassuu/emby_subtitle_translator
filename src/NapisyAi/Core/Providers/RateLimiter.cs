using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    /// <summary>Keeps at least 60/rpm seconds between requests.</summary>
    public sealed class RateLimiter
    {
        private readonly TimeSpan _interval;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private TimeSpan? _last;

        public RateLimiter(int requestsPerMinute, Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _interval = TimeSpan.FromSeconds(60.0 / Math.Max(1, requestsPerMinute));
            _delay = delay ?? Task.Delay;
        }

        public TimeSpan Interval => _interval;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_last is { } last)
                {
                    var remaining = last + _interval - _clock.Elapsed;
                    if (remaining > TimeSpan.Zero)
                    {
                        await _delay(remaining, cancellationToken).ConfigureAwait(false);
                    }
                }

                _last = _clock.Elapsed;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
