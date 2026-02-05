using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using RanaImageTool.Commands;
using RanaImageTool.Infrastructure;
using RanaImageTool.Services;

using Spectre.Console.Cli;

namespace RanaImageTool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. 创建服务集合
        var services = new ServiceCollection();

        // 2. 注册业务服务
        _ = services
            .AddSingleton<IImageService, ImageService>()
            .AddSingleton<IBatchRunner, BatchRunner>();

        // 3. 配置 Spectre.Console.Cli 使用 DI
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            _ = config.SetApplicationName("RanaImageTool")
                .SetApplicationVersion(GetAppVersion());

            // 注册命令
            _ = config.AddCommand<WebpToPngCommand>("webp")
                .WithDescription("将 WebP 文件转换为 PNG 文件，并删除原始文件。");

            _ = config.AddCommand<SetPpiCommand>("setppi")
                .WithDescription("为 JPEG/PNG 文件设置 PPI。同时转换未预期的编码格式");

            _ = config.AddCommand<JpgToPngCommand>("convert")
                .WithDescription("将 JPEG 文件转换为 PNG 文件，并删除原始文件。");

            _ = config.AddCommand<ScanCommand>("scan")
                .WithDescription("扫描目录并计数图片文件。");
        });

        return await app.RunAsync(args);
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return !string.IsNullOrWhiteSpace(version) ? version : "unknown";
    }
}
