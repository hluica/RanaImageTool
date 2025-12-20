using RanaImageTool.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class ScanCommand : Command<BaseSettings>
{
    public override int Execute(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        var dir = settings.Path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {Markup.Escape(dir)}");
            return -1;
        }

        AnsiConsole.MarkupLine($"[grey]Scanning: [/][blue underline]{Markup.Escape(dir)}[/]");

        // 这里的逻辑比较轻量且只用于展示，可以直接写在 Command 里。
        var stats = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetExtension(f).ToLowerInvariant())
            .GroupBy(ext => ext)
            .ToDictionary(g => g.Key, g => g.Count());

        int GetCount(string[] exts) => exts.Sum(e => stats.GetValueOrDefault(e, 0));

        var jpgCount = GetCount([".jpg", ".jpeg"]);
        var pngCount = GetCount([".png"]);
        var webpCount = GetCount([".webp"]);
        var total = jpgCount + pngCount + webpCount;

        var table = new Table();
        table.AddColumn(new TableColumn("[bold green]Format[/]").Footer("[bold]Total[/]"));
        table.AddColumn(new TableColumn("[bold green]Count[/]").RightAligned().Footer($"[bold]{total}[/]"));

        table.AddRow("JPEG", jpgCount.ToString())
             .AddRow("PNG", pngCount.ToString())
             .AddRow("WebP", webpCount.ToString());

        table.Border(TableBorder.Simple);
        AnsiConsole.Write(table);

        return 0;
    }
}
