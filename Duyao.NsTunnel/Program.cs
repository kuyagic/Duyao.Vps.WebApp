// Program.cs

using System.Text;
using Duyao.NsTunnel;

string ulid;
var lastByteCount = 30;//DYC+Ulid = 3+26+pad
var exePath = Environment.ProcessPath;
await using (var fs = new FileStream(exePath!, FileMode.Open, FileAccess.Read))
{
    fs.Seek(lastByteCount*-1, SeekOrigin.End);
    var buffer = new byte[lastByteCount];
    fs.Read(buffer, 0, lastByteCount);
    ulid = Encoding.UTF8.GetString(buffer).Trim();
}

if (!ulid.StartsWith("DYC"))
{
    AotSimpleLogger.Error("Application env Invalid");
    AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(5);
}

// 🔧 修复 CS0136：避免与入口 args 冲突 → 改为 cmdArgs
var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

var cmdArgResult = CommandLineParser.ParseCommandLineArgs(cmdArgs);
var logLv = int.Parse(cmdArgResult["logLevel"].ToString() ?? "1");
var location = Convert.ToString(cmdArgResult["license"]);

AotSimpleLogger.SetLogLevel(logLv);
if (string.IsNullOrEmpty(location))
{
    AotSimpleLogger.Error("Application param Invalid");
    AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(5);
}
else
{
    try
    {
        CryptoHelper.Decrypt(location);
    }
    catch
    {
        AotSimpleLogger.Error("Application param Invalid");
        AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
        Environment.Exit(5);
    }
}
var config = new AppConfig
{
    ApiData = location,
    LicenseCheckTicket = ulid[3..],
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
        AotSimpleLogger.Error("Application ID invalid");
        AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
        Environment.Exit(4);
    }
}
catch
{
    AotSimpleLogger.Error("Application startup check error");
    AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(4);
}
await monitor.Start();

// 保持程序运行
await Task.Delay(Timeout.Infinite);