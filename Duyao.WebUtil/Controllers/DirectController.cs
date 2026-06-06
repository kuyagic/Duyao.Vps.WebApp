using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Duyao.ApiBase;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.WebUtil.Controllers;

[ApiController]
[Route("[controller]")]
public partial class DirectController : CustomBaseController
{
    private readonly ILogger<DirectController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly string YandexApiTemplate = "https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key=https://disk.yandex.ru";

    public DirectController(
        ILogger<DirectController> logger
        , IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    #region YandexDisk

    [HttpGet("y/d/{hash}")]
    [HttpHead("y/d/{hash}")]
    public async Task<IActionResult> DownloadYandexFile(string hash
        , CancellationToken cancellationToken)
    {
        var hc = _httpClientFactory.CreateClient();
        var url = $"{YandexApiTemplate}/d/{hash}";
        var json = await hc.GetFromJsonAsync<JsonObject>(url
            , cancellationToken
        );
        if (json != null && json.ContainsKey("href"))
        {
            var rhc = _httpClientFactory.CreateClient("NoRedirect");
            var href = json["href"]?.GetValue<string>();
            _logger.LogDebug(href);
            var resp = await rhc.GetAsync(href
                , cancellationToken);
            var redir = resp.Headers.Location?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(redir))
            {
                var uri = new Uri(redir);
                var queryString = HttpUtility.ParseQueryString(uri.Query);

                // 只对filename参数进行编码
                if (queryString["filename"] != null)
                {
                    var originalFilename = queryString["filename"];
                    var encodedFilename = originalFilename;
                    queryString["filename"] = encodedFilename;
                }

                // 重新构建URL
                var uriBuilder = new UriBuilder(uri)
                {
                    Query = queryString.ToString()
                };

                var safeUrl = uriBuilder.ToString();
                return Redirect(safeUrl);
            }
        }

        return NotFound();
    }

    [HttpGet("y/i/{hash}")]
    [HttpHead("y/i/{hash}")]
    public async Task<IActionResult> DownloadYandeImage(string hash
        , CancellationToken cancellationToken)
    {
        var hc = _httpClientFactory.CreateClient();
        var url = $"{YandexApiTemplate}/i/{hash}";
        var json = await hc.GetFromJsonAsync<JsonObject>(url
            , cancellationToken
        );
        if (json != null && json.ContainsKey("href"))
        {
            var rhc = _httpClientFactory.CreateClient("NoRedirect");
            var resp = await rhc.GetAsync(json["href"]?.GetValue<string>()
                , cancellationToken);
            var redir = resp.Headers.Location?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(redir))
            {
                var imgRedir = redir
                    .Replace("disposition=attachment", "disposition=inline");

                var uri = new Uri(imgRedir);
                var queryString = HttpUtility.ParseQueryString(uri.Query);

                // 只对filename参数进行编码
                if (queryString["filename"] != null)
                {
                    var originalFilename = queryString["filename"];
                    var encodedFilename = originalFilename;
                    queryString["filename"] = encodedFilename;
                }

                // 重新构建URL
                var uriBuilder = new UriBuilder(uri)
                {
                    Query = queryString.ToString()
                };

                var safeUrl = uriBuilder.ToString();
                return Redirect(safeUrl);
            }
        }

        return NotFound();
    }

    #endregion

    #region Lifebox

    [HttpGet("box/{hash}")]
    [HttpHead("box/{hash}")]
    public async Task<IActionResult> DownloadMylifeboxFirstShareFile(string hash
        , CancellationToken cancellationToken)
    {
        var hc = _httpClientFactory.CreateClient();
        var lifeboxApiTemplate =
            $"https://mylifebox.com/api/share/public/list?publicToken={hash}&language=tr&sortBy=name&sortOrder=ASC&page=0&size=1";
        var json = await hc.GetFromJsonAsync<List<JsonObject>>(lifeboxApiTemplate
            , cancellationToken
        );
        if (json != null && json.Any() && json.First().ContainsKey("tempDownloadURL"))
        {
            var f = json.First();
            var durl = f["tempDownloadURL"]?.GetValue<string>();
            var dname = f["name"]?.GetValue<string>() ?? string.Empty;
            return Redirect($"{durl}&filename={Uri.EscapeDataString(dname)}");
        }

        return NotFound();
    }

    #endregion
    
    #region CloudApp
    [HttpGet("cloudapp/{hash}")]
    [HttpHead("cloudapp/{hash}")]
    public async Task<IActionResult> DownloadCloudAppFile(string hash, CancellationToken cancellationToken)
    {
        var id = hash;
        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        string pageUrl = $"https://share.zight.com/{id}";
        var html = await http.GetStringAsync(pageUrl, cancellationToken);

        // 匹配所有 cdn.zight.com/items/{id}/... 链接 (含 \u0026 转义的查询参数), 排除 thumbnail
        var matches = Regex.Matches(html,
            @"https?://[^""\s<>]*?cdn\.zight\.com/items/" + Regex.Escape(id) +
            @"/[^""\s<>]+",
            RegexOptions.IgnoreCase);

        // 优先选 source=download 的 (携带 response-content-disposition 保留原始文件名)
        // HTML 中 & 被转义为 \u0026，需还原
        foreach (Match m in matches)
        {
            if (!m.Value.Contains("thumbnail", StringComparison.OrdinalIgnoreCase)
                && m.Value.Contains("source=download", StringComparison.OrdinalIgnoreCase))
                return Redirect(m.Value.Replace(@"\u0026", "&"));
        }

        // 其次选 viewer 裸链
        foreach (Match m in matches)
        {
            if (!m.Value.Contains("thumbnail", StringComparison.OrdinalIgnoreCase))
                return Redirect(m.Value.Replace(@"\u0026", "&").Split('?')[0]);
        }

        // 备用: 从 thumbnail URL 中提取真实文件路径
        var m2 = Regex.Match(html,
            @"https?://thumbnail\.cdn\.zight\.com/[pit]/" + Regex.Escape(id) +
            @"/[^""?\s]*?/([^""?\s]+?cdn\.zight\.com/items/[^""\s<>]+)",
            RegexOptions.IgnoreCase);

        if (m2.Success)
        {
            var targetUrl = "https://" + m2.Groups[1].Value.Replace(@"\u0026", "&").Split('?')[0];
            return Redirect(targetUrl);
        }

        return NotFound();
    }
    #endregion
}