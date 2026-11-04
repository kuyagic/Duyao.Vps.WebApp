using Newtonsoft.Json;

namespace Duyao.HlsList.Entity;

/// <summary>
/// /api/add 请求体，字段与 record.json 对齐
/// </summary>
public class HlsInfoUpsert
{
    [JsonProperty("id")]
    public long? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("desc")]
    public string? Desc { get; set; }

    [JsonProperty("img")]
    public string? Img { get; set; }

    [JsonProperty("key_data")]
    public string? KeyData { get; set; }

    [JsonProperty("entry_host")]
    public string? EntryHost { get; set; }

    [JsonProperty("entry_path")]
    public string? EntryPath { get; set; }

    [JsonProperty("is_valid")]
    public int? IsValid { get; set; } = 1;
}
