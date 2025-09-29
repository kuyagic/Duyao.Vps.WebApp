namespace Duyao.SmsForward;

public class TraceRequestMiddleware(RequestDelegate next, ILogger<TraceRequestMiddleware> logger)
{
    public const string REQUEST_ID = "XRequestId";
    public const string HTTP_METHOD = "HttpMethod";

    public async Task InvokeAsync(HttpContext context)
    {
        // 生成唯一的请求ID
        var requestId = Guid.NewGuid().ToString();
        var httpMethod = context.Request.Method.ToUpper();

        context.Items[REQUEST_ID] = requestId;
        context.Items[HTTP_METHOD] = httpMethod;

        // 使用logger scope将requestId添加到日志上下文中
        using (logger.BeginScope(new[]
               {
                   new KeyValuePair<string, object>(REQUEST_ID, requestId),
                   new KeyValuePair<string, object>(HTTP_METHOD, httpMethod)
               }))
        {
            context.Response.Headers.Append("x-request-id"
                , Convert.ToString(context.Items[REQUEST_ID] ?? "UNKNOWN")
            );
            await next(context);
        }
    }
}