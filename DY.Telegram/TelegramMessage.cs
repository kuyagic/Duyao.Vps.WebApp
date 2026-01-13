namespace DY.Telegram;

public class TelegramMessage : IDbEntity
{
    public string? Id { get; set; } // RavenDB 会自动生成，或使用 Telegram message ID
    public long TelegramMessageId { get; set; }
    public long ChatId { get; set; }
    public string? ChatTitle { get; set; }
    public long SenderId { get; set; }
    public string? SenderName { get; set; } // 可以是用户昵称或频道/群组标题
    public string? Text { get; set; }
    public DateTime Date { get; set; }
    public bool IsIncoming { get; set; } // 标记消息是接收还是发送
}