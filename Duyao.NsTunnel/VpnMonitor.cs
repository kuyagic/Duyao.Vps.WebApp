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
    private bool _health = false;
    private bool _isConnected = false;

    public VpnMonitor(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
    }

    public async Task Start()
    {
        Console.WriteLine("Tunnel Monitor started");
        //await ConnectVpn();
        StartTimers();
    }

    private async Task ConnectVpn()
    {
        try
        {
            if (!_isConnected && _health)
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting Tunnel: {ex.Message}");
            _isConnected = false;
        }
    }

    private async Task ExecuteSstpc(string host, int port)
    {
        // 停止已有的进程
        try
        {
            _vpnProcess?.Kill();
            _vpnProcess?.Dispose();
        }
        catch
        {
        }

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
        Console.WriteLine("Tunnel Thread started");
    }

    private void StartTimers()
    {
        // 第3步：每1分钟检查连接
        _connectionCheckTimer =
            new Timer(async _ => await CheckConnection(), null
                , TimeSpan.FromSeconds(2)
                , TimeSpan.FromMinutes(1));

        // 第4步：每1分钟检查健康状态
        _healthCheckTimer = new Timer(async _ => await CheckHealth(), null
            , TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15));
    }

    private async Task<string> GetWithInterface(string url, string interfaceName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                //ping -I $INTERFACE -c 3 -W 5 $TARGET_IP
                FileName = "ping",
                Arguments = $"-I {interfaceName} -c 3 -W 5 {url}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new Exception($"ping failed with exit code {process.ExitCode}");
        return output;
    }

    private async Task CheckConnection()
    {
        try
        {
            var result = await GetWithInterface("9.9.9.11"
                , $"ppp{_config.UnitConfig}");
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
            var hUrl = $"https://nshealth.1api.pp.ua/{_config.HealthCheckUrl}";
            var req = new HttpRequestMessage(HttpMethod.Head, hUrl);
            var response = await _httpClient.SendAsync(req);
            var responseCode = (int)response.StatusCode;

            // 检查返回的json是否符合预期
            bool isHealthy = responseCode == 202;

            if (!isHealthy)
            {
                Console.WriteLine("Health check failed, stopping Tunnel...");
                _health = false;
                StopVpn();
            }
            else if (!_isConnected)
            {
                _health = true;
                Console.WriteLine("Health check passed, reconnecting Tunnel...");
                await ConnectVpn();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Health check error: {ex.Message}");
            _health = false;
            StopVpn();
        }
    }

    private void StopVpn()
    {
        try
        {
            if (_isConnected)
            {
                _vpnProcess?.Kill();
                _vpnProcess?.Dispose();
                Console.WriteLine("Tunnel Thread stopped");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping Tunnel: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
        }
    }
}