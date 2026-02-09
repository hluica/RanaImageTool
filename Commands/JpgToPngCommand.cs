using RanaImageTool.Models;
using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class JpgToPngCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<BaseSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
        => await _batchRunner.RunBatchAsync(
            settings.Path,
            [".jpg", ".jpeg"],
            "[convert] From JPG to PNG",
            async (job) =>
            {
                var resultStream = await _imageService.ConvertFormatToPngAsync(job.SourceStream);

                // ShouldDeleteOriginal = true: 因为是将 jpg 转为 png，通常意味着替换或清理原图
                return new ProcessedJob(
                    job.OriginalFilePath,
                    ".png",
                    resultStream,
                    ShouldDeleteOriginal: true);
            });
}
