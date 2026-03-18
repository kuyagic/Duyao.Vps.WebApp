using System.Diagnostics;
using System.Text.Json;

namespace Duyao.NsTunnel;

public class VpnMonitor
{
    
    private readonly AppConfig _config;
    private readonly HttpClient _httpClient;
    private Process _vpnProcess;
    private Timer _healthCheckTimer;
    private Timer _connectionCheckTimer;
    private bool _isConnected = false;

    public VpnMonitor(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
    }

    public async Task Start()
    {
        Console.WriteLine("VPN Monitor started");
        await ConnectVpn();
        StartTimers();
    }

    private async Task ConnectVpn()
    {
        try
        {
            // 第1步：获取host和port
            // var response = await _httpClient.GetAsync(_config.ApiUrl);
            // response.EnsureSuccessStatusCode();
            // var content = await response.Content.ReadAsStringAsync();
            // var data = JsonSerializer.Deserialize<JsonElement>(content);
            //
            // var host = data.GetProperty("host").GetString();
            // var port = data.GetProperty("port").GetInt32();
            //
            // Console.WriteLine($"Got host: {host}, port: {port}");

            // 第2步：执行sstpc命令
            await ExecuteSstpc("190.247.87.58", 1312);
            _isConnected = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting VPN: {ex.Message}");
            _isConnected = false;
        }
    }

    private async Task ExecuteSstpc(string host, int port)
    {
        // 停止已有的进程
        _vpnProcess?.Kill();
        _vpnProcess?.Dispose();

        var args = $"--log-level 1 --cert-warn --user {_config.VpnUser} --password {_config.VpnPassword} " +
                   $"{host}:{port} require-mschap-v2 nodefaultroute noauth unit {_config.UnitConfig}";

        _vpnProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sstpc",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        _vpnProcess.Start();
        Console.WriteLine("SSTPC process started");
    }

    private void StartTimers()
    {
        // 第3步：每1分钟检查连接
        _connectionCheckTimer = new Timer(async _ => await CheckConnection(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        // 第4步：每1分钟检查健康状态
        _healthCheckTimer = new Timer(async _ => await CheckHealth(), null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
    }

    private async Task CheckConnection()
    {
        try
        {
            using var client = new HttpClientViaInterface($"ppp{_config.UnitConfig}");
            // 使用指定网络接口访问地址
            var result = await client.GetAsync("http://8.8.8.8");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection check error: {ex.Message}, reconnecting...");
            await ConnectVpn();
        }
    }

    private async Task CheckHealth()
    {
        try
        {
            var response = await _httpClient.GetAsync(_config.HealthCheckUrl);
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(content);

            // 检查返回的json是否符合预期
            bool isHealthy = data.GetProperty("status").GetString() == "ok";

            if (!isHealthy)
            {
                Console.WriteLine("Health check failed, stopping VPN...");
                //StopVpn();
                //_isConnected = false;
            }
            else if (!_isConnected)
            {
                Console.WriteLine("Health check passed, reconnecting VPN...");
                //await ConnectVpn();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Health check error: {ex.Message}");
            //StopVpn();
            _isConnected = false;
        }
    }

    private void StopVpn()
    {
        try
        {
            _vpnProcess?.Kill();
            _vpnProcess?.Dispose();
            Console.WriteLine("SSTPC process stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping VPN: {ex.Message}");
        }
    }
}