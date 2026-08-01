using Newtonsoft.Json;

namespace Duyao.VpnList.Entity;

public class VpnInfoDeleteRequest
{
    /// <summary>
    /// 通过 id 删除（支持多个）
    /// </summary>
    [JsonProperty("ids")]
    public List<long>? Ids { get; set; }

    /// <summary>
    /// 通过 ip + port 删除（支持多个）
    /// </summary>
    [JsonProperty("items")]
    public List<VpnInfoKey>? Items { get; set; }
}

public class VpnInfoKey
{
    [JsonProperty("ip")]
    public string? Ip { get; set; }

    [JsonProperty("port")]
    public string? Port { get; set; }
}
