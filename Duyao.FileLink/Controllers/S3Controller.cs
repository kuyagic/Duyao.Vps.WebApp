using Amazon.S3;
using Amazon.S3.Model;
using Duyao.ApiBase;
using Duyao.FileLink.Helper;
using Duyao.TelegramFile.Entity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.FileLink.Controllers;

public class S3Controller : CustomBaseController
{
    private readonly ILogger<DirectController> _logger;
    private readonly IConfiguration _configuration;

    public S3Controller(
        ILogger<DirectController> logger
        , IConfiguration opt
    )
    {
        _logger = logger;
        _configuration = opt;
    }

    private string? ExtractS3KeyFromPath(string? fullPath, string configName)
    {
        // fullPath 示例: /api/s3/wsb/MyFolder/MyFile.txt
        // 需要提取: MyFolder/MyFile.txt

        var prefix = $"/api/s3/{configName}/";

        if (fullPath?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            // 使用 OrdinalIgnoreCase 匹配前缀，但保留后续部分的原始大小写
            var key = fullPath.Substring(prefix.Length);
            return Uri.UnescapeDataString(key); // 处理 URL 编码
        }

        return fullPath;
    }

    [HttpPost("s3/{name}")]
    public Task<IActionResult> GetConfig(string name)
    {
        var s3ConfigList = _configuration.GetSection("S3List").Get<List<S3Config>>();
        var s3Config = s3ConfigList?.FirstOrDefault(x => x.Name == name);
        return Task.FromResult<IActionResult>(Ok(s3Config?.Bkt));
    }

    [HttpGet("s3/{name}/{**path}")]
    [HttpHead("s3/{name}/{**path}")]
    public Task<IActionResult> DefaultRoot(string name, string path)
    {
        var fullPath = HttpContext.Request.Path.Value;

        var s3KeyFromPath = ExtractS3KeyFromPath(fullPath, name);
        var prefix = $"/s3/{name}";
        var resultPath = "/";
        if (s3KeyFromPath?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            resultPath = s3KeyFromPath.Remove(0, prefix.Length);
        }

        var s3ConfigList = _configuration.GetSection("S3List").Get<List<S3Config>>();
        var s3Config = s3ConfigList?.FirstOrDefault(x => x.Name == name);
        if (s3Config == null)
        {
            return Task.FromResult<IActionResult>(NotFound());
        }

        var config = new AmazonS3Config
        {
            ServiceURL = s3Config.Url,
            ForcePathStyle = true
        };
        _logger.LogInformation($"s3={name},path={s3KeyFromPath}");
        using var client = new AmazonS3Client(s3Config.Key, s3Config.Secret, config);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3Config.Bkt,
            Key = resultPath.TrimStart('/'),
            Expires = DateTime.UtcNow.AddSeconds(300),
            Verb = HttpVerb.GET
        };

        var url = client.GetPreSignedURL(request);
        _logger.LogInformation(url);
        return Task.FromResult<IActionResult>(
            Redirect(url)
        );
    }
}