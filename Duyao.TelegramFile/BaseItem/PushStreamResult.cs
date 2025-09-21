using Microsoft.AspNetCore.Mvc;

namespace Duyao.TelegramFile.BaseItem;

public class PushStreamResult : IActionResult
{
    private readonly string _contentType;
    private readonly Func<Stream, ActionContext, Task> _writeAction;

    public PushStreamResult(Func<Stream, ActionContext, Task> writeAction, string contentType)
    {
        _writeAction = writeAction;
        _contentType = contentType;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.ContentType = _contentType;
        await _writeAction(context.HttpContext.Response.Body, context);
    }
}