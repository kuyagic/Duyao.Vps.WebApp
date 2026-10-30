

using Newtonsoft.Json;

namespace Duyao.ApiBase;

public class ApiResponse
{
    [JsonProperty("ok")] public bool Ok { get; set; } = true;
    [JsonProperty("message")] public string? Message { get; set; }
}

public class ApiResponseWithData<T>
{
    [JsonProperty("ok")] public bool Ok { get; set; } = true;
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonProperty("data")] public T Data { get; set; }
}