using System.Text.Json.Serialization;

namespace Duyao.NsTunnel;

public class AppConfig
{
    public string? ApiData { get; set; }
    public string? Netns { get; set; }
    public string? LicenseCheckTicket { get; set; }
    public string? VpnUser { get; set; }
    public string? VpnPassword { get; set; }
    public string? UnitConfig { get; set; }
}

public class NstApiResponse
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}