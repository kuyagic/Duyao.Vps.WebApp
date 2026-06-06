using Duyao.ApiBase;
using Microsoft.AspNetCore.Mvc;
using WTelegram;

namespace Duyao.WebUtil.Controllers;

[ApiController]
[Route("")]
public class RootController : CustomBaseController
{
    [HttpGet("")]
    public Task<IActionResult> DefaultRoot()
    {
        return GetVersion("Root");
    }
}