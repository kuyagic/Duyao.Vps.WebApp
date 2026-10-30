using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Duyao.ApiBase.LoggerMisc;

/// <summary>
/// ANSI 着色控制台 Sink - 支持自定义输出模板
/// </summary>
public class AnsiColoredConsoleSink : ILogEventSink
{
    #region ANSI 颜色常量定义

    private const string ColorReset = "\u001b[0m";
    private const string ColorRed = "\u001b[31m";
    private const string ColorYellow = "\u001b[33m";
    private const string ColorGreen = "\u001b[32m";
    private const string ColorCyan = "\u001b[36m";
    private const string ColorGray = "\u001b[90m";
    private const string ColorRedBold = "\u001b[1;31m";
    private const string ColorBlueBold = "\u001b[1;34m";

    #endregion

    private readonly ITextFormatter _textFormatter;
    private readonly object _syncRoot = new object();

    /// <summary>
    /// 初始化 AnsiColoredConsoleSink
    /// </summary>
    /// <param name="outputTemplate">输出模板，支持 {Timestamp}, {Level}, {Message} 等</param>
    /// <param name="formatProvider">自定义格式化提供程序（可选）</param>
    public AnsiColoredConsoleSink(
        string outputTemplate,
        IFormatProvider? formatProvider = null)
    {
        var selectedProvider = formatProvider ?? CultureInfo.CurrentCulture;

        // 使用 Serilog 内置的模板格式化器
        _textFormatter = new MessageTemplateTextFormatter(outputTemplate, selectedProvider);
    }

    /// <summary>
    /// 发出日志事件到控制台（带颜色）
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            try
            {
                // 使用格式化器渲染日志消息
                var sb = new System.Text.StringBuilder();
                using (var writer = new System.IO.StringWriter(sb))
                {
                    _textFormatter.Format(logEvent, writer);
                }

                var formattedMessage = sb.ToString().TrimEnd('\r', '\n');

                // 获取对应级别的颜色
                var colorCode = GetAnsiColorCode(logEvent.Level);

                // ✅ 输出着色日志（整行着色）
                Console.WriteLine($"{colorCode}{formattedMessage}{ColorReset}");

                // 如果有异常，同色输出异常堆栈
                if (logEvent.Exception != null)
                {
                    Console.WriteLine($"{colorCode}{logEvent.Exception}{ColorReset}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in AnsiColoredConsoleSink: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 根据日志级别获取 ANSI 颜色代码
    /// </summary>
    private static string GetAnsiColorCode(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal => ColorRedBold,      // 致命错误 - 红色加粗
        LogEventLevel.Error => ColorRed,          // 错误 - 红色
        LogEventLevel.Warning => ColorYellow,     // 警告 - 黄色
        LogEventLevel.Information => ColorGreen,  // 信息 - 绿色
        LogEventLevel.Debug => ColorCyan,         // 调试 - 青色
        LogEventLevel.Verbose => ColorGray,       // 详细 - 灰色
        _ => ColorReset
    };
}