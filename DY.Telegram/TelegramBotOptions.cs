namespace DY.Telegram;

public class TelegramBotOptions
{
    public int ApiId { get; set; }
    public string ApiHash { get; set; } = string.Empty;
    public string BotToken { get; set; } = string.Empty;
    public string SessionFile { get; set; } = string.Empty;
    
}