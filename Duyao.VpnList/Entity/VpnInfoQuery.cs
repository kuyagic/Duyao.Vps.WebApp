using Newtonsoft.Json;

namespace Duyao.VpnList.Entity;

public class VpnInfoQuery
{
    /// <summary>
    /// ip 模糊包含
    /// </summary>
    [JsonProperty("ip_contains")]
    public string? IpContains { get; set; }

    /// <summary>
    /// host_name 模糊包含
    /// </summary>
    [JsonProperty("host_name_contains")]
    public string? HostNameContains { get; set; }

    /// <summary>
    /// host_name 模糊不包含
    /// </summary>
    [JsonProperty("host_name_not_contains")]
    public string? HostNameNotContains { get; set; }

    /// <summary>
    /// asn_org 模糊包含，多个 (任一关键字命中即匹配)
    /// </summary>
    [JsonProperty("asn_org_contains")]
    public List<string>? AsnOrgContains { get; set; }

    /// <summary>
    /// country 模糊包含，多个模糊包含
    /// </summary>
    [JsonProperty("country_contains")]
    public List<string>? CountryContains { get; set; }

    /// <summary>
    /// country 模糊不包含，多个 (任一关键字命中即排除)
    /// </summary>
    [JsonProperty("country_not_contains")]
    public List<string>? CountryNotContains { get; set; }

    /// <summary>
    /// is_valid 过滤：
    ///  - 默认 1（只显示有效）
    ///  - 0：只显示失效
    ///  - -1：不加 is_valid 条件（查全部）
    /// </summary>
    [JsonProperty("is_valid")]
    public int IsValid { get; set; } = 1;

    [JsonProperty("page")]
    public int Page { get; set; } = 1;

    [JsonProperty("size")]
    public int Size { get; set; } = 50;
}
