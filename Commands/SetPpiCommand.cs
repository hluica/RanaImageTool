using RanaImageTool.Models;
using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class SetPpiCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<PpiSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
    };

    public override async Task<int> ExecuteAsync(CommandContext context, PpiSettings settings, CancellationToken cancellationToken)
    {
        int effectivePpi = settings.PpiValue switch
        {
            { IsSet: true, Value: not 0 and var value } => value,
            _ => 144
        };

        string desc = settings.UseLinear
            ? "[setppi] Linear Mode"
            : $"[setppi] Fixed mode, val={effectivePpi}";

        return await _batchRunner.RunBatchAsync(
            settings.Path,
            _supportedExtensions,
            desc,
            async (job) =>
            {
                // 1. 在内存中计算
                // Service 内部会自动探测流是 Jpeg 还是 Png 并做相应处理
                var (resultStream, isJpeg) = await _imageService.ModifyPpiAsync(
                    job.SourceStream,
                    effectivePpi,
                    settings.UseLinear
                );

                // 2. 确定目标扩展名
                // ModifyPpiAsync 方法不只是做 PPI 修改，同时会限制可用的输出格式
                string targetExtension = isJpeg ? ".jpeg" : ".png";

                // 3. 返回处理结果
                // ShouldDeleteOriginal = true: 只要扩展名没变，BatchRunner 内部的安全检查会阻止它删除刚刚覆盖好的文件。
                // 这里的 true 是为了保险，如果发生了 .jpg -> .png，它能确保原 .jpg 被删掉。
                return new ProcessedJob(
                    job.OriginalFilePath,
                    targetExtension,
                    resultStream,
                    ShouldDeleteOriginal: true);
            });
    }
}
