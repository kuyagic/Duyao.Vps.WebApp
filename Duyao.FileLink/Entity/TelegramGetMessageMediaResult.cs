using TL;

namespace Duyao.TelegramFile.Entity;

public class TelegramGetMessageMediaResult
{
    public enum TelegramMessageMediaType
    {
        Undefined = 0,
        Document = 1,
        Photo = 2
    }

    public TelegramMessageMediaType Type { get; set; } = TelegramMessageMediaType.Undefined;
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public Document? DocumentInfo { get; set; }
    public Photo? PhotoInfo { get; set; }

    public override string ToString()
    {
        return Type switch
        {
            TelegramMessageMediaType.Document => $"{Type.ToString()}=>{DocumentInfo?.Filename}",
            TelegramMessageMediaType.Photo => $"{Type.ToString()}=>{PhotoInfo?.ID}",
            _ => Type.ToString()
        };
    }
}