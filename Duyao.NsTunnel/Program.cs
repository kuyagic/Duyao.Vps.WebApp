// Program.cs

using System.Text;
using Duyao.NsTunnel;
//printf "%-30s" "DYC01KM1ZN694J4E53PGB6MMPOAFV" >> file
//truncate -s -30 file

var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

var cmdArgResult = CommandLineParser.ParseCommandLineArgs(cmdArgs);
var logLv = int.Parse(cmdArgResult["logLevel"]?.ToString() ?? "1");
var location = Convert.ToString(cmdArgResult["license"]);
var netns = Convert.ToString(cmdArgResult["netns"]);
var godString = Convert.ToString(cmdArgResult["godstring"]);

var isGod = await VpnMonitor.CheckGodMode(godString ?? "");

string ulid;
var lastByteCount = 30; //DYC+Ulid = 3+26+pad
var exePath = Environment.ProcessPath;
await using (var fs = new FileStream(exePath!, FileMode.Open, FileAccess.Read))
{
    fs.Seek(lastByteCount * -1, SeekOrigin.End);
    var buffer = new byte[lastByteCount];
    fs.ReadExactly(buffer, 0, lastByteCount);
    ulid = Encoding.UTF8.GetString(buffer).Trim();
}

if (!ulid.StartsWith("DYC") && !isGod)
{
    AotSimpleLogger.Error("Application env invalid");
    AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(5);
}


if (!string.IsNullOrEmpty(netns))
{
    var currentNs = await VpnMonitor.GetNetns() ?? "";
    if (currentNs != netns)
    {
        AotSimpleLogger.Error("Application ns not equal");
        AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
        Environment.Exit(5);
    }
}

AotSimpleLogger.SetLogLevel(logLv);
if (string.IsNullOrEmpty(location))
{
    AotSimpleLogger.Error("Application param invalid");
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
    IsGod = isGod,
    Netns = netns,
    ApiData = location,
    LicenseCheckTicket = ulid[3..],
    VpnUser = "vpn",
    VpnPassword = "vpn",
    UnitConfig = "555"
};

// .NET 6+ 内置方法，专门为此设计
using var cts = new CancellationTokenSource();
// 一行搞定监听
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

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
    await monitor.Start(cts.Token);
}
catch (OperationCanceledException)
{
    AotSimpleLogger.Info("Exiting");
    monitor.Exiting();
}
catch
{
    AotSimpleLogger.Error("Application startup check error");
    AotSimpleLogger.Warning("Contact https://t.me/ForPrivateChatBot");
    Environment.Exit(4);
}


// 保持程序运行
await Task.Delay(Timeout.Infinite);