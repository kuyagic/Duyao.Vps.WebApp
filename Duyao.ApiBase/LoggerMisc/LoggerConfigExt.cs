using Serilog;
using Serilog.Configuration;

namespace Duyao.ApiBase.LoggerMisc;

public static class LoggerConfigExt
{
    /// <summary>
    /// 添加 ANSI 着色的控制台 Sink（支持自定义输出模板）
    /// </summary>
    /// <param name="sinkConfig">Sink 配置</param>
    /// <param name="outputTemplate">输出模板</param>
    /// <param name="formatProvider">格式化提供程序</param>
    public static LoggerConfiguration AnsiColoredConsole(
        this LoggerSinkConfiguration sinkConfig,
        string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        IFormatProvider? formatProvider = null)
    {
        return sinkConfig.Sink(new AnsiColoredConsoleSink(outputTemplate, formatProvider));
    }

    /// <summary>
    /// 添加简化 ActionName 的 Enricher
    /// </summary>
    public static LoggerConfiguration WithSimplifiedActionName(
        this LoggerEnrichmentConfiguration enrichmentConfig)
    {
        return enrichmentConfig.With<SimplifiedActionNameEnricher>();
    }
}