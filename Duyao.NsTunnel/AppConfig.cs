using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duyao.NsTunnel;

// 假设您已经有一个名为 AppJsonSerializerContext 的上下文
[JsonSourceGenerationOptions(WriteIndented = false)] // 根据需要配置
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(JsonElement))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
public class AppConfig
{
    
    public string ApiData { get; set; }
    public string HealthCheckUrl { get; set; }
    public string VpnUser { get; set; }
    public string VpnPassword { get; set; }
    public string UnitConfig { get; set; }
}