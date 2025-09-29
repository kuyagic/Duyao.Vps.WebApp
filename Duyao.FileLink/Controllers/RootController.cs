using Duyao.ApiBase;
using Microsoft.AspNetCore.Mvc;
using WTelegram;

namespace Duyao.FileLink.Controllers;

[ApiController]
[Route("")]
public class RootController : CustomBaseController
{
    private readonly ILogger<RootController> _logger;
    private readonly IConfiguration _configuration;

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
        return GetVersion("Root");
    }
}