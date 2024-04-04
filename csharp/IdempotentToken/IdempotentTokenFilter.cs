using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DIYComponents.IdempotentToken;

/// <summary>
/// 实现幂等性令牌的返回
/// </summary>
internal class IdempotentTokenFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        throw new NotImplementedException();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // 在Action之后执行
        if(context.Result is ObjectResult objectResult)
        {
            // 将令牌添加到返回的结果中
            objectResult.Value = new { IToken = "", Data = objectResult.Value};
        }
    }
}
