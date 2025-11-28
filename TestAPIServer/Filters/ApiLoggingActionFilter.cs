using Microsoft.AspNetCore.Mvc.Filters;

namespace TestAPIServer.Filters;

public class ApiLoggingActionFilter : IAsyncActionFilter
{
    private readonly ILogger<ApiLoggingActionFilter> _logger;

    public ApiLoggingActionFilter(ILogger<ApiLoggingActionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpMethod = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? "";
        var actionName = context.ActionDescriptor.RouteValues["action"] ?? path.Split('/').LastOrDefault() ?? "Unknown";
        var clientIp = GetClientIpAddress(context.HttpContext);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var logMessage = $"[{timestamp}] {httpMethod} {actionName} - 클라이언트: {clientIp} 에서 호출됨";

        _logger.LogInformation(logMessage);

        await next();
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            return ipAddress.Split(',')[0].Trim();
        }

        ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            return ipAddress;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}

