using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class WebpToPngCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<BaseSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
        => await _batchRunner.RunBatchAsync(
            settings.Path,
            [".webp"],
            "[webp] From WebP to PNG",
            (file) => _imageService.ConvertFormat(file, ".png")
        );
}
