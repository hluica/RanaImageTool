using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaImageTool.Settings;

public class PpiSettings : BaseSettings
{
    [CommandOption("--val [PPI]")]
    [Description("设置固定 PPI 大小。若不指定则使用 --linear 设置。若不带参数则使用默认值 144 pixels / inch。")]
    [DefaultValue(144)]
    public FlagValue<int> PpiValue { get; set; } = new();

    [CommandOption("--linear")]
    [Description("根据图片的宽度设置 PPI 大小。使其数值等于图片宽度的 1/10 。此设置为默认选项。")]
    public bool UseLinear { get; set; }

    public override ValidationResult Validate()
    {
        // 1. 冲突检查
        if (UseLinear && PpiValue.IsSet)
            return ValidationResult.Error("选项 '--linear' 和 '--val' 不能同时使用。请仅指定其中一个。");

        // 2. 默认回退逻辑
        if (!UseLinear && !PpiValue.IsSet)
            UseLinear = true;

        return ValidationResult.Success();
    }
}
