namespace Duyao.ApiBase.Middleware;

public class HttpOptionsMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        return BeginInvoke(context);
    }

    private Task BeginInvoke(HttpContext context)
    {
        if (context.Request.Method == "OPTIONS")
        {
            if (!string.IsNullOrEmpty(context.Request.Headers["Origin"]))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin",
                    new[] { Convert.ToString(context.Request.Headers["Origin"]) });
            }
            else
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            }
            context.Response.Headers.Append("Access-Control-Allow-Headers",
                new[] { "Origin, X-Requested-With, Content-Type, Accept" });
            context.Response.Headers.Append("Access-Control-Allow-Methods",
                new[] { "GET, POST, PUT, DELETE, OPTIONS" });
            context.Response.Headers.Append("Access-Control-Allow-Credentials", new[] { "true" });
            context.Response.StatusCode = 204;
            return context.Response.CompleteAsync();
        }
        return next.Invoke(context);
    }
}