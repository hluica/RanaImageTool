using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class JpgToPngCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<BaseSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        return await _batchRunner.RunBatchAsync(
            settings.Path,
            [".jpg", ".jpeg"],
            "[convert] From JPG to PNG",
            (file) => _imageService.ConvertFormat(file, ".png")
        );
    }
}
