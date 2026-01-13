using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Embedded;

namespace DY.Telegram;

public class RavenDbService
{
    private static IDocumentStore? _store;
    private readonly ILogger<RavenDbService> _logger;
    private readonly string _dbDataFullPath;

    public RavenDbService(ILogger<RavenDbService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbDataFullPath = configuration["RavenDb:DbDirectory"] ?? "RavenStore";
        var full = System.IO.Path.GetFullPath(_dbDataFullPath);
        _logger.LogInformation($"DatabaseDir {full}");
    }

    public async Task<IDocumentStore> GetStoreAsync()
    {
        var opt = new ServerOptions
        {
            DataDirectory = Path.Combine(_dbDataFullPath, "db"),
            LogsPath = Path.Combine(_dbDataFullPath, "log"),
            Licensing = new ServerOptions.LicensingOptions
            {
                EulaAccepted = true,
                DisableAutoUpdate = true
            }
        };
        if (_store != null) return _store;

        // ★ 关键：启动嵌入式服务器
        var server = EmbeddedServer.Instance;
        server.StartServer(opt);
        _store = await server.GetDocumentStoreAsync("TelegramMessageArchive");
        return _store;
    }
}