using RanaImageTool.Settings;
using RanaImageTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class ScanCommand : Command<BaseSettings>
{
    protected override int Execute(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        string dir = settings.Path ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            AnsiConsole.Error.MarkupLine($"[red][[ERROR]][/] Directory not found: [red underline]{Markup.Escape(dir)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey][[INFO]][/] Scanning: [blue underline]{Markup.Escape(dir)}[/]");

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
        };

        var stats = Directory
            .EnumerateFiles(dir, "*.*", enumerationOptions)
            .Select(f => Path.GetExtension(f).ToLowerInvariant())
            .GroupBy(ext => ext)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        int GetCount(string[] exts)
            => exts.Sum(e => stats.GetValueOrDefault(e, 0));

        int jpgCount = GetCount([".jpg", ".jpeg"]);
        int pngCount = GetCount([".png"]);
        int webpCount = GetCount([".webp"]);
        int total = jpgCount + pngCount + webpCount;

        var table = new Table()
            .AddColumn(new TableColumn("[bold green]Format[/]").Footer("[bold]Total[/]"))
            .AddColumn(new TableColumn("[bold green]Count[/]").RightAligned().Footer($"[bold]{total}[/]"))
            .AddRow("JPEG", jpgCount.ToString())
            .AddRow("PNG", pngCount.ToString())
            .AddRow("WebP", webpCount.ToString())
            .Border(TableBorder.Simple);

        AnsiConsole.Write(table);

        return 0;
    }
}
