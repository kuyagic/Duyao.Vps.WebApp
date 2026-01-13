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

    private static string GetConfigFileFullPath(string[] args)
    {
        var cmdParam = CommandLineParser.ParseCommandLineArgs(args);
        string resultConfigFile;
        var envConfigFile = Environment
            .GetEnvironmentVariable("DY_TELEGRAM_BOT_CONFIG");
        if (!File.Exists(envConfigFile))
        {
            var cmdConfigFile = Path.GetFullPath(cmdParam["config-file"]);
            if (!File.Exists(cmdParam["config-file"]))
            {
                var configPath = GetUserConfigFolderPath();
                var userConfig = Path.Combine(configPath, "DY.Telegram.json");
                resultConfigFile = !File.Exists(userConfig) ? "config.json" : userConfig;
            }
            else
            {
                resultConfigFile = cmdConfigFile;
            }
        }
        else
        {
            resultConfigFile = envConfigFile;
        }

        Console.WriteLine($"Use Config File {Path.GetFullPath(resultConfigFile)}");
        return Path.GetFullPath(resultConfigFile);
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
            var host = Host.CreateDefaultBuilder(args)
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
                    config.AddJsonFile(GetConfigFileFullPath(args), optional: false, reloadOnChange: true);
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddTransient<RavenDbService>();
                    services.AddTransient<TelegramMessageService>();
                    services.AddHostedService<TelegramBotService>();
                }).Build();

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