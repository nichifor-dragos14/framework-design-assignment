using System.Collections.Concurrent;

namespace dotnet_request_limiter_and_logger.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _requestLimit;
    private readonly int _timeWindowSeconds;

    private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requestLog = new();

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

        if (ipAddress == null)
        {
            await _next(context);

            return;
        }

        if (_timeWindowSeconds < 1 || _requestLimit < 1)
        {
            await _next(context);

            return;
        }

        var now = DateTime.UtcNow;
        var requests = _requestLog.GetOrAdd(ipAddress, _ => new ConcurrentQueue<DateTime>());

        while (requests.TryPeek(out var oldest) && (now - oldest).TotalSeconds > _timeWindowSeconds)
        {
            requests.TryDequeue(out _);
        }

        if (requests.Count >= _requestLimit)
        {
            _logger.LogError($"[RateLimit] IP {ipAddress} blocked. Too many requests.");
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            await context.Response.WriteAsync("Too many requests. Try again later.");

            return;
        }

        requests.Enqueue(now);

        await _next(context);
    }
}