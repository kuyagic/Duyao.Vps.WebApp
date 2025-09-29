namespace Duyao.TelegramFile.Entity;

public class WTelegramClientConfig
{
    public int? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? BotToken { get; set; }
    public string? MtProxyUrl { get; set; }
    public int? BotResponse { get; set; }
    public string? HostName { get; set; }
    public long[]? AllowUserId { get; set; }

    public string? RemotePublicKey { get; set; }
    public string? LocalPrivateKey { get; set; }
}