// Program.cs

using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.Reflection;
using System.Text;
using Duyao.NsTunnel;

var ulid = string.Empty;
var exePath = Assembly.GetExecutingAssembly().Location;
await using (var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read))
{
    fs.Seek(-26, SeekOrigin.End);
    var buffer = new byte[26];
    fs.Read(buffer, 0, 26);
    ulid = Encoding.UTF8.GetString(buffer).Trim();
}

if (ulid.StartsWith("DYC"))
{
    Console.WriteLine("Not Allowed");
    Environment.Exit(4);
}
var config = new AppConfig
{
    ApiUrl = "https://your-api.com/endpoint",
    HealthCheckUrl = ulid[3..],
    VpnUser = "vpn",
    VpnPassword = "vpn",
    UnitConfig = "555"
};

var monitor = new VpnMonitor(config);
await monitor.Start();

// 保持程序运行
await Task.Delay(Timeout.Infinite);