using Microsoft.Extensions.Primitives;

namespace Duyao.ApiBase.Middleware;

public static class MiddlewareExt
{
    public static IApplicationBuilder RegisterMiddleware(this IApplicationBuilder builder)
    {
        return builder
                .UseMiddleware<TraceRequestMiddleware>()
                .UseMiddleware<GlobalExceptionMiddleware>()
                .UseMiddleware<HttpOptionsMiddleware>()
            ;
    }

    public static string? FetchHeader(this HttpRequest request, string headerKey)
    {
        var exist = request.Headers
            .FirstOrDefault(a => a.Key.Equals(headerKey, StringComparison.InvariantCultureIgnoreCase));
        StringValues sv;
        if (exist.Key == null) return null;

        if (!request.Headers.TryGetValue(exist.Key, out sv)) return null;

        var hv = sv.First();
        return hv;
    }
    public static string? ClientIp(this HttpRequest request)
    {
        var seq = new List<KeyValuePair<int, string?>>
        {
            new(1, request.FetchHeader("x-haproxy-ip")),
            new(2, request.FetchHeader("CF-Connecting-IP")),
            new(3, request.FetchHeader("x-real-ip")),
            new(4, request.FetchHeader("x-forwarded-for")),
            new(5, request.FetchHeader("REMOTE_ADDR"))
        };
        return seq.Where(a => !string.IsNullOrEmpty(a.Value)).OrderBy(b => b.Key).FirstOrDefault().Value;
    }
    public static string? ClientIp(this IHttpContextAccessor ctx)
    {
        var request = ctx.HttpContext!.Request;
        var seq = new List<KeyValuePair<int, string?>>
        {
            new(1, request.FetchHeader("x-haproxy-ip")),
            new(2, request.FetchHeader("CF-Connecting-IP")),
            new(3, request.FetchHeader("x-real-ip")),
            new(4, request.FetchHeader("x-forwarded-for")),
            new(5, request.FetchHeader("REMOTE_ADDR"))
        };
        return seq.Where(a => !string.IsNullOrEmpty(a.Value)).OrderBy(b => b.Key).FirstOrDefault().Value;
    }
}