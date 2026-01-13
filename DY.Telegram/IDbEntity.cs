namespace DY.Telegram;

public interface IDbEntity
{
    public string? Id { get; set; } // RavenDB 会自动生成，或使用 Telegram message ID
}