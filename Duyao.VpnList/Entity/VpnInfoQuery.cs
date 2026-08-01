using Newtonsoft.Json;

namespace Duyao.VpnList.Entity;

public class VpnInfoQuery
{
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
    /// isp_name 模糊包含
    /// </summary>
    [JsonProperty("isp_name_contains")]
    public string? IspNameContains { get; set; }

    /// <summary>
    /// country 模糊包含，多个模糊包含
    /// </summary>
    [JsonProperty("country_contains")]
    public List<string>? CountryContains { get; set; }

    /// <summary>
    /// is_valid 默认 1
    /// </summary>
    [JsonProperty("is_valid")]
    public int? IsValid { get; set; } = 1;

    [JsonProperty("page")]
    public int Page { get; set; } = 1;

    [JsonProperty("page_size")]
    public int PageSize { get; set; } = 50;
}
