using System;
using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    public static class Retry
    {
        /// <summary>Calls with pacing; retries transient errors and treats a long run of them as an exhausted daily quota.</summary>
        public static async Task<string> CallAsync(
            Func<CancellationToken, Task<string>> call,
            RateLimiter limiter,
            Func<TimeSpan, CancellationToken, Task>? sleep,
            CancellationToken cancellationToken,
            int maxTransient = 5)
        {
            sleep ??= Task.Delay;
            var attempt = 0;
            while (true)
            {
                await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await call(cancellationToken).ConfigureAwait(false);
                }
                catch (TransientException e)
                {
                    attempt++;
                    if (attempt >= maxTransient)
                    {
                        throw new DailyQuotaException("Limit zapytań wyczerpany - spróbuj później");
                    }

                    var wait = e.RetryAfter ?? TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 60));
                    await sleep(wait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
