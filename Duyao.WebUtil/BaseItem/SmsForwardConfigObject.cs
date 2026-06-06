using Newtonsoft.Json;

namespace Duyao.WebUtil.BaseItem;

public class SmsConfig
{
    public ForwardInfo Forward { get; set; }
    public string[] Pattern { get; set; }
}

public class ForwardInfo
{
    public string ApiHost { get; set; } = "https://api.telegram.org";
    public string TelegramBotId { get; set; }
    public string ChatId { get; set; }
    public string CatchAll { get; set; }
}