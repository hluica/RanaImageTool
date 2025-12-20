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
        int effectivePpi = 144;
        if (settings.PpiValue.IsSet)
        {
            effectivePpi = settings.PpiValue.Value == 0 ? 144 : settings.PpiValue.Value;
        }

        string desc = settings.UseLinear ? "[setppi] Linear Mode" : $"[setppi] Fixed mode, val={effectivePpi}";

        return await _batchRunner.RunBatchAsync(
            settings.Path,
            [".jpg", ".jpeg", ".png"],
            desc,
            (file) => _imageService.ModifyPpi(file, effectivePpi, settings.UseLinear)
        );
    }
}
