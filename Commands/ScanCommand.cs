using RanaImageTool.Settings;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class ScanCommand : Command<BaseSettings>
{
    public override int Execute(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        string dir = settings.Path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {Markup.Escape(dir)}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Scanning: [/][blue underline]{Markup.Escape(dir)}[/]");

        var stats = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetExtension(f).ToLowerInvariant())
            .GroupBy(ext => ext)
            .ToDictionary(g => g.Key, g => g.Count());

        int GetCount(string[] exts) => exts.Sum(e => stats.GetValueOrDefault(e, 0));

        int jpgCount = GetCount([".jpg", ".jpeg"]);
        int pngCount = GetCount([".png"]);
        int webpCount = GetCount([".webp"]);
        int total = jpgCount + pngCount + webpCount;

        var table = new Table();
        _ = table.AddColumn(new TableColumn("[bold green]Format[/]").Footer("[bold]Total[/]"));
        _ = table.AddColumn(new TableColumn("[bold green]Count[/]").RightAligned().Footer($"[bold]{total}[/]"));

        _ = table.AddRow("JPEG", jpgCount.ToString())
             .AddRow("PNG", pngCount.ToString())
             .AddRow("WebP", webpCount.ToString());

        _ = table.Border(TableBorder.Simple);
        AnsiConsole.Write(table);

        return 0;
    }
}
