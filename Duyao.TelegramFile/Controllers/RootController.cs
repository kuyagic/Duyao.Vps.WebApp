using Duyao.TelegramFile.BaseItem;
using Duyao.TelegramFile.Entity;
using Duyao.TelegramFile.Helper;
using Microsoft.AspNetCore.Mvc;
using telegram.bot.webfile.Helper;
using TL;
using WTelegram;

namespace Duyao.TelegramFile.Controllers;

[ApiController]
[Route("")]
public class RootController : CustomBaseController
{
    private readonly ILogger<RootController> _logger;
    private readonly IConfiguration _configuration;
    private readonly Client _telegramClient;

    public RootController(Client telegramClient
        ,ILogger<RootController> logger
        , IConfiguration config
        )
    {
        _configuration = config;
        _telegramClient = telegramClient;
        _logger = logger;
    }

    [HttpGet("")]
    public Task<IActionResult> DefaultRoot()
    {
        return Task.FromResult<IActionResult>(Ok(new ApiResponse
        {
            Message = "corebot running"
        }));
    }
    
    public async Task<IActionResult> DownloadChatMedia(long chatId, int chatMessageId
        , CancellationToken cancellationToken
        , bool isFromChannel
        )
    {
        Response.Headers.Append("x-client-ip", GetClientIp());
        
        var mediaRet = await _telegramClient.GetMessageMedia(chatId, chatMessageId, isFromChannel);

        if (!mediaRet.Success)
        {
            _logger.LogWarning($"{mediaRet.Message}, {chatId},{chatMessageId}");
            
            return NotFound("File not Found"); // 404 Not Found
        }
        
        if (mediaRet.Type == TelegramGetMessageMediaResult.TelegramMessageMediaType.Document)
        {
            var doc = mediaRet.DocumentInfo;
            if (doc != null)
            {
                Response.Headers.Append("x-mime", doc.mime_type ?? "not_found");
                Response.Headers.Append("x-file-location", $"{chatId}/{chatMessageId}/{doc.ID}");
                return await DownloadChatDocument(doc.ToFileLocation()
                    , doc.size
                    , doc.Filename
                    , doc.mime_type
                    , cancellationToken
                );
            }
        }

        if (mediaRet.Type == TelegramGetMessageMediaResult.TelegramMessageMediaType.Photo)
        {
            var doc = mediaRet.PhotoInfo;
            Response.Headers.Append("x-mime", "photo");
            Response.Headers.Append("x-file-location", $"{chatId}/{chatMessageId}/{doc?.ID}");
            Response.ContentType = "image/png";
            Response.Headers.Append("Content-Disposition", "inline");
            await _telegramClient.DownloadFileAsync(mediaRet.PhotoInfo, Response.Body);
            return new EmptyResult();
        }
        _logger.LogWarning($"{mediaRet.Message}, {chatId},{chatMessageId}");
        return NotFound("File Not Found"); // 404 Not Found
    }

    [HttpGet("{hash}")]
    [HttpHead("{hash}")]
    public async Task<IActionResult> DownloadChatFileViaHash(string hash
        , CancellationToken cancellationToken)
    {
        try
        {
            var ret = Utils.RevealTelegramFileInfo(hash);
            _logger.LogInformation($"Download From {GetClientIp()},{hash}");
            return await DownloadChatMedia(ret.chatId, (int)ret.messageId, cancellationToken, false);
        }
        catch(Exception exp)
        {
            _logger.LogWarning($"Hash {hash} error,[{exp.Message}]");
            return BadRequest("File Not Found");
        }
    }
    
    #region Internal Download Logic

    private Task<IActionResult> DownloadChatDocument(InputDocumentFileLocation fileLocation
        , long fileSize
        , string fileName
        , string? mimeType
        , CancellationToken cancellationToken
    )
    {
        _logger.LogInformation($"File ID={fileLocation?.id}");
        _logger.LogInformation($"File Size={Utils.FormatSize(fileSize)},({fileSize})");
        _logger.LogInformation($"File Name={fileName}");
        _logger.LogInformation($"Mime Type={mimeType}");
        
        if (fileName.Equals("sticker.webm", StringComparison.InvariantCultureIgnoreCase)
            && mimeType!=null
            && mimeType.Equals("video/webm", StringComparison.InvariantCultureIgnoreCase))
        {
            //tg 贴图 不完美验证，修改文件名。
            fileName = $"sticker_{fileLocation?.id}.webm.mp4";
        }
        // 2. Handle Range Request
        long fromBytes = 0;
        var untilBytes = fileSize - 1;
        var containRange = Request.Headers.ContainsKey("Range");
        if (containRange)
        {
            #region 有Range请求头

            var rangeHeader = Request.Headers["Range"].ToString();
            if (rangeHeader.StartsWith("bytes="))
            {
                var ranges = rangeHeader.Substring("bytes=".Length).Split('-');
                if (long.TryParse(ranges[0], out fromBytes))
                {
                    if (ranges.Length > 1 && long.TryParse(ranges[1], out untilBytes))
                    {
                        // Valid range with both start and end
                    }
                    else
                    {
                        untilBytes = fileSize - 1; // Range with only start
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid Range Header,not x-y");
                    return Task.FromResult<IActionResult>(StatusCode(416, "Invalid Range Header")); // Or better error handling
                }
            }
            else
            {
                _logger.LogWarning("Invalid Range Header,not start with bytes=");
                return Task.FromResult<IActionResult>(StatusCode(416, "Invalid Range Header")); // Or better error handling
            }

            #endregion

            Response.StatusCode = 206;
        }
        else
        {
            Response.StatusCode = 200;
        }

        if (untilBytes >= fileSize || fromBytes < 0 || untilBytes < fromBytes)
        {
            Response.Headers.Append("Content-Range", $"bytes */{fileSize}");
            _logger.LogWarning($"Range Not Satisfiable, invalid form {fromBytes}-{untilBytes}");
            return Task.FromResult<IActionResult>(StatusCode(416, "Range Not Satisfiable"));
        }

        // 3. Streaming Logic
        var chunkSize = 1024 * 1024;
        var reqLength = untilBytes - fromBytes + 1;
        
        
        var defContentType = "application/octet-stream";
        var cd = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
        if (!string.IsNullOrEmpty(mimeType) && mimeType.StartsWith("image/"))
        {
            _logger.LogInformation($"file maybe image {mimeType}");
            cd = "inline;";
            defContentType = mimeType;
        }
        
        Response.ContentLength = reqLength;
        Response.Headers["Accept-Ranges"] = "bytes";

        Response.Headers.Append("Content-Disposition", cd);

        if (containRange) Response.Headers.Append("Content-Range", $"bytes {fromBytes}-{untilBytes}/{fileSize}");
        if (Request.Method == "HEAD") return Task.FromResult<IActionResult>(new EmptyResult());

        return Task.FromResult<IActionResult>(new PushStreamResult(async (stream, context) =>
        {
            await stream.WriteAsyncEnumerableBytes(
                _telegramClient.YieldFileAsync(fileLocation
                    ,fromBytes
                    ,untilBytes
                    ,fileSize
                    , chunkSize
                    , cancellationToken
                )
            );
        }, defContentType)); // 设置 Content-Type
    }

    #endregion
}