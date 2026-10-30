using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Duyao.ApiBase.Middleware;

public class TraceRequestMiddleware(RequestDelegate next, ILogger<TraceRequestMiddleware> logger)
{
    public const string REQUEST_ID = "XRequestId";
    public const string HTTP_METHOD = "HttpMethod";
    private static void MaskData(JToken token)
    {
        if (token is JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                // 忽略大小写匹配键名是否包含 "password"
                if (property.Name.Contains("password", StringComparison.OrdinalIgnoreCase))
                {
                    // 将敏感值替换为掩码
                    property.Value = new JValue("******");
                }
                // 忽略大小写匹配键名是否包含 "password"
                else if (property.Name.Contains("base64", StringComparison.OrdinalIgnoreCase))
                {
                    // 将敏感值替换为掩码
                    property.Value = new JValue("******");
                }
                else
                {
                    // 如果不是敏感字段，递归检查其子节点
                    MaskData(property.Value);
                }
            }
        }
        else if (token is JArray jArray)
        {
            // 如果是数组，递归检查数组中的每个元素
            foreach (var item in jArray)
            {
                MaskData(item);
            }
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        // 生成唯一的请求ID
        var requestId = Guid.NewGuid().ToString("n");
        var httpMethod = context.Request.Method.ToUpper();
        context.Items[REQUEST_ID] = requestId;
        context.Items[HTTP_METHOD] = httpMethod;

        var clientIp = context.Request.ClientIp();

        // 使用logger scope将requestId添加到日志上下文中
        using (logger.BeginScope(new[]
               {
                   new KeyValuePair<string, object>(REQUEST_ID, requestId),
                   new KeyValuePair<string, object>(HTTP_METHOD, httpMethod),
               }))
        {
            if (!context.Response.Headers.ContainsKey("x-request-id"))
            {
                context.Response.Headers.Append("x-request-id"
                    , Convert.ToString(context.Items[REQUEST_ID] ?? "UNKNOWN")
                );
            }

            if (!context.Response.Headers.ContainsKey("x-version-id"))
            {
                context.Response.Headers.Append("x-version-id"
                    , Convert.ToString(BuildInfo.GIT_VERSION)
                );
            }

            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("x-response-time"))
                {
                    context.Response.Headers.Append("x-response-time", $"{sw.ElapsedMilliseconds}");
                }

                return Task.CompletedTask;
            });

            #region 记录POST请求的body

            var requestBodyJson = string.Empty;
            var reqMethod = "POST";
            if (context.Request.Method == HttpMethods.Post &&
                context.Request.ContentType?.Contains("application/json") == true)
            {
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        var jToken = JToken.Parse(body);
                        MaskData(jToken);

                        requestBodyJson = JsonConvert.SerializeObject(jToken, Formatting.None);
                        logger.LogDebug($"[{clientIp}][POST {context.Request.Path}]");
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning("Invalid JSON body: {Exception}", ex.Message);
                    }
                }
            }

            if (context.Request.Method == HttpMethods.Get)
            {
                logger.LogDebug($"[{clientIp}][GET {context.Request.Path}{context.Request.QueryString}]");
                reqMethod = "GET";
            }

            if (context.Request.Method == HttpMethods.Delete)
            {
                logger.LogDebug($"[{clientIp}][DELETE {context.Request.Path}]");
                reqMethod = "DELETE";
            }

            #endregion

            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                logger.LogInformation($"[{clientIp}][{reqMethod} {context.Request.Path}][{requestBodyJson}] elapsed {sw.ElapsedMilliseconds} ms");
            }
        }//using (logger.BeginScope(new[]
    }
}