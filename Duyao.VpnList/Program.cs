using System.Runtime.InteropServices;
using Duyao.TelegramFile.Entity;
using Duyao.VpnList.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/*
dotnet publish -p:PublishSingleFile=true -f net8.0 -r linux-arm64 -o publish -c Release
 */
try
{
    #region basePath Check

    var basePath = AppContext.BaseDirectory;
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

    // 让 MVC 使用 Newtonsoft.Json 替代 System.Text.Json
    builder.Services.AddControllers()
        .AddNewtonsoftJson(opt =>
        {
            opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            opt.SerializerSettings.DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ";
            opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        });

    builder.Services.Configure<MvcNewtonsoftJsonOptions>(opt =>
    {
        opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        opt.SerializerSettings.DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ";
        opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    });

    builder.Services.AddHttpClient();

    // SQLite + EFCore
    // 优先使用环境变量 ConnectionStrings__Default，便于容器化部署
    var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? builder.Configuration.GetConnectionString("Default")
                  ?? "Data Source=/data/vpn.db";
    builder.Services.AddDbContext<VpnDbContext>(opt =>
        opt.UseSqlite(connStr));
    Console.WriteLine($"[VpnList] ConnectionString: {connStr}");

    builder.Configuration.AddEnvironmentVariables();

    #endregion

    var app = builder.Build();

    #region App 代码

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // 自动执行迁移（Code First）
    using (var db = scope.ServiceProvider.GetRequiredService<VpnDbContext>())
    {
        db.Database.Migrate();
        logger.LogInformation("VpnList DB migrated");
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
