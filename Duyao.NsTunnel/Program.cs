// Program.cs

using System.Diagnostics;
using System.Text.Json;
using System.Net;
using Duyao.NsTunnel;

var config = new AppConfig
{
    ApiUrl = "https://your-api.com/endpoint",
    HealthCheckUrl = "https://your-health-check.com/status",
    VpnUser = "vpn",
    VpnPassword = "vpn",
    UnitConfig = "555"
};

var monitor = new VpnMonitor(config);
await monitor.Start();

// 保持程序运行
await Task.Delay(Timeout.Infinite);