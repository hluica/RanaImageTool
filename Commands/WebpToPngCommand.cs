using RanaImageTool.Models;
using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class WebpToPngCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<BaseSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webp"
    };

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
        => await _batchRunner.RunBatchAsync(
            settings.Path,
            _supportedExtensions,
            "[webp] From WebP to PNG",
            async (job) =>
            {
                // 1. 纯内存转换
                var resultStream = await _imageService.ConvertFormatToPngAsync(job.SourceStream);

                // 2. 返回处理结果
                return new ProcessedJob(
                    job.OriginalFilePath,
                    ".png",
                    resultStream,
                    ShouldDeleteOriginal: true);
            }
        );
}
