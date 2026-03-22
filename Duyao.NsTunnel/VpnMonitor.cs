using System.Diagnostics;
using System.Text.Json;

namespace Duyao.NsTunnel;

public class VpnMonitor
{
    private readonly AppConfig _config;
    private readonly HttpClient _httpClient;
    private Process? _vpnProcess;
    private Timer? _healthCheckTimer;
    private Timer? _connectionCheckTimer;
    private bool _health = false;
    private bool _isConnected = false;

    public VpnMonitor(AppConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
    }

    public Task Start()
    {
        AotSimpleLogger.Info("Tunnel monitor started");
        //await ConnectVpn();
        StartTimers();
        return Task.CompletedTask;
    }

    public static Task<bool> IsTcpPortOpen(string host, string port)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    //ping -I $INTERFACE -c 3 -W 5 $TARGET_IP
                    FileName = "nc",
                    Arguments = $"-z -w 2 {host} {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit();
            //var output = process.StandardError.ReadToEndAsync().Result;
            //Console.WriteLine(output);
            return Task.FromResult(process.ExitCode == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    public static Task<string?> GetNetns()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    //ping -I $INTERFACE -c 3 -W 5 $TARGET_IP
                    FileName = "ip",
                    Arguments = "netns identify",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync().Result?.Trim();
            process.WaitForExit();
            AotSimpleLogger.Debug($"netns name=[{output}]");
            return Task.FromResult(output);
        }
        catch
        {
            return Task.FromResult(default(string?));
        }
    }

    private async Task ConnectVpn()
    {
        try
        {
            if (!_isConnected && _health)
            {
                AotSimpleLogger.Info("Starting connecting tunnel");

                // 第1步：获取host和port
                var response = await _httpClient.GetAsync($"https://nst-api.1api.pp.ua/e/{_config.ApiData}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions.Default);
                var decrypted = CryptoHelper.Decrypt(data.GetProperty("data").GetString());
                if (decrypted == null)
                {
                    throw new Exception("Data is invalid");
                }

                var host = decrypted.Split(',')[0];
                var port = decrypted.Split(',')[1];
                var checkConnect = await IsTcpPortOpen(host, port);
                if (!checkConnect)
                {
                    AotSimpleLogger.Debug("Try another connection");
                    await ConnectVpn();
                }
                else
                {
                    // 第2步：执行sstpc命令
                    await ExecuteSstpc(host, int.Parse(port));
                    _isConnected = true;
                }
            }
        }
        catch (Exception ex)
        {
            AotSimpleLogger.Error($"Error connecting Tunnel: {ex.Message}");
            _isConnected = false;
        }
    }

    private async Task ExecuteSstpc(string host, int port)
    {
        AotSimpleLogger.Debug("Tunnel method entry");
        // 停止已有的进程
        try
        {
            _vpnProcess?.Kill();
            _vpnProcess?.Dispose();
            AotSimpleLogger.Debug("Tunnel process killed");
        }
        catch(Exception exp)
        {
            AotSimpleLogger.Debug($"Tunnel process killing error {exp.Message}");
        }

        var conditionParam1 = "";
        var conditionParam2 = "nodefaultroute";
        if (!string.IsNullOrWhiteSpace(_config.Netns))
        {
            conditionParam1 = "--save-server-route";
            conditionParam2 = "defaultroute replacedefaultroute";
        }
        var args = $"--log-level 1 {conditionParam1} --cert-warn --user {_config.VpnUser} --password {_config.VpnPassword} " +
                   $"{host}:{port} require-mschap-v2 {conditionParam2} noauth unit {_config.UnitConfig}";

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
        AotSimpleLogger.Info("Tunnel thread started");
        // await _vpnProcess.WaitForExitAsync();
        // if (_vpnProcess.ExitCode != 0)
        // {
        //     AotSimpleLogger.Debug($"vpnProcess exit {_vpnProcess.ExitCode}");
        //     await ConnectVpn();
        // }
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
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)); // 10秒超时

            await process.WaitForExitAsync(cts.Token);
            if (process.ExitCode != 0)
                throw new Exception($"failed with exit code {process.ExitCode}");

            var output = await process.StandardOutput.ReadToEndAsync();
            return output;
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new Exception("timed out");
        }
    }
    
    public async Task EnsureEnv()
    {
        AotSimpleLogger.Info("Init application");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                //ping -I $INTERFACE -c 3 -W 5 $TARGET_IP
                FileName = "apt",
                Arguments = "install --no-install-recommends -y ncat sstp-client",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var msg = $"System env init failed with exit code {process.ExitCode}";
            AotSimpleLogger.Error(msg);
            Environment.Exit(process.ExitCode);
        }
    }

    private async Task CheckConnection()
    {
        try
        {
            AotSimpleLogger.Debug($"【{_vpnProcess?.Id}】Connection check");
            var result = await GetWithInterface("9.9.9.11"
                , $"ppp{_config.UnitConfig}");
            //Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            if (_health)
            {
                AotSimpleLogger.Error($"Connection check error: {ex.Message}, reconnecting...");
                _isConnected = false;
                await ConnectVpn();
            }
            else
            {
                _isConnected = false;
            }
        }
    }

    private async Task CheckHealth()
    {
        var ownEnv = Environment.GetEnvironmentVariable("DY_NST_LICENSE");
        if (ownEnv?.Equals("long1234")??false)
        {
            return;
        }
        AotSimpleLogger.Debug("License check");
        try
        {
            // 检查返回的json是否符合预期
            bool isHealthy = await DoCheck();

            if (!isHealthy)
            {
                if (_health)
                {
                    AotSimpleLogger.Warning("License check failed, stopping Tunnel...");
                    _health = false;
                    StopVpn();
                }
            }
            else if (!_isConnected)
            {
                _health = true;
                AotSimpleLogger.Info("License check passed, reconnecting Tunnel...");
                await ConnectVpn();
            }
        }
        catch (Exception ex)
        {
            AotSimpleLogger.Error($"License check error: {ex.Message}");
            _health = false;
            StopVpn();
        }
    }

    public async Task<bool> DoCheck()
    {
        var hUrl = $"https://nshealth.1api.pp.ua/{_config.LicenseCheckTicket}";
        var req = new HttpRequestMessage(HttpMethod.Head, hUrl);
        var response = await _httpClient.SendAsync(req);
        var responseCode = (int)response.StatusCode;

        // 检查返回的json是否符合预期
        bool isHealthy = responseCode == 202;
        return isHealthy;
    }

    private void StopVpn()
    {
        try
        {
            if (_isConnected)
            {
                _vpnProcess?.Kill();
                _vpnProcess?.Dispose();
                AotSimpleLogger.Info("Tunnel thread stopped");
            }
        }
        catch (Exception ex)
        {
            AotSimpleLogger.Error($"Error stopping tunnel: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
        }
    }
}