using System.Collections.Concurrent;

namespace dotnet_request_limiter_and_logger.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _requestLimit;
    private readonly int _timeWindowSeconds;

    private static readonly ConcurrentDictionary<string, List<DateTime>> _requestLog = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<RateLimitingMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;

        var rateLimitConfig = config.GetSection("RateLimiting");

        _requestLimit = rateLimitConfig.GetValue<int>("RequestLimit");
        _timeWindowSeconds = rateLimitConfig.GetValue<int>("TimeWindowSeconds");
    }

    public async Task Invoke(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;

        if (ipAddress != null)
        {
            _requestLog.AddOrUpdate(ipAddress, new List<DateTime> { now }, (key, list) =>
            {
                list.Add(now);
                list.RemoveAll(time => (now - time).TotalSeconds > _timeWindowSeconds);

                return list;
            });

            if (_requestLog[ipAddress].Count > _requestLimit)
            {
                _logger.LogError($"[RateLimit] IP {ipAddress} blocked. Too many requests.");
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                await context.Response.WriteAsync("Too many requests. Try again later.");

                return;
            }
        }

        await _next(context);
    }
}
