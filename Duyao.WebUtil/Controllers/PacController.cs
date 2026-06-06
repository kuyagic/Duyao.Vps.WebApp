using Duyao.ApiBase;
using Duyao.TelegramFile.Entity;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.WebUtil.Controllers;

[ApiController]
[Route("[controller]")]
public class PacController : CustomBaseController
{
    private const string CidrListUrl = "https://cdn.jsdelivr.net/gh/17mon/china_ip_list@master/china_ip_list.txt";

    private readonly ILogger<PacController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public PacController(ILogger<PacController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private static string CidrToMask(byte prefix)
    {
        uint mask = (0xFFFFFFFFu << (32 - prefix)) & 0xFFFFFFFFu;
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private async Task<string> GenerateSocksPac(string host, int port, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var response = await httpClient.GetAsync(CidrListUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var cidrLines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var conditions = new List<string>();
        foreach (var line in cidrLines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var parts = line.Trim().Split('/');
            if (parts.Length == 2 && byte.TryParse(parts[1], out var prefix))
            {
                string mask = CidrToMask(prefix);
                conditions.Add($"        isInNet(host, \"{parts[0]}\", \"{mask}\") ||");
            }
        }

        // 补充内网段
        conditions.AddRange(
        [
            "        isPlainHostName(host) ||",
            "        isInNet(host, \"10.0.0.0\", \"255.0.0.0\") ||",
            "        isInNet(host, \"172.16.0.0\", \"255.240.0.0\") ||",
            "        isInNet(host, \"192.168.0.0\", \"255.255.0.0\") ||",
            "        isInNet(host, \"127.0.0.0\", \"255.0.0.0\")"
        ]
            );

        // 修掉最后的 " ||"
        if (conditions.Count > 0)
            conditions[^1] = conditions[^1].Replace(" ||", ";");

        string pac = $$"""
                       'use strict';

                       var autoproxy = 'SOCKS5 {{host}}:{{port}}; DIRECT';
                       var direct = 'DIRECT';

                       function FindProxyForLocalDirect(url, host) {
                           if (
                       {{string.Join('\n', conditions)}}
                           ) {
                               return true;
                           }
                           return false;
                       }

                       function FindProxyForURL(url, host) {
                           if (FindProxyForLocalDirect(url, host)) {
                               return direct;
                           }
                           return autoproxy;
                       }
                       """;

        return pac;
    }

    /// <summary>
    /// 通过 Query 参数生成 PAC: /Pac?host=127.0.0.1&amp;port=1080
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> GetPacByQuery(
        [FromQuery] string host = "127.0.0.1",
        [FromQuery] int port = 1080,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            return BadRequest(new ApiResponse { Ok = false, Message = "Invalid host or port" });

        try
        {
            var pac = await GenerateSocksPac(host, port, cancellationToken);
            return Content(pac, "application/x-ns-proxy-autoconfig");
        }
        catch (Exception exp)
        {
            _logger.LogError(exp, "生成 PAC 失败");
            return StatusCode(502, new ApiResponse { Ok = false, Message = exp.Message });
        }
    }

    /// <summary>
    /// 通过路径参数生成 PAC: /Pac/127.0.0.1/1080
    /// </summary>
    [HttpGet("{host}/{port:int}")]
    public async Task<IActionResult> GetPacByPath(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            return BadRequest(new ApiResponse { Ok = false, Message = "Invalid host or port" });

        try
        {
            var pac = await GenerateSocksPac(host, port, cancellationToken);
            return Content(pac, "application/x-ns-proxy-autoconfig");
        }
        catch (Exception exp)
        {
            _logger.LogError(exp, "生成 PAC 失败");
            return StatusCode(502, new ApiResponse { Ok = false, Message = exp.Message });
        }
    }
}
