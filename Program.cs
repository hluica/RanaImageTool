using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;

using RanaImageTool.Commands;
using RanaImageTool.Infrastructure;
using RanaImageTool.Services;
using RanaImageTool.Utils;

using Spectre.Console;

using Spectre.Console.Cli;

namespace RanaImageTool;

public static class Program
{
    private static readonly RecyclableMemoryStreamManager.Options _rmsOptions = new()
    {
        BlockSize = 131072, // 128KiB
        LargeBufferMultiple = 1048576, // 1MiB
        MaximumBufferSize = 134217728, // 128MiB
        MaximumSmallPoolFreeBytes = 2147483648L, // 2GiB
        MaximumLargePoolFreeBytes = 2147483648L, // 2GiB
        GenerateCallStacks = false
    };
    public static async Task<int> Main(string[] args)
    {
        // 1. 配置服务集合
        var services = new ServiceCollection()
            .AddSingleton(_ => new RecyclableMemoryStreamManager(_rmsOptions))
            .AddSingleton<IImageService, ImageService>()
            .AddSingleton<IBatchRunner, BatchRunner>();

        // 2. 注册服务并配置应用
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            _ = config.SetApplicationName("RanaImageTool")
                .SetApplicationVersion(GetAppVersion());

            // 注册命令
            _ = config.AddCommand<WebpToPngCommand>("webp")
                .WithDescription("Convert WebP files to PNG format, and delete the original files. ");

            _ = config.AddCommand<SetPpiCommand>("setppi")
                .WithDescription("Set PPI for JPEG/PNG files, and convert unexpected encoding formats. ");

            _ = config.AddCommand<JpgToPngCommand>("convert")
                .WithDescription("Convert JPEG files to PNG format, and delete the original files.");

            _ = config.AddCommand<ScanCommand>("scan")
                .WithDescription("Scan directories and count image files. ");

            _ = config.SetExceptionHandler((ex, _) =>
            {
                AnsiConsole.Error.MarkupLine("[red][[ERROR]][/] [white]Unexpected Error Happened:[/]");
                AnsiConsole.Error.WriteException(ex, ExceptionFormats.ShortenEverything);

                // -1: exceptions handled by framework automatically; 1: exceptions handled in program manually.
                return -1;
            });
        });

        return await app.RunAsync(args);
    }

    private static string GetAppVersion()
    {
        string? version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }
}
