using Microsoft.AspNetCore.Mvc;

namespace Duyao.FileLink.Controllers;

public partial class DirectController
{
    [HttpGet("")]
    public Task<IActionResult> DefaultRoot()
    {
        return GetVersion("DirectLink");
    }

    [HttpGet("y")]
    public Task<IActionResult> DefaultY()
    {
        return GetVersion("DirectLink Ydx");
    }

    [HttpGet("box")]
    public Task<IActionResult> DefaultBox()
    {
        return GetVersion("DirectLink Box");
    }
}