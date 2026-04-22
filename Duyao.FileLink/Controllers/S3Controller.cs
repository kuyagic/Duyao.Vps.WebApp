using Amazon.S3;
using Amazon.S3.Model;
using Duyao.ApiBase;
using Duyao.FileLink.Helper;
using Duyao.TelegramFile.Entity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Duyao.FileLink.Controllers;

[ApiController]
[Route("[controller]")]
public class S3Controller : CustomBaseController
{
    private readonly ILogger<S3Controller> _logger;
    private readonly IConfiguration _configuration;

    public S3Controller(
        ILogger<S3Controller> logger
        , IConfiguration opt
    )
    {
        _logger = logger;
        _configuration = opt;
    }

    [HttpGet("")]
    public Task<IActionResult> S3Version()
    {
        return GetVersion("S3 Presign");
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

    [HttpPost("{name}")]
    public Task<IActionResult> GetConfig(string name)
    {
        var s3ConfigList = _configuration.GetSection("S3List").Get<List<S3Config>>();
        var s3Config = s3ConfigList?.FirstOrDefault(x => x.Name == name);
        return Task.FromResult<IActionResult>(Ok(s3Config?.Bkt));
    }

    [HttpGet("{name}/{**path}")]
    [HttpHead("{name}/{**path}")]
    public async Task<IActionResult> DefaultRoot(string name, string path)
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
            _logger.LogInformation($"s3={name},not found");
            return Ok(new ApiResponse
            {
                Ok = false,
                Message = "NotFound"
            });
        }

        if (string.IsNullOrEmpty(resultPath.TrimStart('/')))
        {
            return Ok(new ApiResponse
            {
                Ok = false,
                Message = "NotFound"
            });
        }

        var config = new AmazonS3Config
        {
            ServiceURL = s3Config.Url,
            ForcePathStyle = true
        };

        using var client = new AmazonS3Client(s3Config.Key, s3Config.Secret, config);
        var testFile = new GetObjectMetadataRequest
        {
            BucketName = s3Config.Bkt,
            Key = resultPath.TrimStart('/'),
        };
        try
        {
            var testResp = await client.GetObjectMetadataAsync(testFile);
            if (testResp.ContentLength == 0)
            {
                return Ok(new ApiResponse
                {
                    Ok = false,
                    Message = "Forbidden"
                });
            }
        }
        catch
        {
            return Ok(new ApiResponse
            {
                Ok = false,
                Message = "Forbidden"
            });
        }

        _logger.LogInformation($"s3={name},path={resultPath}");
        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3Config.Bkt,
            Key = resultPath.TrimStart('/'),
            Expires = DateTime.UtcNow.AddSeconds(300),
            Verb = HttpVerb.GET
        };

        var url = await client.GetPreSignedURLAsync(request);
        _logger.LogInformation(url);
        return Redirect(url);
    }
}