using System.Collections.Concurrent;
using Duyao.ApiBase;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.WebUtil.Controllers;

[ApiController]
[Route("redir")]
public class RedirectController : CustomBaseController
{
    private readonly ILogger<RedirectController> _logger;
    private readonly string _redirectFile;

    /// <summary>
    /// 原子替换整个字典，避免 LoadRedirects 中 Clear() 导致请求瞬时空窗
    /// </summary>
    private ConcurrentDictionary<string, string> _redirects = new(StringComparer.OrdinalIgnoreCase);

    public RedirectController(
        ILogger<RedirectController> logger,
        IConfiguration config
    )
    {
        _logger = logger;
        _redirectFile = config.GetValue<string>("RedirectFile") ?? "/app/redir.conf";

        // 首次加载
        ReloadFromFile();
    }

    [HttpGet("")]
    public Task<IActionResult> DefaultRoot()
    {
        return GetVersion("Redirect");
    }

    /// <summary>
    /// 手动触发重载：GET /redir/reload
    /// </summary>
    [HttpPost("reload")]
    public IActionResult Reload()
    {
        ReloadFromFile();
        return Ok(new { success = true, count = _redirects.Count, file = _redirectFile });
    }

    [HttpGet("{key}")]
    [HttpHead("{key}")]
    public Task<IActionResult> DoRedirect(string key)
    {
        ReloadFromFile();
        try
        {
            if (!_redirects.TryGetValue(key, out var url))
            {
                _logger.LogWarning("Redirect key not found: {Key}", key);
                return Task.FromResult<IActionResult>(NotFound());
            }

            _logger.LogInformation("Redirecting key {Key} to {Url}", key, url);

            // 对 HEAD 请求：只返回状态码 + Headers，不带 Body
            if (Request.Method == "HEAD")
            {
                Response.StatusCode = 302;
                Response.Headers.Location = url;
                return Task.FromResult<IActionResult>(new EmptyResult());
            }

            // 对 GET 请求：返回完整重定向
            return Task.FromResult<IActionResult>(Redirect(url));
        }
        catch (Exception exception)
        {
            return Task.FromException<IActionResult>(exception);
        }
    }

    /// <summary>
    /// 从文件重新加载所有重定向映射（原子替换）
    /// </summary>
    private void ReloadFromFile()
    {
        var newDict = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!System.IO.File.Exists(_redirectFile))
        {
            _logger.LogWarning("配置文件未找到: {File}", _redirectFile);
            _redirects = newDict;
            return;
        }

        try
        {
            var lines = System.IO.File.ReadAllLines(_redirectFile);
            int loaded = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var parts = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var k = parts[0].Trim('/');
                    var url = parts[1].Trim();
                    if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        newDict[k] = url;
                        loaded++;
                    }
                    else
                    {
                        _logger.LogWarning("无效 URL，跳过: '{Key}' → '{Url}'", k, url);
                    }
                }
                else
                {
                    _logger.LogWarning("格式错误，跳过: {Line}", line);
                }
            }

            // 原子替换
            _redirects = newDict;
            _logger.LogInformation("重载完成: {Count} 条映射 (文件: {File})", loaded, _redirectFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 {File} 出错: {Msg}", _redirectFile, ex.Message);
            // 保留旧数据，不替换
        }
    }

}