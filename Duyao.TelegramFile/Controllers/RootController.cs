using System.Reflection;
using Duyao.ApiBase;
using Duyao.TelegramFile.BaseItem;
using Duyao.TelegramFile.Entity;
using Duyao.TelegramFile.Helper;
using Microsoft.AspNetCore.Mvc;
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

    public RootController(
         ILogger<RootController> logger
        , IConfiguration config
    )
    {
        _configuration = config;
        _logger = logger;
    }

    [HttpGet("")]
    public Task<IActionResult> DefaultRoot()
    {
        return GetVersion("Core");
    }
}