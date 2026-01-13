using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

// 确保引入 System 命名空间

namespace DY.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly Client _client;
    private readonly TelegramBotOptions _options;
    private readonly TelegramMessageService _telegramMessageService; // 注入消息存储服务

    public TelegramBotService(ILogger<TelegramBotService> logger,
        IConfiguration configuration,
        TelegramMessageService telegramMessageService) // 添加注入
    {
        _logger = logger;
        _telegramMessageService = telegramMessageService; // 初始化服务

        _options = configuration.GetSection("TelegramBot").Get<TelegramBotOptions>()
                   ?? throw new InvalidOperationException("TelegramBot 配置节点缺失或无效。");
        WTelegram.Helpers.Log = (lvl, str) =>
        {
            // _logger.Log((LogLevel)lvl, "WTL: {Message}", str);
        };
        // Client. = (lvl, str) => _logger.Log((LogLevel)lvl, "WTL: {Message}", str);
        // 初始化 WTelegram 客户端
        try
        {
            _client = new Client(_options.ApiId, _options.ApiHash, _options.SessionFile);

            // 注册更新处理逻辑
            _client.OnUpdates += OnUpdatesReceived;
        }
        catch (Exception ex)
        {
            // **防御性编程**：确保在日志系统启动前发生的同步错误能够被看到
            Console.WriteLine($"严重错误：WTelegramClient 实例化失败: {ex.Message}");
            _logger.LogError(ex, "WTelegramClient 实例化失败。");
            throw; // 重新抛出异常，让 Host 启动失败，并确保我们有输出
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramBotService 正在启动...");

        try
        {
            var user = await _client.LoginBotIfNeeded(_options.BotToken);
            _logger.LogInformation("机器人登录成功，用户ID: {UserId}", user.id);
            _logger.LogInformation("Bot Name: {FirstName} (@{Username})", user.first_name, user.username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "机器人登录失败。");
            // **异常处理**：如果登录失败，应该通知 Host 停止
            Environment.ExitCode = 1; // 设置非零退出码
            _logger.LogCritical("TelegramBotService 无法启动，应用程序将退出。");
            return;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("接收到停止信号，TelegramBotService 正在关闭...");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("执行清理操作：关闭 WTelegramClient...");
        await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("TelegramBotService 清理完成。");
    }

    private async Task OnUpdatesReceived(UpdatesBase updateBase)
    {
        foreach (var upd in updateBase.UpdateList)
        {
            switch (upd)
            {
                case UpdateNewMessage newMessage:
                    if (newMessage.message is not Message msg) continue;
                    ProcessMessage(updateBase, msg);
                    await _telegramMessageService.SaveMessageAsync(updateBase, msg);
                    break;
                // 可以添加其他更新类型处理
            }
        }
    }

    // 您原来的 ProcessMessage 逻辑，现在可以利用 _logger 进行日志记录
    private void ProcessMessage(UpdatesBase @base, Message msg)
    {
        var chat = @base.UserOrChat(msg.Peer);
        var sender = @base.UserOrChat(msg.From ?? msg.Peer);

        _logger.LogInformation("----- New Telegram Message Received -----");
        if (chat != null)
        {
            _logger.LogInformation($"消息来自对话: {chat}");
        }

        if (sender != null)
        {
            _logger.LogInformation($"发送人: {sender}");
            if (sender is User user)
            {
                _logger.LogInformation(
                    $"发送人ID: {user.id}, 用户名: @{user.username}, 全名: {user.first_name} {user.last_name}");
            }
            else if (sender is Chat chatSender)
            {
                _logger.LogInformation($"发送人（聊天）ID: {chatSender.ID}, 标题: {chatSender.Title}");
            }
            else if (sender is Channel channelSender)
            {
                _logger.LogInformation($"发送人（频道）ID: {channelSender.ID}, 标题: {channelSender.Title}");
            }
        }
        else
        {
            _logger.LogWarning("无法获取发送人信息（可能为匿名消息或未知Peer）");
        }

        _logger.LogInformation($"消息内容: {msg.message}");
        _logger.LogInformation("-----------------------------------------");
    }
}