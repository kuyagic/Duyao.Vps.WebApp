using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Duyao.NsTunnel;

public class HttpClientViaInterface:IDisposable
{
     private readonly string _interfaceName;
    private readonly HttpClient _httpClient;
    public HttpClientViaInterface(string interfaceName)
    {
        _interfaceName = interfaceName;
        
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync
        };
        
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(8);
    }
    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    
        try
        {
            // 获取指定网络接口的IP地址
            var interfaceIp = GetInterfaceIpAddress(_interfaceName);
        
            // 绑定到指定接口
            socket.Bind(new IPEndPoint(interfaceIp, 0));
        
            // 连接到目标地址
            await socket.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, cancellationToken);
        
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
    private IPAddress GetInterfaceIpAddress(string interfaceName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ip",
                Arguments = $"-4 addr show {interfaceName} | grep -oP '(?<=inet\\s)\\d+(\\.\\d+){{3}}'",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (string.IsNullOrEmpty(output))
            throw new Exception($"Network interface '{interfaceName}' not found or has no IPv4 address");
        if (IPAddress.TryParse(output, out var ipAddress))
            return ipAddress;
        throw new Exception($"Invalid IP address for interface '{interfaceName}': {output}");
    }
    public async Task<string> GetAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}