using Microsoft.AspNetCore.Mvc.Filters;

namespace Test.Filter
{
    public class Filter1 : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
         ActionExecutingContext context,
         ActionExecutionDelegate next)
        {
            // Action 执行前
            var actionName = context.ActionDescriptor.DisplayName;

            var result = await next(); // 执行 Action

            // Action 执行后
            if (result.Exception != null)
            {
                // 异常处理
            }
        }
    }
}
