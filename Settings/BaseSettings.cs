using System.ComponentModel;

using Spectre.Console.Cli;

namespace RanaImageTool.Settings;

public class BaseSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("待处理目录。若不指定则使用当前工作目录。子目录将被递归处理。")]
    public string? Path { get; set; }
}
