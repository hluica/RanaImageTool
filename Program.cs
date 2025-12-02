using ExifLibrary;
using SixLabors.ImageSharp;
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

            config.AddCommand<WebpToPngCommand>("webp")
                .WithDescription("将 WebP 文件转换为 PNG 文件，并删除原始文件。");

            config.AddCommand<SetPpiCommand>("setppi")
                .WithDescription("为 JPEG/PNG 文件设置 PPI。同时转换未预期的编码格式");

            config.AddCommand<JpgToPngCommand>("convert")
                .WithDescription("将 JPEG 文件转换为 PNG 文件，并删除原始文件。");

            config.AddCommand<ScanCommand>("scan")
                .WithDescription("扫描目录并计数图片文件。");

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

        // 一次遍历，递归查找文件
        var stats = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetExtension(f).ToLowerInvariant())
            .GroupBy(ext => ext)
            .ToDictionary(g => g.Key, g => g.Count());

        // 获取数据辅助方法
        int GetCount(string[] exts) => exts.Sum(e => stats.GetValueOrDefault(e, 0));

        // 获取数据
        var jpgCount = GetCount([".jpg", ".jpeg"]);
        var pngCount = GetCount([".png"]);
        var webpCount = GetCount([".webp"]);

        var total = jpgCount + pngCount + webpCount;

        // 定义表格
        var table = new Table();

        // 定义列
        // 第一列：表头 "Format" 绿色加粗；页脚 "Total" 白色加粗
        table.AddColumn(new TableColumn("[bold green]Format[/]")
            .Footer("[bold]Total[/]"));

        // 第二列：表头 "Count" 绿色加粗；页脚白色加粗；右对齐
        table.AddColumn(new TableColumn("[bold green]Count[/]")
            .RightAligned()
            .Footer($"[bold]{total}[/]"));

        // 定义行
        table.AddRow("JPEG", jpgCount.ToString());
        table.AddRow("PNG", pngCount.ToString());
        table.AddRow("WebP", webpCount.ToString());

        // 定义样式
        table.Border(TableBorder.Simple);

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
        // 程序每次运行后都将导致图片格式与数量变动，故此处需在每次运行前重新扫描文件
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
        if (!string.Equals(inputPath, finalPath, StringComparison.OrdinalIgnoreCase) && File.Exists(inputPath))
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

        // 将关键变量定义在外部作用域，以便在流关闭后继续使用
        bool isJpeg = false;
        int targetPpi = 0;
        string? finalPath = null;

        // 阶段一：读取元数据、计算参数以及处理非 JPEG 格式 (使用流)
        using (var fs = File.OpenRead(inputPath))
        {
            // 1. 侦测格式与元数据
            var imageInfo = Image.Identify(fs);
            // 重置流位置供 DetectFormat() 使用
            fs.Position = 0;
            var format = Image.DetectFormat(fs);

            // 2. 确定逻辑分支参数
            isJpeg = format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase);
            targetPpi = useLinear ? (int)(imageInfo.Width / 10.0) : fixedPpi;

            // 确定最终路径
            string correctExtension = isJpeg ? ".jpg" : ".png";
            finalPath = Path.ChangeExtension(inputPath, correctExtension);

            // 3. 非 JPEG 格式优化：直接复用当前文件流进行转码
            if (!isJpeg)
            {
                // 再次重置流位置供 Load() 使用
                fs.Position = 0;
                using var image = Image.Load(fs);

                image.Metadata.HorizontalResolution = targetPpi;
                image.Metadata.VerticalResolution = targetPpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

                // 非 JPEG 直接转换为 PNG
                var encoder = new PngEncoder();

                image.Save(tempPath, encoder);
            }
        }

        // 阶段二：处理 JPEG 格式 (使用文件路径)
        // 此时 fs 已 Dispose，文件句柄已释放，可使用路径访问
        if (isJpeg)
        {
            var exifFile = ImageFile.FromFile(inputPath);

            // 使用 Remove() 和 Add() 手动更新分辨率属性
            exifFile.Properties.Remove(ExifTag.XResolution);
            exifFile.Properties.Add(new ExifURational(ExifTag.XResolution, (uint)targetPpi, 1));

            exifFile.Properties.Remove(ExifTag.YResolution);
            exifFile.Properties.Add(new ExifURational(ExifTag.YResolution, (uint)targetPpi, 1));

            // 使用 Set() 自动更新分辨率单位
            exifFile.Properties.Set(ExifTag.ResolutionUnit, ResolutionUnit.Inches);

            exifFile.Save(tempPath);
        }

        // 阶段三：原子化提交与清理
        try
        {
            // 此时 tempPath 必然已生成（无论走哪个分支）
            File.Move(tempPath, finalPath, overwrite: true);

            // 清理旧扩展名的文件（如果它未被覆盖）
            if (!finalPath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) && File.Exists(inputPath))
            {
                File.Delete(inputPath);
            }
        }
        catch
        {
            // 在 Move 失败时尝试清理临时文件
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            // 重新抛出异常以供上层捕获
            throw;
        }
    }
}