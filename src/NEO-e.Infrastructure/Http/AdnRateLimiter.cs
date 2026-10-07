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

    public AdnRateLimiter(int maxGlobalConcurrent, TimeSpan minIntervalBetweenCalls)
    {
        _globalSemaphore = new SemaphoreSlim(maxGlobalConcurrent, maxGlobalConcurrent);
        _minInterval = minIntervalBetweenCalls;
    }

    public async Task WaitAsync(string companyKey, CancellationToken ct)
    {
        await _globalSemaphore.WaitAsync(ct);
        try
        {
            var companySemaphore = _companySemaphores.GetOrAdd(companyKey, _ => new SemaphoreSlim(1, 1));
            await companySemaphore.WaitAsync(ct);
            try
            {
                if (_lastCallTime.TryGetValue(companyKey, out var lastCall))
                {
                    var elapsed = DateTimeOffset.UtcNow - lastCall;
                    if (elapsed < _minInterval)
                        await Task.Delay(_minInterval - elapsed, ct);
                }

                _lastCallTime[companyKey] = DateTimeOffset.UtcNow;
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