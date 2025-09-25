// dotnet publish -f net8.0 -r linux-arm64 -p:PublishSingleFile=true -p:PublishSelfContained=false -o u:\publish

using System.Reflection;
using Duyao.ApiBase;
using Duyao.SmsForward;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

try
{
    #region basePath Check

    var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
    if (string.IsNullOrEmpty(basePath))
    {
        basePath = AppContext.BaseDirectory;
    }

    Log.Information($"base-path:{basePath}");
    var options = new WebApplicationOptions
    {
        ContentRootPath = basePath,
        Args = args
    };

    #endregion

    var builder = WebApplication.CreateBuilder(options);


    #region Init SeriLog

    var logFilePath = Path.Join(basePath, "logs", "duyao.netcore.web-.txt");
    var logDirectory = Path.GetDirectoryName(logFilePath);
    if (!Directory.Exists(logDirectory))
    {
        Directory.CreateDirectory(logDirectory);
        Log.Information($"Created log directory: {logDirectory}");
    }

    var reqIdName = TraceRequestMiddleware.REQUEST_ID;
    var outputTemplate = "[{" + reqIdName +
                         "}][{HttpMethod}][{ActionName}][{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:l}{NewLine}{Exception}";
    var logCfg = new LoggerConfiguration().WriteTo
            .Console(
                theme: AnsiConsoleTheme.Sixteen
                , outputTemplate: outputTemplate
            )
            .WriteTo.File(
                logFilePath
                , outputTemplate: outputTemplate
                , rollingInterval: RollingInterval.Day
            )
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(builder.Configuration)
        ;

    Log.Logger = logCfg
        // .Enrich.WithCaller(true,2)
        .CreateLogger();

    #endregion

    // 获取当前程序集
    var assembly = Assembly.GetExecutingAssembly();
    // 获取版本信息
    var version = assembly.GetName().Version;
    var verString = string.Format(BuildInfo.BuildTime, version);
    Log.Information("Starting APP");
    Log.Information($"Version : {verString}");
    if (OperatingSystem.IsWindows())
    {
        Console.Title = $"{Assembly.GetEntryAssembly()?.FullName} - {verString}";
    }

    builder.Logging.ClearProviders().AddSerilog();


    builder.Services.AddControllers()
        //.AddXmlDataContractSerializerFormatters()
        .AddNewtonsoftJson();
    ;
    var config = builder.Configuration.SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();
    builder.Services.Configure<AppConfigObject>(config);

    builder.Services.AddCors();

    builder.Services.AddHttpContextAccessor();

    //builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfig.Value));

    var app = builder.Build();
    app.UseCors(a => a.WithOrigins("*").AllowAnyHeader()
        .AllowAnyMethod()
    );

    app.UseAuthorization();
    app.MapControllers();

    app.UseMiddleware<TraceRequestMiddleware>();
    app.UseRouting();

    app.Run();
}
catch (Exception exp)
{
    Log.Fatal(exp.Message);
}
finally
{
    Log.Information("Closing App");
    Log.CloseAndFlush();
}