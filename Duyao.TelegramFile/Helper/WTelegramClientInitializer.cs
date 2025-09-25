using Duyao.TelegramFile.Entity;
using TL;
using WTelegram;

namespace Duyao.TelegramFile.Helper;

public class WTelegramClientInitializer
{
    private readonly ILogger<WTelegram.Client> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _provider;

    public WTelegramClientInitializer(ILogger<WTelegram.Client> logger
        , IConfiguration opt
        , IServiceProvider provider
    )
    {
        _provider = provider;
        _logger = logger;
        _configuration = opt;
        _logger.LogInformation("Init Singleton TelegramClient");
    }

    public Client Initialize()
    {
        var tgConfig = _configuration.GetSection("Telegram").Get<WTelegramClientConfig>();
        var apiId = Utils.SelectConfigValue(tgConfig?.ApiId ?? 0, "TG_BOT_API_ID", 0);
        var apiHash = Utils.SelectConfigValue(tgConfig?.ApiHash, "TG_BOT_API_HASH", "");
        var botToken = Utils.SelectConfigValue(tgConfig?.BotToken, "TG_BOT_API_BOT_TOKEN", "");
        var mtProxyUrl = Utils.SelectConfigValue(tgConfig?.MtProxyUrl, "TG_MT_PROXY_URL", "");
        var botResponse = Utils.SelectConfigValue(tgConfig?.BotResponse, "TG_BOT_RESPONSE", 1);
        var apiHostName = Utils.SelectConfigValue(tgConfig?.HostName, "TG_BOT_API_HOSTNAME", "");
        var allowUser = Utils.SelectConfigValue(tgConfig?.AllowUserId, "TG_BOT_ALLOW_USER_ID"
            , [0L]
            , s => s.Split(',').Select(long.Parse).ToArray()
        );
        if (string.IsNullOrEmpty(apiHash) || string.IsNullOrEmpty(botToken) || apiId == 0)
        {
            throw new InvalidOperationException("环境变量或者配置文件没找到正确配置");
        }

        var client = new Client(apiId, apiHash);
        if (!string.IsNullOrEmpty(mtProxyUrl))
        {
            client.MTProxyUrl = mtProxyUrl;
        }
        var user = client.LoginBotIfNeeded(botToken);
        _logger.LogInformation($"ApiHostName - {apiHostName}");
        _logger.LogInformation($"BotInfo - {user.Result.first_name},{user.Result.last_name},@{user.Result.username}");
        var allowUserStr = string.Join(",", allowUser?.Select(a => a.ToString()).ToArray() ?? []);
        _logger.LogInformation($"allowUserStr - {allowUserStr}");
        _logger.LogInformation($"botResponse - {botResponse}");
        
        async void KeepAliveAsync()
        {
            while (true)
            {
                var dtTick = DateTime.Now.Ticks;
                await Task.Delay(TimeSpan.FromMinutes(5)); // 每5分钟检查一次
                _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},KeepAliveAsync Done");
                try
                {
                    await client.Ping(dtTick); // 假设的 ping 方法
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"连接异常: {ex.Message}");
                    await client.LoginBotIfNeeded(botToken);
                }
            }
        }
        
        async Task OnUpdate(Update upd)
        {
            _logger.LogInformation($"OnUpdate triggered,{upd.GetType().FullName}");
            if (upd is UpdateNewMessage msg)
            {
                try
                {
                    if (msg.message.Peer is PeerUser pu
                        && msg.message is Message m
                        && m.media != null
                       )
                    {
                        var fileName = string.Empty;
                        // var fileSize = -1L;
                        // var fileId = 0L;
                        // var mimeType = string.Empty;
                        var msgType = string.Empty;
                        if (m.media is MessageMediaDocument md
                            && md.document is Document doc)
                        {
                            msgType = "document";
                            fileName = doc.Filename;
                            // fileSize = doc.size;
                            // fileId = doc.ID;
                            // mimeType = doc.mime_type;
                        }

                        else if (m.media is MessageMediaPhoto photo)
                        {
                            msgType = "photo";
                            // mimeType = "image/png";
                            // if (photo.photo is Photo p)
                            // {
                            //     fileSize = p.sizes.Last().FileSize;
                            //     fileId = p.ID;
                            // }
                        }
                        else
                        {
                            return;
                        }

                        //template = {user_id},{msg_id}|filename|hash
                        var encTmpl = Utils.HashTelegramFileInfo(pu.user_id, m.ID);
                        var replyText = $"{apiHostName}/{encTmpl}";

                        _logger.LogInformation($"receive {msgType} msg from chat(user) {pu.user_id}");
                        _logger.LogInformation($"Message Id {m.ID},text {m.message}");
                        _logger.LogInformation($"URL {replyText}");
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            _logger.LogInformation($"Message Media FileName {fileName}");
                            replyText = $"{fileName}\r\n{replyText}";
                        }

                        if (botResponse == 0)
                        {
                            return;
                        }

                        try
                        {
                            if (allowUser != null
                                && (allowUser.Contains(pu.user_id) || allowUser.Contains(-1)
                                )
                               )
                            {
                                await client.SendMessageAsync(new InputPeerUser(pu.user_id, 0), replyText,
                                    reply_to_msg_id: m.ID);
                            }
                            else
                            {
                                _logger.LogInformation($"User {pu.user_id} not allowed to use");
                                await client.SendMessageAsync(new InputPeerUser(pu.user_id, 0),
                                    "You not allowed to use this bot", reply_to_msg_id: m.ID);
                            }
                        }
                        catch (Exception exp)
                        {
                            _logger.LogError(exp.Message);
                        }
                    }

                    if (msg.message.Peer is PeerUser pu2
                        && msg.message is Message m2
                        && m2.message.ToLower().StartsWith("/start", StringComparison.InvariantCultureIgnoreCase)
                       )
                    {
                        #region Startup Message

                        if (botResponse == 0)
                        {
                            return;
                        }

                        try
                        {
                            if (allowUser != null
                                && (allowUser.Contains(pu2.user_id) || allowUser.Contains(-1)
                                )
                               )
                            {
                                await client.SendMessageAsync(new InputPeerUser(pu2.user_id, 0),
                                    "Send me document or photo message to get web link"
                                    , reply_to_msg_id: m2.ID);
                            }
                            else
                            {
                                _logger.LogInformation($"User {pu2.user_id} not allowed to use");
                                await client.SendMessageAsync(new InputPeerUser(pu2.user_id, 0),
                                    "You not allowed to use this bot", reply_to_msg_id: m2.ID);
                            }
                        }
                        catch (Exception exp)
                        {
                            _logger.LogError(exp.Message);
                        }

                        #endregion
                    }
                } //try
                catch (Exception exp)
                {
                    _logger.LogError(exp.Message);
                }
            }
        }

        var mgnr = client.WithUpdateManager(OnUpdate /*, "Updates.state"*/);
        KeepAliveAsync();
        return client;
    }
    
}