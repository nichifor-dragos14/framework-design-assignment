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
        var stopwatch = Stopwatch.StartNew();

        var request = context.Request;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var method = request.Method;
        var path = request.Path;

        await _next(context);

        stopwatch.Stop();

        var responseStatus = context.Response.StatusCode;
        var elapsedTime = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation($"[{DateTime.UtcNow}] {method} {path} from {ipAddress} -> status {responseStatus} in ({elapsedTime}ms)");
    }
}