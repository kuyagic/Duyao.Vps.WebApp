using Newtonsoft.Json;

namespace Duyao.VpnList.Entity;

public class VpnInfoUpsert
{
    [JsonProperty("id")]
    public long? Id { get; set; }

    [JsonProperty("ip")]
    public string? Ip { get; set; }

    [JsonProperty("port")]
    public string? Port { get; set; }

    [JsonProperty("host_name")]
    public string? HostName { get; set; }

    [JsonProperty("country")]
    public string? Country { get; set; }

    [JsonProperty("isp_name")]
    public string? IspName { get; set; }

    [JsonProperty("ext_1")]
    public string? Ext1 { get; set; }

    [JsonProperty("ext_2")]
    public string? Ext2 { get; set; }

    [JsonProperty("is_valid")]
    public int? IsValid { get; set; } = 1;
}
