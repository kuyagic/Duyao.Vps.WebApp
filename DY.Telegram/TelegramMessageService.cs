using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;

// 引入 WTelegramClient 的 TL 命名空间

namespace DY.Telegram;

public class TelegramMessageService(ILogger<TelegramMessageService> logger, RavenDbService dataStore)
{
    private async Task DoDatabaseSave(IDbEntity entity)
    {
        var db = await dataStore.GetStoreAsync();
        using var session = db.OpenAsyncSession();
        await session.StoreAsync(entity);
        await session.SaveChangesAsync();
    }

    public async Task SaveMessageAsync(UpdatesBase updateBase, Message? message)
    {
        if (message?.Peer == null)
        {
            logger.LogWarning("尝试保存空消息或无 Peer 的消息。");
            return;
        }


        // 解析聊天和发送者信息
        // 使用 updateBase.UserOrChat 可以获取到解析后的 Peer 对象
        var chat = updateBase.UserOrChat(message.Peer);
        var sender = updateBase.UserOrChat(message.From ?? message.Peer);

        long chatId = 0;
        string chatTitle = "Unknown Chat";
        if (chat != null)
        {
            chatId = chat.ID;
            if (chat is Channel channel) chatTitle = channel.title;
            else if (chat is User userChat) chatTitle = $"{userChat.first_name} {userChat.last_name}".Trim();
        }

        long senderId = 0;
        string senderName = "Unknown Sender";
        if (sender != null)
        {
            senderId = sender.ID;
            if (sender is User userSender) senderName = $"{userSender.first_name} {userSender.last_name}".Trim();
            else if (sender is Channel channelSender) senderName = channelSender.title;
        }

        var telegramMessage = new TelegramMessage
        {
            // **数据建模**：Telegram Message ID 是唯一的，可以作为 RavenDB 的业务主键
            Id = $"TelegramMessages/{message.ID}-{message.Peer.ID}", // 复合 ID 确保唯一性
            TelegramMessageId = message.ID,
            ChatId = chatId,
            ChatTitle = chatTitle,
            SenderId = senderId,
            SenderName = senderName,
            Text = message.message ?? string.Empty,
            Date = message.Date,
            IsIncoming = true // 假设所有通过 OnUpdates 收到的都是 incoming 消息
        };
        await DoDatabaseSave(telegramMessage);
        logger.LogInformation($"已将消息 (ID: {telegramMessage.TelegramMessageId} 保存到数据库。");
        await Task.Run(() => Task.CompletedTask);
    }
}