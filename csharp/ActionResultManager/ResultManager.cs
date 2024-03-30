using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DIYCompotents.ActionResultManager;


public class ResultManager : IActionFilter
{
    private static ConcurrentQueue<Func<ActionExecutedContext, KeyValuePair<string, object>>> RESULT_FUNC = new();

    public static void AddResult(Func<ActionExecutedContext, KeyValuePair<string, object>> action)
    {
        RESULT_FUNC.Enqueue(action);
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult)
        {
            objectResult.Value = new Dictionary<string, object>() { { "data", objectResult.Value } };

            var dict = objectResult.Value as IDictionary<string, object>;
            var additionalInfo = new Dictionary<string, object>();

            foreach (var action in RESULT_FUNC)
            {
                var res = action(context);
                additionalInfo.Add(res.Key, res.Value);
            }

            dict.Add("addInfo", additionalInfo);

            objectResult.Value = JsonSerializer.Serialize(dict);
        }
    }
}
