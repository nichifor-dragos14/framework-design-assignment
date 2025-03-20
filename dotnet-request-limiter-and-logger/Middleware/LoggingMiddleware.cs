using System.Diagnostics;

namespace dotnet_request_limiter_and_logger.Middleware;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(
        RequestDelegate next,
        ILogger<LoggingMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var now = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var request = context.Request;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var method = request.Method;
        var path = request.Path;

        await _next(context);

        stopwatch.Stop();

        var responseStatus = context.Response.StatusCode;
        var responseStatusHeader = context.Response.Headers.FirstOrDefault();
        var elapsedTime = stopwatch.ElapsedMilliseconds;

        switch(context.Response.StatusCode)
        {
            case StatusCodes.Status422UnprocessableEntity:
                _logger.LogError($"[RateLimit] Request was blocked blocked. No IP address.");
                break;
            case StatusCodes.Status429TooManyRequests:
                _logger.LogError($"[RateLimit] IP {ipAddress} blocked. Too many requests.");
                break;
            default:
                _logger.LogInformation($"[{now}] {method} {path} from {ipAddress} -> status {responseStatus} in ({elapsedTime}ms)");
                break;
        }
    }
}