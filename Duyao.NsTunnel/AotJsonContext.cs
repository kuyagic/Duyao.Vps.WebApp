using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duyao.NsTunnel;

// 假设您已经有一个名为 AppJsonSerializerContext 的上下文
[JsonSourceGenerationOptions(WriteIndented = false)] // 根据需要配置
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(NstApiResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        TypeInfoResolver = AppJsonSerializerContext.Default,
        WriteIndented = false
    };
}