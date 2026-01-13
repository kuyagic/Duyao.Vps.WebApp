using System.IO.Enumeration;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using TL;

namespace DY.Telegram;

public static class Program
{
    private static void ProcessMessage(UpdatesBase @base, Message msg)
    {
        // 确保消息不是空消息或服务消息
        // 获取发送消息的对话（Peer）信息
        // @base 继承自 IPeerResolver，允许你调用 UserOrChat 方法来解析 Peer
        var chat = @base.UserOrChat(msg.Peer);
        // 获取发送人（From）信息
        // msg.From 可以为空，尤其是在匿名发帖的情况下
        var sender = @base.UserOrChat(msg.From ?? msg.Peer);
        // 判断对话类型并获取详细信息
        if (chat != null)
        {
            Console.WriteLine($"消息来自对话: {chat}");
            // 如果需要更详细的对话信息，可以根据 chat 的实际类型进行转换
            // 例如：if (chat is ChatGroup groupChat) { /* 获取群组信息 */ }
            // 或者 if (chat is Channel channelChat) { /* 获取频道信息 */ }
        }

        // 判断发送人类型并获取详细信息
        if (sender != null)
        {
            Console.WriteLine($"发送人: {sender}");
            // 如果发送人是用户，可以获取其用户名和姓名等信息
            if (sender is User user)
            {
                Console.WriteLine($"发送人ID: {user.ID}");
                Console.WriteLine($"发送人用户名: {user.username}");
                Console.WriteLine($"发送人全名: {user.first_name} {user.last_name}");
                // 其他用户详细信息可以通过 user 对象获取
            }
            // 如果发送人是频道或群组（例如，匿名管理员消息），则 sender 可能是 Chat 或 Channel 类型
            else if (sender is Chat chatSender)
            {
                Console.WriteLine($"发送人（聊天）ID: {chatSender.ID}");
                Console.WriteLine($"发送人（聊天）标题: {chatSender.Title}");
            }
            else if (sender is Channel channelSender)
            {
                Console.WriteLine($"发送人（频道）ID: {channelSender.ID}");
                Console.WriteLine($"发送人（频道）标题: {channelSender.Title}");
            }
        }
        else
        {
            Console.WriteLine("无法获取发送人信息（可能为匿名消息或未知Peer）");
        }

        Console.WriteLine($"消息内容: {msg.message}");
    }

    private const string ConfigDirectoryName = ".config";
    private static string? GetCurrentDir()
    {
        var processPath = Environment.ProcessPath;
        if (processPath is not null)
        {
            var programDirectory = Path.GetDirectoryName(processPath)!;
            return programDirectory;
        }

        return null;
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging((_, logging) =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true; // ← 关键：强制单行输出
                    options.TimestampFormat = "HH:mm:ss "; // 可选：自定义时间格式
                    options.ColorBehavior = LoggerColorBehavior.Enabled;
                });
            })
            .ConfigureAppConfiguration((_, config) =>
            {
                string configPath = GetUserConfigFolderPath();
                var userConfig = Path.Combine(configPath, "DY.Telegram.json");
                if (!File.Exists(userConfig))
                {
                    userConfig = "config.json";
                }
                Console.WriteLine($"Use Config File {Path.GetFullPath(userConfig)}");
                config.AddJsonFile(userConfig, optional: false, reloadOnChange: true);
            })
            .ConfigureServices((_, services) =>
            {
                services.AddTransient<RavenDbService>();
                services.AddTransient<TelegramMessageService>();
                services.AddHostedService<TelegramBotService>();
            });

    private static string GetUserConfigFolderPath()
    {
        string basePath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (string.IsNullOrEmpty(basePath))
        {
            throw new DirectoryNotFoundException("无法获取当前用户的主目录路径。");
        }
        var fullPath = Path.Combine(basePath, ConfigDirectoryName);
        try
        {
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
        catch
        {
            // ignored
        }

        return fullPath;
    }

    private static void InitUserPath()
    {
        // 获取当前 PATH
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dotnetPath = Path.Combine(home, ".dotnet");
        if (!currentPath.Contains(dotnetPath))
        {
            string newPath = $"{dotnetPath}{Path.PathSeparator}{currentPath}";
            Environment.SetEnvironmentVariable("PATH", newPath);
        }
    }

    public static async Task Main(string[] args)
    {
        try
        {
            InitUserPath();
            var host = CreateHostBuilder(args).Build();

            await host.RunAsync(); // 使用 RunAsync 简化主方法的等待和停止逻辑
        }
        catch (Exception ex)
        {
            // 在主机启动失败时，直接使用 Console.WriteLine 输出错误信息
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("--------------------- Host 启动失败 ---------------------");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
        }
    }
}