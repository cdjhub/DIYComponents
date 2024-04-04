using Microsoft.AspNetCore.Http;

namespace DIYComponents.IdempotentToken;

/// <summary>
/// 实现幂等性令牌的检验 
/// </summary>
public class IdempotentTokenMiddleware
{
    private RequestDelegate _next;
    private IdempotentTokenManager _manager;

    public IdempotentTokenMiddleware(RequestDelegate next, IdempotentTokenManager manager)
    {
        _next = next;
        _manager = manager;
    }

    async public Task Invoke(HttpContext context)
    {
        var nonused = (NonIdempotentTokenAttribute)context.GetEndpoint()?.Metadata.GetMetadata<NonIdempotentTokenAttribute>();
        if (nonused != null)
        {
            _next(context);
            return;
        }

        string token = context.Request.Headers["IToken"];
        if (token == null || !_manager.ValidataToken(token))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
}
