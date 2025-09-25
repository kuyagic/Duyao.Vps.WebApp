using System.Text.Json.Serialization;

namespace Duyao.TelegramFile.Entity;

public class ApiResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;

    [JsonPropertyName("message")] public string? Message { get; set; }
}