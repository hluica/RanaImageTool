using System.Collections.Concurrent;
using System.Diagnostics;

using Spectre.Console;

namespace RanaImageTool.Services;

public class BatchRunner : IBatchRunner
{
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

        var errors = new ConcurrentBag<(string file, Exception exception)>();

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

                await Task.Run(() =>
                {
                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    _ = Parallel.ForEach(files, parallelOptions, file =>
                    {
                        try
                        {
                            action(file);
                        }
                        catch (Exception ex)
                        {
                            errors.Add((file, ex));
                        }
                        finally
                        {
                            task.Increment(1);
                        }
                    });
                });
            });

        sw.Stop();
        string ts = sw.Elapsed.ToString(sw.Elapsed.Hours >= 1 ? @"h\:mm\:ss\.ff" : @"m\:ss\.ff");

        if (errors.IsEmpty)
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
