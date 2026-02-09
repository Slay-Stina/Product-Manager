using System.Collections.Concurrent;

namespace Product_Manager.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RequestCounter> _requestCounts = new();
    private static readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private readonly int _maxRequestsPerMinute = 100;
    private readonly int _cleanupIntervalMinutes = 5;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            _logger.LogWarning("Received request without valid RemoteIpAddress, blocking request");
            context.Response.StatusCode = 400; // Bad Request
            await context.Response.WriteAsync("Client IP address is required.");
            return;
        }

        var clientIp = remoteIp.ToString();
        var now = DateTime.UtcNow;

        var counter = _requestCounts.GetOrAdd(clientIp, _ => new RequestCounter());

        bool isRateLimited;
        lock (counter.Lock)
        {
            // Clean up old entries within this counter
            counter.Timestamps.RemoveAll(t => (now - t).TotalMinutes > 1);

            isRateLimited = counter.Timestamps.Count >= _maxRequestsPerMinute;
            
            if (!isRateLimited)
            {
                counter.Timestamps.Add(now);
            }
        }

        if (isRateLimited)
        {
            _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}", clientIp);
            
            // Ensure headers are set before response starts
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.Headers.Append("Retry-After", "60");
            }
            
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        // Periodic cleanup of old IPs (every 5 minutes)
        var timeSinceLastCleanup = now - _lastCleanup;
        if (timeSinceLastCleanup.TotalMinutes >= _cleanupIntervalMinutes && _cleanupSemaphore.CurrentCount > 0)
        {
            _ = Task.Run(async () => await CleanupOldEntriesAsync(now));
        }

        await _next(context);
    }

    private async Task CleanupOldEntriesAsync(DateTime now)
    {
        // Use semaphore to ensure only one cleanup runs at a time
        if (!await _cleanupSemaphore.WaitAsync(0))
        {
            return; // Another cleanup is already running
        }

        try
        {
            // Atomically update the last cleanup time
            var previousCleanup = _lastCleanup;
            _lastCleanup = now;
            
            // If another thread already updated it to a more recent time, skip this cleanup
            if (previousCleanup > now.AddMinutes(-_cleanupIntervalMinutes))
            {
                return;
            }
            
            var keysToRemove = new List<string>();

            foreach (var kvp in _requestCounts)
            {
                bool shouldRemove;
                lock (kvp.Value.Lock)
                {
                    shouldRemove = kvp.Value.Timestamps.All(t => (now - t).TotalMinutes > 5);
                }
                
                if (shouldRemove)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _requestCounts.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} inactive IP addresses from rate limiter", keysToRemove.Count);
            }
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    private class RequestCounter
    {
        public object Lock { get; } = new object();
        public List<DateTime> Timestamps { get; } = new();
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
