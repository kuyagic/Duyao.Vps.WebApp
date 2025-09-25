using System.Reflection;
using Duyao.TelegramFile.Entity;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.ApiBase;

public class CustomBaseController : ControllerBase
{
        
    protected static string ServerPrivateKey => "3064020100301406072a8648ce3d020106092b240303020801010204493047" +
                                                "0201010414d48da9f2c6d099ca8e7258e467b5bc82af43af5fa12c032a0004" +
                                                "2763d236cbbf5cc3209ed22aa40a8bf756fd7b3594df6c446ea0ead3dc0a9f" +
                                                "a788e263c495a1865c";
    protected string ServerPublicKey => "3042301406072a8648ce3d020106092b2403030208010102032a00042763d236cbbf5cc32" +
                                        "09ed22aa40a8bf756fd7b3594df6c446ea0ead3dc0a9fa788e263c495a1865c";

    protected string? GetClientIp()
    {
        if (HttpContext.Request.Headers.ContainsKey("x-haproxy-ip"))
            return HttpContext.Request.Headers["x-haproxy-ip"].Distinct().FirstOrDefault();
        // 尝试从 X-Forwarded-For 标头中获取 IP 地址（适用于反向代理）
        if (HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            return HttpContext.Request.Headers["X-Forwarded-For"].Distinct().FirstOrDefault();
        // 否则，从 Connection.RemoteIpAddress 获取 IP 地址
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "<NULL>";
    }

    protected string? GetHeaderValue(string headerName)
    {
        return Request.Headers.TryGetValue(headerName, out var value)
            ? value.Distinct().FirstOrDefault()
            : null;
    }

    public Task<IActionResult> GetVersion(string appName)
    {
        // 获取当前程序集
        var assembly = Assembly.GetExecutingAssembly();
        // 获取版本信息
        var version = assembly.GetName().Version;
        var verString = string.Format(BuildInfo.BuildTime, appName, version);
        return Task.FromResult<IActionResult>(Ok(new ApiResponse
        {
            Message = $"{verString} is running"
        }));
    }
    
}