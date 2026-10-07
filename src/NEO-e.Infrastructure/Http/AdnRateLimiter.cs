using System.Collections.Concurrent;
using System.Threading;
using NEO_e.Application.Contracts;

namespace NEO_e.Infrastructure.Http;

public interface IAdnRateLimiter
{
    Task WaitAsync(string companyKey, CancellationToken ct);
}

public sealed class AdnRateLimiter : IAdnRateLimiter
{
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _companySemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastCallTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _minInterval;
    private readonly object _globalLock = new();

    public AdnRateLimiter(int maxGlobalConcurrent, TimeSpan minIntervalBetweenCalls)
    {
        _globalSemaphore = new SemaphoreSlim(maxGlobalConcurrent, maxGlobalConcurrent);
        _minInterval = minIntervalBetweenCalls;
    }

    public async Task WaitAsync(string companyKey, CancellationToken ct)
    {
        // Wait for global semaphore
        await _globalSemaphore.WaitAsync(ct);
        try
        {
            // Wait for company semaphore (serial per company)
            var companySemaphore = _companySemaphores.GetOrAdd(companyKey, _ => new SemaphoreSlim(1, 1));
            await companySemaphore.WaitAsync(ct);
            try
            {
                // Enforce minimum interval between calls for this company
                lock (_globalLock)
                {
                    if (_lastCallTime.TryGetValue(companyKey, out var lastCall))
                    {
                        var elapsed = DateTimeOffset.UtcNow - lastCall;
                        if (elapsed < _minInterval)
                        {
                            var waitTime = _minInterval - elapsed;
                            Task.Delay(waitTime, ct).Wait(ct);
                        }
                        _lastCallTime[companyKey] = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        _lastCallTime[companyKey] = DateTimeOffset.UtcNow;
                    }
                }
            }
            finally
            {
                companySemaphore.Release();
            }
        }
        finally
        {
            _globalSemaphore.Release();
        }
    }
}