// Program.cs

using System.Text;
using Duyao.NsTunnel;

string ulid;
var exePath = Environment.ProcessPath;
await using (var fs = new FileStream(exePath!, FileMode.Open, FileAccess.Read))
{
    fs.Seek(-26, SeekOrigin.End);
    var buffer = new byte[26];
    fs.Read(buffer, 0, 26);
    ulid = Encoding.UTF8.GetString(buffer).Trim();
}

if (!ulid.StartsWith("DYC"))
{
    Console.WriteLine("Application Env Invalid");
    Console.WriteLine("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(5);
}

if (args.Length != 1)
{
    Console.WriteLine("Application Param Invalid");
    Console.WriteLine("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(5);
}
else
{
    try
    {
        CryptoHelper.Decrypt(args[0]);
    }
    catch
    {
        Console.WriteLine("Application Param Invalid");
        Console.WriteLine("Contact https://t.me/ForPrivateChatBot");
        Environment.Exit(5);
    }
}
var config = new AppConfig
{
    ApiData = args[0],
    HealthCheckUrl = ulid[3..],
    VpnUser = "vpn",
    VpnPassword = "vpn",
    UnitConfig = "555"
};

var monitor = new VpnMonitor(config);
try
{
    await monitor.EnsureEnv();
    var check = await monitor.DoCheck();
    if (!check)
    {
        Console.WriteLine("Application ID Invalid");
        Console.WriteLine("Contact https://t.me/ForPrivateChatBot");
        Environment.Exit(4);
    }
}
catch
{
    Console.WriteLine("Application Startup Check Error");
    Console.WriteLine("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(4);
}
await monitor.Start();

// 保持程序运行
await Task.Delay(Timeout.Infinite);