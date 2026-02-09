using RanaImageTool.Models;
using RanaImageTool.Services;
using RanaImageTool.Settings;

using Spectre.Console.Cli;

namespace RanaImageTool.Commands;

public class SetPpiCommand(IBatchRunner batchRunner, IImageService imageService) : AsyncCommand<PpiSettings>
{
    private readonly IBatchRunner _batchRunner = batchRunner;
    private readonly IImageService _imageService = imageService;

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
            [".jpg", ".jpeg", ".png"],
            desc,
            async (job) =>
            {
                // 1. 在内存中计算
                // Service 内部会自动探测流是 Jpeg 还是 Png 并做相应处理
                var resultStream = await _imageService.ModifyPpiAsync(
                    job.SourceStream,
                    effectivePpi,
                    settings.UseLinear
                );

                // 2. 确定目标扩展名
                // 对于 PPI 修改，通常保持原扩展名不变 (.jpg -> .jpg, .png -> .png)
                string originalExtension = Path.GetExtension(job.OriginalFilePath);

                // 3. 返回处理结果
                // ShouldDeleteOriginal = true: 只要扩展名没变，BatchRunner 内部的安全检查会阻止它删除刚刚覆盖好的文件。
                // 这里的 true 是为了保险，如果未来逻辑变成 .jpg -> .png，它能确保原 .jpg 被删掉。
                return new ProcessedJob(
                    job.OriginalFilePath,
                    originalExtension,
                    resultStream,
                    ShouldDeleteOriginal: true);
            });
    }
}
