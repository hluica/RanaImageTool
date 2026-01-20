using ExifLibrary;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;

namespace RanaImageTool.Services;

public class ImageService : IImageService
{
    public void ConvertFormat(string inputPath, string targetExtension)
    {
        string dir = Path.GetDirectoryName(inputPath)!;
        string fileName = Path.GetFileNameWithoutExtension(inputPath);

        string finalPath = Path.Combine(dir, fileName + targetExtension);
        string tempPath = finalPath + $".tmp_{Guid.NewGuid():N}";

        using (var image = Image.Load(inputPath))
        {
            var encoder = new PngEncoder();
            image.Save(tempPath, encoder);
        }

        File.Move(tempPath, finalPath, overwrite: true);

        if (!string.Equals(inputPath, finalPath, StringComparison.OrdinalIgnoreCase) && File.Exists(inputPath))
        {
            File.Delete(inputPath);
        }
    }

    public void ModifyPpi(string inputPath, int fixedPpi, bool useLinear)
    {
        string tempPath = inputPath + $".tmp_{Guid.NewGuid():N}";
        bool isJpeg = false;
        int targetPpi = 0;
        string? finalPath = null;

        // 阶段一：读取元数据、计算参数以及处理非 JPEG 格式
        using (var fs = File.OpenRead(inputPath))
        {
            var imageInfo = Image.Identify(fs);
            fs.Position = 0;
            var format = Image.DetectFormat(fs);

            isJpeg = format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase);
            targetPpi = useLinear ? (int)(imageInfo.Width / 10.0) : fixedPpi;

            string correctExtension = isJpeg ? ".jpg" : ".png";
            finalPath = Path.ChangeExtension(inputPath, correctExtension);

            if (!isJpeg)
            {
                fs.Position = 0;
                using var image = Image.Load(fs);

                image.Metadata.HorizontalResolution = targetPpi;
                image.Metadata.VerticalResolution = targetPpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

                var encoder = new PngEncoder();
                image.Save(tempPath, encoder);
            }
        }

        // 阶段二：处理 JPEG 格式 (使用文件路径)
        if (isJpeg)
        {
            var exifFile = ImageFile.FromFile(inputPath);

            exifFile.Properties.Remove(ExifTag.XResolution);
            exifFile.Properties.Add(new ExifURational(ExifTag.XResolution, (uint)targetPpi, 1));

            exifFile.Properties.Remove(ExifTag.YResolution);
            exifFile.Properties.Add(new ExifURational(ExifTag.YResolution, (uint)targetPpi, 1));

            exifFile.Properties.Set(ExifTag.ResolutionUnit, ResolutionUnit.Inches);

            exifFile.Save(tempPath);
        }

        // 阶段三：原子化提交与清理
        try
        {
            File.Move(tempPath, finalPath!, overwrite: true);

            if (!finalPath!.Equals(inputPath, StringComparison.OrdinalIgnoreCase) && File.Exists(inputPath))
                File.Delete(inputPath);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }
}
