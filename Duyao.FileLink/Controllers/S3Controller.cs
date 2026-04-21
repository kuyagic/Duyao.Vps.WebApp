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
        using var client = new AmazonS3Client(s3Config.Key, s3Config.Secret, config);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3Config.Bkt,
            Key = path.TrimStart('/'),
            Expires = DateTime.UtcNow.AddSeconds(300)
        };

        request.Verb = HttpVerb.GET;

        var url = client.GetPreSignedURL(request);
        return Task.FromResult<IActionResult>(
            Redirect(url)
        );
    }
}