using System.Diagnostics;

namespace Duyao.ApiBase.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger
    //,TelegramNotifierService telegramNotifier
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 异步发送 Telegram 异常通知（不阻塞响应）
        void NotifyTelegram(Exception ex, string requestId)
        {
            var hostName = Environment.GetEnvironmentVariable("APP_HOST_NAME")
                           ?? Environment.MachineName;
            logger.LogError("[❌] GlobalExceptionHandler begin");
            logger.LogError($"{requestId}，{ex.Message}");
            logger.LogError($"{requestId}，{ex.StackTrace?.Replace("\n", " <---- ")}");

            // 只提取项目代码的最近 2 个堆栈帧（过滤 NuGet/系统库）
            var projectFrames = new StackTrace(ex, true).GetFrames()
                ?.Where(f =>
                {
                    var type = f.GetMethod()?.DeclaringType?.FullName;
                    return type != null
                           && (type.StartsWith("ScApi") || type.StartsWith("Sc.ExtApi") || type.StartsWith("Sc."));
                })
                .Take(2)
                .Select(f =>
                {
                    var method = f.GetMethod();
                    var file = f.GetFileName();
                    return
                        $"at {method?.DeclaringType?.FullName}.{method?.Name}({string.Join(", ", method?.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}") ?? [])})"
                        + (file != null ? $" in {Path.GetFileName(file)}:line {f.GetFileLineNumber()}" : "");
                })
                .ToList();

            var stackInfo = projectFrames is { Count: > 0 }
                ? string.Join("\n", projectFrames)
                : "（无项目代码堆栈）";

            // _ = telegramNotifier.SendMessageAsync(
            //     $"<b>❌ 未处理异常</b>\n"
            //     + $"<b>机器名:</b> {hostName}\n"
            //     + $"<b>流水号:</b> <code>{requestId}</code>\n"
            //     + $"<b>请求路径:</b> {context.Request.Method} {context.Request.Path}\n"
            //     + $"<b>异常类型:</b> <code>{ex.GetType().Name}</code>\n"
            //     + $"<b>异常消息:</b> {ex.Message}\n"
            //     + $"<b>项目堆栈(最近2帧):</b>\n<code>{stackInfo}</code>\n"
            //     + $"<b>时间:</b> {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        try
        {
            await next(context);
        }
        catch (Exception exp) when (exp is InvalidOperationException)
        {
            var requestId = Convert.ToString(context.Items[TraceRequestMiddleware.REQUEST_ID]) ?? "UNKNOWN";

            var errorResponse = new ApiResponse
            {
                Ok = false,
                Message = exp.Message
            };
            // NotifyTelegram(exp, requestId);


            //context.Response.StatusCode = exp.StatusCode;
            context.Response.ContentType = "application/json";

            if (!context.Response.Headers.ContainsKey("x-request-id"))
            {
                context.Response.Headers.Append("x-request-id"
                    , Convert.ToString(context.Items[TraceRequestMiddleware.REQUEST_ID] ?? "UNKNOWN")
                );
            }

            // logger.LogError($"{exp.Message}");
            await context.Response.WriteAsync(errorResponse.ToJsonString() ?? "{}");
        }
        catch (Exception exception)
        {
            var requestId = Convert.ToString(context.Items[TraceRequestMiddleware.REQUEST_ID]) ?? "UNKNOWN";

            NotifyTelegram(exception, requestId);

            var errorResponse = new ApiResponse
            {
                Ok = false,
                Message = exception.Message
            };
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            if (!context.Response.Headers.ContainsKey("x-request-id"))
            {
                context.Response.Headers.Append("x-request-id"
                    , Convert.ToString(context.Items[TraceRequestMiddleware.REQUEST_ID] ?? "UNKNOWN")
                );
            }

            await context.Response.WriteAsync(errorResponse.ToJsonString() ?? "{}");
        }
    }
}