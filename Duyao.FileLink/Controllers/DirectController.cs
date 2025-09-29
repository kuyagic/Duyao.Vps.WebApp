using System.Text.Json.Nodes;
using System.Web;
using Duyao.ApiBase;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.FileLink.Controllers;

[ApiController]
[Route("[controller]")]
public partial class DirectController : CustomBaseController
{
    private readonly ILogger<DirectController> _logger;

    private static string ydxApiTemplate = "https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key=https://disk.yandex.ru";

    public DirectController(
        ILogger<DirectController> logger
    )
    {
        _logger = logger;
    }

    #region YandexDisk

    [HttpGet("y/d/{hash}")]
    [HttpHead("y/d/{hash}")]
    public async Task<IActionResult> DownloadYandexFile(string hash
        , CancellationToken cancellationToken)
    {
        var hc = new HttpClient();
        var url = $"{ydxApiTemplate}/d/{hash}";
        var json = await hc.GetFromJsonAsync<JsonObject>(url
            , cancellationToken
        );
        if (json != null && json.ContainsKey("href"))
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var rhc = new HttpClient(handler);
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
        var hc = new HttpClient();
        var url = $"{ydxApiTemplate}/i/{hash}";
        var json = await hc.GetFromJsonAsync<JsonObject>(url
            , cancellationToken
        );
        if (json != null && json.ContainsKey("href"))
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var rhc = new HttpClient(handler);
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
        var hc = new HttpClient();
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
}