using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace RanaImageTool;

// ==========================================
// 1. 程序入口与配置
// ==========================================
public static class Program
{
    /// <summary>
    ///  程序主要逻辑（按使用目的顺序）：
    ///  A 转换 WebP 文件；
    ///  B 设置 PPI；
    ///  C 转换 JPG 文件；
    ///  D 扫描图片文件数目。
    /// </summary>
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("RanaImageTool");

            config.AddCommand<ScanCommand>("scan")
                .WithDescription("扫描目录并计数图片文件。");

            config.AddCommand<WebpToPngCommand>("webp")
                .WithDescription("将 WebP 文件转换为 PNG 文件，并删除原始文件。");

            config.AddCommand<JpgToPngCommand>("trans")
                .WithDescription("将 JPEG 文件转换为 PNG 文件，并删除原始文件。");

            config.AddCommand<SetPpiCommand>("setppi")
                .WithDescription("为 JPEG/PNG 文件设置 PPI。");

            config.SetApplicationVersion(GetAppVersion());
        });

        return app.Run(args);
    }

    // 自定义方法获取程序版本
    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return !string.IsNullOrWhiteSpace(version) ? version : "unknown";
    }
}

// ==========================================
// 2. 参数设置 (Settings)
// ==========================================

// 基础参数：路径
public class BaseSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("待处理目录。若不指定则使用当前工作目录。子目录将被递归处理。")]
    public string? Path { get; set; }
}

// PPI 设置参数
public class PpiSettings : BaseSettings
{
    [CommandOption("--val [PPI]")]
    [Description("设置固定 PPI 大小。若不指定则使用 --linear 设置。若不带参数则使用默认值 144 pixels / inch。")]
    [DefaultValue(144)]
    public FlagValue<int> PpiValue { get; set; } = new();

    [CommandOption("--linear")]
    [Description("根据图片的宽度设置 PPI 大小。使其数值等于图片宽度的 1/10 。此设置为默认选项。")]
    public bool UseLinear { get; set; }

    public override ValidationResult Validate()
    {
        // 1. 冲突检查：不能同时使用 --linear 和 --val (无论 --val 是否带数值)
        if (UseLinear && PpiValue.IsSet)
        {
            return ValidationResult.Error("选项 '--linear' 和 '--val' 不能同时使用。请仅指定其中一个。");
        }

        // 2. 默认回退逻辑：如果用户什么参数都没填 (既没线性也没定值)
        // 则默认启用 Linear 模式
        if (!UseLinear && !PpiValue.IsSet)
        {
            UseLinear = true;
        }

        return ValidationResult.Success();
    }
}

// ==========================================
// 3. 命令实现 (Commands)
// ==========================================

// --- 逻辑 D: Scan 命令 ---
public class ScanCommand : Command<BaseSettings>
{
    public override int Execute(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        var dir = settings.Path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {dir}");
            return -1;
        }

        AnsiConsole.MarkupLine($"Scanning: [blue]{dir}[/]");

        // 递归查找文件
        var jpgCount = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Count(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

        var pngCount = Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories).Count();

        var webpCount = Directory.EnumerateFiles(dir, "*.webp", SearchOption.AllDirectories).Count();

        // 创建表格
        var table = new Table();
        table.AddColumn("[bold green]Format[/]");
        table.AddColumn("[bold green]Count[/]");
        table.AddRow("JPEG", jpgCount.ToString());
        table.AddRow("PNG", pngCount.ToString());
        table.AddRow("WebP", webpCount.ToString());

        // 设置表格格式
        table.Border(TableBorder.Simple);
        table.Columns[1].Alignment = Justify.Right;
        table.Columns[1].RightAligned();

        // 绘制表格
        AnsiConsole.Write(table);
        return 0;
    }
}

// --- 逻辑 A: WebP 转 PNG 命令 ---
public class WebpToPngCommand : AsyncCommand<BaseSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        return await ProcessorEngine.RunBatchAsync(
            settings.Path,
            [".webp"],
            "Converting WebP to PNG",
            (file) => ProcessorEngine.ConvertFormat(file, ".png")
        );
    }
}

// --- 逻辑 C: JPG 转 PNG (Trans) 命令 ---
public class JpgToPngCommand : AsyncCommand<BaseSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        return await ProcessorEngine.RunBatchAsync(
            settings.Path,
            [".jpg", ".jpeg"],
            "Converting JPG to PNG (Trans Mode)",
            (file) => ProcessorEngine.ConvertFormat(file, ".png")
        );
    }
}

// --- 逻辑 B: Set PPI 命令 ---
public class SetPpiCommand : AsyncCommand<PpiSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PpiSettings settings, CancellationToken cancellationToken)
    {
        // 设置默认值
        int effectivePpi = 144;

        // 读取传入值
        if (settings.PpiValue.IsSet)
        {
            effectivePpi = settings.PpiValue.Value == 0 ? 144 : settings.PpiValue.Value;
        }

        string desc = settings.UseLinear ? "Setting PPI (Linear)" : $"Setting PPI (Fixed: {effectivePpi})";

        return await ProcessorEngine.RunBatchAsync(
            settings.Path,
            [".jpg", ".jpeg", ".png"],
            desc,
            (file) => ProcessorEngine.ModifyPpi(file, effectivePpi, settings.UseLinear)
        );
    }
}

// ==========================================
// 4. 核心处理引擎 (Engine)
// ==========================================
public static class ProcessorEngine
{
    /// <summary>
    /// 通用批处理逻辑：扫描 -> 进度条 -> 并行执行 -> 统计
    /// </summary>
    public static Task<int> RunBatchAsync(string? path, string[] extensions, string activityName, Action<string> action)
    {
        var sw = Stopwatch.StartNew();
        string dir = path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {dir}");
            return Task.FromResult(-1);
        }

        // i. 扫描文件
        AnsiConsole.MarkupLine($"[gray]Scanning files in {dir}...[/]");
        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No matching files found.[/]");
            return Task.FromResult(0);
        }

        AnsiConsole.MarkupLine($"Found [green]{files.Count}[/] files.");

        // 用于收集错误
        var errors = new ConcurrentBag<(string File, string Message)>();

        // ii. 启动进度条 UI
        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns([
                new TaskDescriptionColumn(),    // 任务名
                new ProgressBarColumn(),        // 进度条
                new PercentageColumn(),         // 百分比
                new SpinnerColumn(),            // 转圈圈
                new RemainingTimeColumn(),      // 剩余时间
            ])
            .Start(ctx =>
            {
                var task = ctx.AddTask($"[green]{activityName}[/]", maxValue: files.Count);

                // iii. 并行执行 (限制为 CPU 核心数)
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                Parallel.ForEach(files, parallelOptions, file =>
                {
                    try
                    {
                        action(file);
                    }
                    catch (Exception ex)
                    {
                        errors.Add((file, ex.Message));
                    }
                    finally
                    {
                        task.Increment(1);
                    }
                });
            });

        sw.Stop();

        // iv. 结果汇报
        if (errors.IsEmpty)
        {
            AnsiConsole.MarkupLine($"[green]Success![/] Processed {files.Count} files in [bold]{sw.Elapsed.TotalSeconds:F2}s[/].");
            return Task.FromResult(0);
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Completed with {errors.Count} errors[/] in [bold]{sw.Elapsed.TotalSeconds:F2}s[/].");
            AnsiConsole.Write(new Rule("[red]Failures[/]"));
            foreach (var err in errors)
            {
                AnsiConsole.MarkupLine($"[red]File[/]: {Path.GetFileName(err.File)}");
                AnsiConsole.MarkupLine($"[gray]Error[/]: {err.Message}");
                AnsiConsole.WriteLine();
            }
            return Task.FromResult(1);
        }
    }

    /// <summary>
    /// 逻辑 A & C: 格式转换 (删除源文件)
    /// </summary>
    public static void ConvertFormat(string inputPath, string targetExtension)
    {
        string dir = Path.GetDirectoryName(inputPath)!;
        string fileName = Path.GetFileNameWithoutExtension(inputPath);

        string finalPath = Path.Combine(dir, fileName + targetExtension);
        string tempPath = finalPath + $".tmp_{Guid.NewGuid():N}";

        // 加载图片
        using (var image = Image.Load(inputPath))
        {
            // 根据程序目标，硬编码为 PNG
            var encoder = new PngEncoder();

            // i. 保存临时文件
            image.Save(tempPath, encoder);
        }

        // ii. 覆盖移动
        File.Move(tempPath, finalPath, overwrite: true);

        // 再次验证，避免误删文件
        if (!string.Equals(inputPath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            // iii. 删除源文件
            File.Delete(inputPath);
        }
    }

    /// <summary>
    /// 逻辑 B: 修改 PPI (覆盖源文件)
    /// </summary>
    public static void ModifyPpi(string inputPath, int fixedPpi, bool useLinear)
    {
        string tempPath = inputPath + $".tmp_{Guid.NewGuid():N}";

        // 加载图片
        // 使用文件流以支持 IImageFormat 输出
        using (var fs = File.OpenRead(inputPath))
        {
            IImageFormat format = Image.DetectFormat(fs);
            fs.Position = 0;

            using var image = Image.Load(fs);

            int targetPpi = useLinear ? (int)(image.Width / 10.0) : fixedPpi;

            // 只有修改前后数值相差足够大，才有必要执行。避免无效 I/O 
            if (Math.Abs(image.Metadata.HorizontalResolution - targetPpi) < 5)
            {
                return;
            }

            // 修改元数据
            image.Metadata.HorizontalResolution = targetPpi;
            image.Metadata.VerticalResolution = targetPpi;
            image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

            // 使用原格式保存
            image.Save(tempPath, image.Configuration.ImageFormatsManager.GetEncoder(format));
        }

        // 覆盖源文件
        File.Move(tempPath, inputPath, overwrite: true);
    }
}