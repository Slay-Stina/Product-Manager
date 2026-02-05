using System.Collections.Concurrent;

namespace Product_Manager.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RequestCounter> _requestCounts = new();
    private readonly int _maxRequestsPerMinute = 100;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        var counter = _requestCounts.GetOrAdd(clientIp, _ => new RequestCounter());

        bool isRateLimited;
        lock (counter)
        {
            // Clean up old entries
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
            context.Response.StatusCode = 429; // Too Many Requests
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        // Clean up old IPs periodically
        if (now.Second == 0)
        {
            CleanupOldEntries(now);
        }

        await _next(context);
    }

    private void CleanupOldEntries(DateTime now)
    {
        var keysToRemove = _requestCounts
            .Where(kvp => kvp.Value.Timestamps.All(t => (now - t).TotalMinutes > 5))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _requestCounts.TryRemove(key, out _);
        }
    }

    private class RequestCounter
    {
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
