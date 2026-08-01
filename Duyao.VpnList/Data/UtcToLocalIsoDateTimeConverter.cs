using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Duyao.VpnList.Data;

/// <summary>
/// 把 EF/手填的 UTC DateTime 在序列化时转成服务器本地时区，并带上时区偏移。
/// 数据库里始终存 UTC，本控制器只负责"返回时区友好"这件事。
/// </summary>
public class UtcToLocalIsoDateTimeConverter : DateTimeConverterBase
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffzzz";

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value is DateTime dt) return dt;
        return reader.Value;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is DateTime dt)
        {
            // 数据库里是 UTC，但 EF 读出来常是 Kind=Unspecified，统一按 UTC 处理
            var utc = dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            };
            var local = utc.ToLocalTime();
            writer.WriteValue(local.ToString(Format, CultureInfo.InvariantCulture));
            return;
        }
        writer.WriteValue(value?.ToString());
    }
}
