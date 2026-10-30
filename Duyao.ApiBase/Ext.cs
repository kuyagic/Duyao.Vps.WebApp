using System.Text;
using Duyao.ApiBase.LoggerMisc;
using Newtonsoft.Json;
using Serilog;

namespace Duyao.ApiBase;

public static class ApiBaseExt
{
    public static string? ToJsonString(this object? obj)
    {
        if (obj == null)
            return null;
        return JsonConvert.SerializeObject(obj);
    }

    /// <summary>
    /// 初始化 Log 模块
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="logFilePath"></param>
    public static void InitSeriLog(this WebApplicationBuilder builder, string logFilePath)
    {
        // var logFilePath = Path.Join(basePath, "logs", "sc.extapi-.txt");
        //var logFilePath = Path.Join(basePath, "logs", $"{assembly.GetName().Name?.ToLower() ?? "app"}-.txt");
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (logDirectory == null)
        {
            Log.Error($"日志文件夹NULL，filePath=<{logFilePath}>");
            throw new ArgumentException();
        }

        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            Log.Information($"Created log directory: {logDirectory}");
        }

        //[{HttpMethod}]
        var templateBuilder = new StringBuilder();
        templateBuilder.Append("[{XRequestId}]");
        templateBuilder.Append("[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");
        templateBuilder.Append("[{Level:u4}]");
        // templateBuilder.Append("[{ActionName}] ");
        templateBuilder.Append("{Message:l}{NewLine}{Exception}");

        var logCfg = new LoggerConfiguration()
                // .Console(
                //     theme: AnsiConsoleTheme.Sixteen
                //     , outputTemplate: outputTemplate
                // )
                .WriteTo.AnsiColoredConsole(outputTemplate: templateBuilder.ToString())
                .WriteTo.File(
                    logFilePath
                    , outputTemplate: templateBuilder.ToString()
                    , rollingInterval: RollingInterval.Day
                )
                .Enrich.FromLogContext()
                .Enrich.WithSimplifiedActionName()
                .ReadFrom.Configuration(builder.Configuration)
            ;

        Log.Logger = logCfg
            // .Enrich.WithCaller(true,2)
            .CreateLogger();
    }
}