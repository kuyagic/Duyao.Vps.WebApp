using System.Reflection;
using System.Runtime.InteropServices;
using Duyao.TelegramFile.Entity;
using Duyao.TelegramFile.Helper;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging.Console;
using WTelegram;

/*
dotnet publish -p:PublishSingleFile=true -f net8.0 -r linux-arm64 -o publish -c Release
scp -i /root/zmbox publish/telegram.bot.webfile \
kra.erl.re:/opt/docker/app/corefilebot/data/bin/corefilebot
 */
try
{
    #region basePath Check

    var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
    if (string.IsNullOrEmpty(basePath)) basePath = AppContext.BaseDirectory;
    var options = new WebApplicationOptions
    {
        ContentRootPath = basePath,
        Args = args
    };

    #endregion

    var builder = WebApplication.CreateBuilder(options);

    #region Builder 代码

    builder.Services.AddLogging(cfg => cfg.AddSimpleConsole(a =>
        {
            a.SingleLine = true;
            a.ColorBehavior = LoggerColorBehavior.Enabled;
        })
    );

    builder.Services.AddMvcCore();
    builder.Services.Configure<WTelegramClientConfig>(builder.Configuration.GetSection("Telegram"));
    builder.Configuration.AddEnvironmentVariables();


    Helpers.Log = (lvl, str) => { };
    builder.Services.AddSingleton<Client>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Client>>();
            var initer = new WTelegramClientInitializer(
                logger, builder.Configuration
                , provider
            );
            return initer.Initialize();
        }
    ); //AddSingleton TelegramClient

    #endregion

    var app = builder.Build();

    #region App 代码

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();


    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionHandlerPathFeature != null)
            {
                logger.LogError(exceptionHandlerPathFeature.Error, "全局异常处理");

                var error = new ApiResponse
                {
                    Ok = false,
                    Message = exceptionHandlerPathFeature.Error.Message
                };
                await context.Response.WriteAsJsonAsync(error);
            }
        });
    });

    //显式初始化单例，不等到接口调用时才初始化。
    var init = app.Services.GetRequiredService<Client>().UserId;
    if (init > 0) logger.LogInformation($"Bot ID {init} Inited");

    #endregion

    app.MapControllers();
    app.Run();
}
catch (Exception exp)
{
    Console.WriteLine(exp.Message);
    // Console.WriteLine(exp.StackTrace);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine("Exit in 5 seconds");
        Thread.Sleep(5000);
    }
}