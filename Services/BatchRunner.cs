using System.Diagnostics;
using System.Threading.Channels;

using Microsoft.IO;

using RanaImageTool.Models;
using RanaImageTool.Utils;

using Spectre.Console;

namespace RanaImageTool.Services;

public class BatchRunner(RecyclableMemoryStreamManager streamManager) : IBatchRunner
{
    private readonly RecyclableMemoryStreamManager _streamManager = streamManager;

    private static readonly Color _processingAccentColor = ColorHelper.GetWindowsAccentColor(Color.Yellow);
    private static readonly Color _finishedAccentColor = ColorHelper.GetWindowsAccentColor(Color.Green);

    private static readonly int _threadCount = Math.Max(1, Environment.ProcessorCount - 1); // 保留一个核心给非计算任务
    private static readonly int _channelCapacity = Math.Clamp(_threadCount + 2, 6, 48);

    private sealed record FileMetadata(string FilePath, long Size);

    public async Task<int> RunBatchAsync(
        string? path,
        HashSet<string> extensions,
        string activityName,
        Func<SourceJob, Task<ProcessedJob>> processAction)
    {
        var sw = Stopwatch.StartNew();
        string dir = path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error![/] Directory not found: [underline]{Markup.Escape(dir)}[/][/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Scanning: [/][blue underline]{Markup.Escape(dir)}[/]");

        var directoryInfo = new DirectoryInfo(dir);
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
        };

        var files = directoryInfo
            .EnumerateFiles("*", enumerationOptions)
            .Where(f => extensions.Contains(f.Extension))
            .Select(f => new FileMetadata(f.FullName, f.Length))
            .ToList();

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No matching files found.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"Found [green]{files.Count}[/] files.");

        var errors = new List<(string file, Exception exception)>();

        await AnsiConsole
            .Progress()
            .AutoClear(false)
            .Columns([
                new TaskDescriptionColumn(),
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(_processingAccentColor),
                    FinishedStyle = new Style(_finishedAccentColor)
                },
                new PercentageColumn
                {
                    CompletedStyle = new Style(_finishedAccentColor),
                },
                new SpinnerColumn
                {
                    Style = new Style(_processingAccentColor),
                },
                new RemainingTimeColumn
                {
                    Style = new Style(Color.Yellow),
                },
            ])
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]{Markup.Escape(activityName)}[/]", maxValue: files.Count);

                // --- 1. 配置通道 ---

                // Load -> Process 通道 (有界，背压)
                var loadChannel = Channel.CreateBounded<SourceJob>(
                    new BoundedChannelOptions(_channelCapacity)
                    {
                        SingleWriter = true,
                        SingleReader = false
                    });

                // Process -> Save 通道 (有界，防止 Save 速度慢于 Process 导致内存爆炸)
                var saveChannel = Channel.CreateBounded<ProcessedJob>(
                    new BoundedChannelOptions(_channelCapacity * 2)
                    {
                        SingleWriter = false,
                        SingleReader = true
                    });

                // Save -> Result 通道 (无界，通常处理很快)
                var resultChannel = Channel.CreateUnbounded<BatchResult>(
                    new UnboundedChannelOptions
                    {
                        SingleWriter = true,
                        SingleReader = true
                    });

                // --- 2. 配置任务 ---

                var coordinatorTask = Task.Run(async () =>
                {
                    await foreach (var result in resultChannel.Reader.ReadAllAsync())
                    {
                        if (result.Exception != null)
                            errors.Add((result.File, result.Exception));
                        task.Increment(1);
                    }
                });

                var saverTask = Task.Run(async () =>
                {
                    await foreach (var job in saveChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(job.OriginalFilePath)!;
                            string fileName = Path.GetFileNameWithoutExtension(job.OriginalFilePath);
                            string finalPath = Path.Combine(dir, fileName + job.TargetExtension);
                            string tempPath = finalPath + $".tmp_{Guid.NewGuid():N}";

                            // 写入临时文件
                            using (var fs = new FileStream(
                                tempPath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                4096,
                                true))
                            {
                                job.ResultStream.Position = 0;
                                await job.ResultStream.CopyToAsync(fs);
                            }

                            // 原子移动
                            File.Move(tempPath, finalPath, overwrite: true);

                            // 清理原文件
                            if (job.ShouldDeleteOriginal
                                && !string.Equals(job.OriginalFilePath, finalPath, StringComparison.OrdinalIgnoreCase)
                                && File.Exists(job.OriginalFilePath))
                                File.Delete(job.OriginalFilePath);

                            await resultChannel.Writer.WriteAsync(
                                new BatchResult(job.OriginalFilePath, null));
                        }
                        catch (Exception ex)
                        {
                            await resultChannel.Writer.WriteAsync(
                                new BatchResult(job.OriginalFilePath, ex));
                        }
                        finally
                        {
                            // 关键：Saver 负责 Dispose 输出流
                            await job.ResultStream.DisposeAsync();
                        }
                    }
                });

                var processorTasks = new Task[_threadCount];
                for (int i = 0; i < _threadCount; i++)
                {
                    processorTasks[i] = Task.Run(async () =>
                    {
                        await foreach (var job in loadChannel.Reader.ReadAllAsync())
                        {
                            try
                            {
                                // 执行具体的业务逻辑
                                var processedJob = await processAction(job);

                                // 推入 Save 队列
                                await saveChannel.Writer.WriteAsync(processedJob);
                            }
                            catch (Exception ex)
                            {
                                await resultChannel.Writer.WriteAsync(
                                    new BatchResult(job.OriginalFilePath, ex));
                            }
                            finally
                            {
                                // 关键：Processor 负责 Dispose 输入流
                                await job.SourceStream.DisposeAsync();
                            }
                        }
                    });
                }

                var loaderTask = Task.Run(async () =>
                {
                    foreach (var file in files)
                    {
                        RecyclableMemoryStream? stream = null;
                        try
                        {
                            // 使用 RecyclableMemoryStream 替代 new MemoryStream()
                            stream = _streamManager.GetStream("ReadFromFileStream", file.Size);

                            // 异步读取文件到内存
                            using (var fs = new FileStream(
                                file.FilePath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read,
                                4096,
                                true))
                            {
                                await fs.CopyToAsync(stream);
                            }
                            stream.Position = 0; // 重置指针供读取

                            // 推入 Process 队列
                            await loadChannel.Writer.WriteAsync(
                                new SourceJob(file.FilePath, stream));

                            stream = null; // 交出流的所有权
                        }
                        catch (Exception ex)
                        {
                            // 如果读取阶段就失败，直接跳过 Process/Save，发送到 Result
                            await resultChannel.Writer.WriteAsync(
                                new BatchResult(file.FilePath, ex));
                        }
                        finally
                        {
                            if (stream is not null)
                                await stream.DisposeAsync();
                        }
                    }
                });

                // --- 3. 收集结果 ---

                try
                {
                    await loaderTask; // 步骤 1: 等待加载完成
                }
                finally
                {
                    loadChannel.Writer.Complete();
                }

                try
                {
                    await Task.WhenAll(processorTasks); // 步骤 2: 等待所有 CPU 任务完成
                }
                finally
                {
                    saveChannel.Writer.Complete();
                }

                try
                {
                    await saverTask; // 步骤 3: 等待保存任务完成
                }
                finally
                {
                    resultChannel.Writer.Complete();
                }

                await coordinatorTask; // 步骤 4: 等待 UI 更新完毕
            });

        sw.Stop();
        string ts = sw.Elapsed.ToString(sw.Elapsed.Hours >= 1 ? @"h\:mm\:ss\.ff" : @"m\:ss\.ff");

        if (errors.Count == 0)
        {
            AnsiConsole.MarkupLine($"[green]Success![/] Processed {files.Count} files in time: [bold]{ts}[/].");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Completed with {errors.Count} errors[/] in time: [bold]{ts}[/].");
            AnsiConsole.Write(new Rule("[red]Failures[/]"));
            foreach (var (file, exception) in errors)
            {
                AnsiConsole.MarkupLine($"[gray bold]File:[/] [underline]{Markup.Escape(Path.GetFileName(file))}[/]");
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                AnsiConsole.WriteLine();
            }
            return 1;
        }
    }
}
