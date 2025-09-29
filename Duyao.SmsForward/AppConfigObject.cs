using Newtonsoft.Json;

namespace Duyao.SmsForward;

public class AppConfigObject
{
    [JsonProperty("Sms")] public SmsConfig Sms { get; set; }
}

public class SmsConfig
{
    public ForwardInfo forward { get; set; }
    public string[] pattern { get; set; }
}

public class ForwardInfo
{
    public string api_host { get; set; } = "https://api.telegram.org";
    public string telegram_bot_id { get; set; }
    public string chat_id { get; set; }
    public string catch_all { get; set; }
}