using ExifLibrary;

using Microsoft.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;

namespace RanaImageTool.Services;

public class ImageService(RecyclableMemoryStreamManager streamManager) : IImageService
{
    private readonly RecyclableMemoryStreamManager _streamManager = streamManager;

    public async Task<Stream> ConvertFormatToPngAsync(Stream inputStream)
    {
        // 创建一个可回收的内存流作为输出
        var outputStream = _streamManager.GetStream();

        try
        {
            // 重置流位置，确保从头读取
            inputStream.Position = 0;

            // ImageSharp 加载
            using (var image = await Image.LoadAsync(inputStream))
            {
                await image.SaveAsync(outputStream, new PngEncoder());
            }

            // 重置输出流位置，以便后续读取写入磁盘
            outputStream.Position = 0;
            return outputStream;
        }
        catch
        {
            // 如果处理过程中出错，必须手动释放创建的输出流
            await outputStream.DisposeAsync();
            throw;
        }
    }

    public async Task<Stream> ModifyPpiAsync(Stream inputStream, int fixedPpi, bool useLinear)
    {
        // 预检图片 PPI
        inputStream.Position = 0;
        var imageInfo = await Image.IdentifyAsync(inputStream);
        int targetPpi = useLinear
            ? (int)(imageInfo.Width / 10.0)
            : fixedPpi;

        // 预检图片格式
        inputStream.Position = 0;
        var format = await Image.DetectFormatAsync(inputStream);
        bool isJpeg = format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase);

        var outputStream = _streamManager.GetStream("ModifyPpiResult");

        try
        {
            if (isJpeg)
            {
                // ExifLibNet 逻辑
                inputStream.Position = 0;
                var exifFile = await ImageFile.FromStreamAsync(inputStream);

                exifFile.Properties.Remove(ExifTag.XResolution);
                exifFile.Properties.Add(new ExifURational(ExifTag.XResolution, (uint)targetPpi, 1));

                exifFile.Properties.Remove(ExifTag.YResolution);
                exifFile.Properties.Add(new ExifURational(ExifTag.YResolution, (uint)targetPpi, 1));

                exifFile.Properties.Set(ExifTag.ResolutionUnit, ResolutionUnit.Inches);

                // 保存到输出流
                await exifFile.SaveAsync(outputStream);
            }
            else
            {
                // ImageSharp 逻辑
                inputStream.Position = 0;
                using var image = await Image.LoadAsync(inputStream);

                image.Metadata.HorizontalResolution = targetPpi;
                image.Metadata.VerticalResolution = targetPpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

                // 保存为 PNG 格式的输出流
                await image.SaveAsync(outputStream, new PngEncoder());
            }

            outputStream.Position = 0;
            return outputStream;
        }
        catch
        {
            await outputStream.DisposeAsync();
            throw;
        }
    }
}
