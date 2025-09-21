using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Duyao.SmsForward.Controllers;

[ApiController]
[Route("")]
public class RootController : ControllerBase
{
    private readonly IOptions<AppConfigObject> _config;
    private readonly ILogger<RootController> _logger;

    public RootController(ILogger<RootController> logger, IOptions<AppConfigObject> option
    )
    {
        _logger = logger;
        _config = option;
    }

    [HttpPost]
    [Route("kv")]
    public async Task<IActionResult> ProcessKv([FromBody] JObject obj)
    {
        if (obj == null)
        {
            _logger.LogInformation("obj is <NULL>");
        }

        var cfg = _config.Value.Sms;

        var checkKey = (new string[] { "address", "text", "date_sent", "tag" })
            .Any(p => !obj.ContainsKey(p));
        if (checkKey)
        {
            return await Task.Run(() => BadRequest("key not exists"));
        }

        //DateTime.Parse("1970-01-01 0:00:00").AddMilliseconds(1715936332994).ToLocalTime()
        var smsFrom = obj["address"].ToString();
        var smsText = obj["text"].ToString();
        var smsDate = obj["date_sent"].ToString();
        var smsTag = (obj["tag"] ?? "").ToString();

        smsText = System.Net.WebUtility.HtmlEncode(smsText);

        if (!long.TryParse(smsDate, out var lDate))
        {
            return await Task.Run(() => BadRequest("Date Error"));
        }

        var timeZoneInfoUnix = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var dSmsDate = DateTime.Parse("1970-01-01 0:00:00").AddMilliseconds(lDate).ToLocalTime();
        var retSmsDateString = TimeZoneInfo.ConvertTime(dSmsDate, timeZoneInfoUnix).ToString("yyyy/MM/dd HH:mm:ss");

        _logger.LogInformation($"sms DateTime=[{retSmsDateString}]");

        var parseMode = "html";
        var fwdObj = cfg.forward;
        var fwdUrl = $"{fwdObj.api_host}/bot{fwdObj.telegram_bot_id}/sendMessage";
        var hc = new HttpClient();
        foreach (var pattern in cfg.pattern)
        {
            var m = System.Text.RegularExpressions.Regex.Match(smsText, pattern);
            if (m.Success)
            {
                _logger.LogInformation("需要转发");

                var mxList = System.Text.RegularExpressions.Regex.Matches(smsText, "\\d{4,6}");
                foreach (Match mx in mxList)
                {
                    if (m.Index - 10 <= mx.Index && mx.Index <= m.Index + 10)
                    {
                        smsText = smsText.Replace(mx.ToString(), $" <code>{mx}</code> ");
                    }
                }

                var message = new StringBuilder();
                message.AppendLine(smsText);
                message.AppendLine($"Tag: #SMS , #m{DateTime.Now:yyyyMM} , #d{DateTime.Now:yyyyMMdd}");
                message.AppendLine($"Device: <code>{smsTag}</code>");
                message.AppendLine($"DateTime <code>{retSmsDateString}</code>");
                message.AppendLine($"From <code>{smsFrom}</code>");
                var data = new
                {
                    chat_id = fwdObj.chat_id,
                    text = message.ToString(),
                    parse_mode = parseMode
                };
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var r = hc.PostAsync(fwdUrl, new StringContent(json, Encoding.UTF8, "application/json"))
                    .Result.Content.ReadAsStringAsync()
                    .Result;

                return await Task.Run(() => Ok(r));
            }
        }

        _logger.LogInformation("Catch All");
        var catchAllMsg = new StringBuilder();
        catchAllMsg = new StringBuilder();
        catchAllMsg.AppendLine(smsText);
        catchAllMsg.AppendLine($"Tag: #SMS , #m{DateTime.Now:yyyyMM} , #d{DateTime.Now:yyyyMMdd}");
        catchAllMsg.AppendLine($"Device: <code>{smsTag}</code>");
        catchAllMsg.AppendLine($"DateTime <code>{retSmsDateString}</code>");
        catchAllMsg.AppendLine($"From <code>{smsFrom}</code>");
        var catchAllData = new
        {
            chat_id = fwdObj.catch_all,
            text = catchAllMsg.ToString(),
            parse_mode = parseMode
        };
        _logger.LogDebug(catchAllMsg.ToString());
        hc = new HttpClient();
        var catchAllResult = hc.PostAsync(fwdUrl,
                new StringContent(System.Text.Json.JsonSerializer.Serialize(catchAllData), Encoding.UTF8,
                    "application/json"))
            .Result.Content.ReadAsStringAsync()
            .Result;
        _logger.LogInformation(catchAllResult);
        return await Task.Run(() => Ok("CatchALL Sent"));
    }

    [HttpPost]
    [Route("fwd/{device}")]
    public async Task<IActionResult> PrintKvData(string device, [FromBody] JObject? obj)
    {
        if (obj == null)
        {
            _logger.LogInformation("obj is <NULL>");
            obj = new JObject();
        }

        obj["tag"] = device;
        return await ProcessKv(obj);
    }

    [HttpGet]
    [Route("me")]
    public async Task<IActionResult> GetMe()
    {
        return await Task.Run(() => Ok(_config.Value.Sms.pattern));
    }

    [HttpGet]
    [Route("version")]
    public async Task<IActionResult> GetVersion()
    {
        // 获取当前程序集
        var assembly = Assembly.GetExecutingAssembly();
        // 获取版本信息
        var version = assembly.GetName().Version;
        return await Task.Run(() => Ok(string.Format(BuildInfo.BuildTime, version)));
    }
}