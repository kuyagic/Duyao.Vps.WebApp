using System.Reflection;
using System.Runtime.InteropServices;
using Duyao.ApiBase;
using Microsoft.AspNetCore.Diagnostics;
using Duyao.ApiBase.Middleware;
using Serilog;

/*
dotnet publish -p:PublishSingleFile=true -f net8.0 -r linux-arm64 -o publish -c Release
 */
 
string verString = string.Empty;
try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;

    // 获取当前程序集
    var assembly = Assembly.GetExecutingAssembly();

    var basePath = AppContext.BaseDirectory;
    var options = new WebApplicationOptions
    {
        ContentRootPath = basePath,
        Args = args
    };


    var builder = WebApplication.CreateBuilder(options);

    var logFilePath = Path.Join(basePath, "logs", $"{assembly.GetName().Name?.ToLower() ?? "app"}-.txt");
    
    builder.InitSeriLog(logFilePath);

    // 获取版本信息
    var version = assembly.GetName().Version;

    verString = string.Format(BuildInfo.BuildTime, "CoreApi", version)
            .Replace("__commit_id__", BuildInfo.GIT_NONE)
        ;
    Log.Information($"Starting App. {verString}");

    if (OperatingSystem.IsWindows())
    {
        Console.Title = $"{Assembly.GetEntryAssembly()?.FullName} - {verString}";
    }

    #region Builder 代码

    builder.WebHost.ConfigureKestrel(opt =>
    {
        opt.Limits.MaxRequestBodySize = 10 * 1024 * 1024; //10MB
    });

    builder.Logging.ClearProviders().AddSerilog();
    builder.Services.AddControllers()
        //.AddXmlDataContractSerializerFormatters()
        .AddNewtonsoftJson();

    var config = builder.Configuration.SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile("database.json", optional: false, reloadOnChange: true)
        .Build();

    builder.Services.AddHttpClient();
    builder.Services.AddCors();
    builder.Services.AddHttpContextAccessor();

    // SQLite + EFCore
    // 优先使用环境变量 ConnectionStrings__Default，便于容器化部署
    var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? builder.Configuration.GetConnectionString("Default")
                  ?? "";
    //待注入 pgsql 的 配置
    Console.WriteLine($"[VpnList] ConnectionString: {connStr}");

    builder.Configuration.AddEnvironmentVariables();

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

    #endregion

    app.RegisterMiddleware();
    app.MapControllers();
    app.Run();
}
catch (Exception exp)
{
    Console.WriteLine(exp.Message);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine("Exit in 5 seconds");
        Thread.Sleep(5000);
    }
}
