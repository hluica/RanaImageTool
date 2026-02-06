using System.Diagnostics;
using System.Threading.Channels;
using Spectre.Console;

namespace RanaImageTool.Services;

public class BatchRunner : IBatchRunner
{
    private readonly record struct BatchResult(string File, Exception? Exception);

    public async Task<int> RunBatchAsync(string? path, string[] extensions, string activityName, Action<string> action)
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

        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
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
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
                new RemainingTimeColumn
                {
                    Style = new Style(Color.Yellow),
                },
            ])
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]{Markup.Escape(activityName)}[/]", maxValue: files.Count);

                // 1. 配置高性能通道
                // jobChannel: 限制容量为 500。如果 Worker 处理不过来，生产者会暂缓写入 (背压)
                var jobChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(500)
                {
                    SingleWriter = true, // 只有一个主循环在写入
                    SingleReader = false // 多个 Worker 抢占读取
                });

                // resultChannel: 结果处理（UI更新/错误记录）通常很快，使用无界通道
                var resultChannel = Channel.CreateUnbounded<BatchResult>(new UnboundedChannelOptions
                {
                    SingleWriter = false, // 多个 Worker 写入
                    SingleReader = true   // 只有一个协调者读取
                });

                // 2. 计算 Worker 数量
                // 预留 1 个核心给 IO/UI/GC，其余核心全速运行业务逻辑
                int workerCount = Math.Max(1, Environment.ProcessorCount - 1);

                // 3-A. 启动协调者 (Coordinator) - 负责 UI 更新和错误收集
                var coordinatorTask = Task.Run(async () =>
                {
                    // 持续读取直到结果通道关闭
                    await foreach (var result in resultChannel.Reader.ReadAllAsync())
                    {
                        // 错误处理：单线程操作，无需锁
                        if (result.Exception != null)
                        {
                            errors.Add((result.File, result.Exception));
                        }

                        // UI 更新：单线程操作，流畅无阻塞
                        task.Increment(1);
                    }
                });

                // 3-B. 启动消费者集群 (Workers) - 负责执行 Action
                var consumerTasks = new Task[workerCount];
                for (int i = 0; i < workerCount; i++)
                {
                    consumerTasks[i] = Task.Run(async () =>
                    {
                        // 持续从任务通道读取
                        await foreach (string file in jobChannel.Reader.ReadAllAsync())
                        {
                            try
                            {
                                // 执行传入的具体业务逻辑
                                action(file);

                                // 发送成功信号
                                await resultChannel.Writer.WriteAsync(new BatchResult(file, null));
                            }
                            catch (Exception ex)
                            {
                                // 发送失败信号，携带异常信息
                                await resultChannel.Writer.WriteAsync(new BatchResult(file, ex));
                            }
                        }
                    });
                }

                // 3-C. 生产者 (Producer) - 快速分发任务
                try
                {
                    foreach (string? file in files)
                    {
                        // 将文件推入管道
                        // 如果管道满了，这里会异步等待，自动平衡生产与消费速度
                        await jobChannel.Writer.WriteAsync(file);
                    }
                }
                finally
                {
                    // 无论生产者是否发生异常，都要确保关闭通道
                    jobChannel.Writer.Complete();
                }

                // 4. 优雅关闭流程
                // 等待所有 Worker 处理完通道中的剩余数据
                await Task.WhenAll(consumerTasks);

                // 告知协调者不会再有结果产生
                resultChannel.Writer.Complete();

                // 等待协调者完成最后的 UI 更新和错误记录
                await coordinatorTask;
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
