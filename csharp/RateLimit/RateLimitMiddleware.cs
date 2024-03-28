using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Net.Http;

namespace DIYComponents.RateLimit;

public class RateLimitMiddleware<T> where T : ITryConsume
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, ITryConsume> _fixedWindows = new ConcurrentDictionary<string, ITryConsume>();

    public RateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    async public Task Invoke(HttpContext context)
    {
        var rateLimitAttribute = (RateLimitAttribute)context.GetEndpoint()?.Metadata.GetMetadata<RateLimitAttribute>();
        if (rateLimitAttribute == null)
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress.ToString();
        var key = $"{ipAddress}-{context.Request.Path}";

        var fixedWindows = _fixedWindows.GetOrAdd(key, k =>
            TryConsumeFactory.CreateInstance<T>(rateLimitAttribute.LimitCount, rateLimitAttribute.Period)
        );

        if (!fixedWindows.TryConsume())
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        await _next(context);
    }
}
