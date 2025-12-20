namespace RanaImageTool.Services;

public interface IImageService
{
    /// <summary>
    /// 将指定图片转换为目标格式（通常是 PNG），并删除源文件
    /// </summary>
    void ConvertFormat(string inputPath, string targetExtension);

    /// <summary>
    /// 修改图片的 PPI，如果是 JPG 则无损修改 Exif，否则转码为 PNG
    /// </summary>
    void ModifyPpi(string inputPath, int fixedPpi, bool useLinear);
}
