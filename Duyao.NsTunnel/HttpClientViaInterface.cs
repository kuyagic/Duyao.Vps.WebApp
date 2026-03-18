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
        var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        var targetInterface = interfaces.FirstOrDefault(i => i.Name == interfaceName);
        
        if (targetInterface == null)
            throw new Exception($"Network interface '{interfaceName}' not found");
        var ipProps = targetInterface.GetIPProperties();
        var ipv4 = ipProps.UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        
        if (ipv4 == null)
            throw new Exception($"No IPv4 address found on interface '{interfaceName}'");
        return ipv4.Address;
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