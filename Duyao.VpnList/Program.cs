using System.Reflection;
using System.Runtime.InteropServices;
using Duyao.ApiBase;
using Duyao.ApiBase.Middleware;
using Duyao.VpnList.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

    // 关闭微软库（EF Core、ASP.NET Core 等）的 info 级别日志，只保留 Warning 及以上
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

    // 获取版本信息
    var version = assembly.GetName().Version;

    verString = string.Format(BuildInfo.BuildTime, "VpnList", version)
            .Replace("__commit_id__", BuildInfo.GIT_NONE)
        ;
    Log.Information($"Starting App. {verString}");

    if (OperatingSystem.IsWindows())
    {
        Console.Title = $"{Assembly.GetEntryAssembly()?.FullName} - {verString}";
    }

    #region Builder 代码

    // // 让 MVC 使用 Newtonsoft.Json 替代 System.Text.Json
    // // 数据库里 DateTime 始终是 UTC；返回时通过 UtcToLocalIsoDateTimeConverter 转成服务器本地时区
    // builder.Services.AddControllers()
    //     .AddNewtonsoftJson(opt =>
    //     {
    //         opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    //         opt.SerializerSettings.DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ";
    //         opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    //         opt.SerializerSettings.Converters.Add(new UtcToLocalIsoDateTimeConverter());
    //     });

    // builder.Services.Configure<MvcNewtonsoftJsonOptions>(opt =>
    // {
    //     opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    //     opt.SerializerSettings.DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ";
    //     opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    //     opt.SerializerSettings.Converters.Add(new UtcToLocalIsoDateTimeConverter());
    // });

    builder.Services.AddHttpClient();

    // SQLite + EFCore
    // 优先使用环境变量 ConnectionStrings__Default，便于容器化部署
    var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? builder.Configuration.GetConnectionString("Default")
                  ?? "Data Source=/mnt/d/vpn.db";
    builder.Services.AddDbContext<VpnDbContext>(opt =>
        opt.UseSqlite(connStr));
    
    Log.Information($"[VpnList] ConnectionString: {connStr}");

    builder.Configuration.AddEnvironmentVariables();

    #endregion

    var app = builder.Build();

    #region App 代码

    using var scope = app.Services.CreateScope();

    // 自动执行迁移（Code First）
    using (var db = scope.ServiceProvider.GetRequiredService<VpnDbContext>())
    {
        db.Database.Migrate();
        Log.Information("[VpnList] DB migrated");
    }

    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionHandlerPathFeature != null)
            {
                Log.Error(exceptionHandlerPathFeature.Error, "全局异常处理");

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

    app.MapControllers();
    app.RegisterMiddleware();
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
