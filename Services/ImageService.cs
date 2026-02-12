using ExifLibrary;

using Microsoft.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;

namespace RanaImageTool.Services;

public class ImageService(RecyclableMemoryStreamManager streamManager) : IImageService
{
    private readonly RecyclableMemoryStreamManager _streamManager = streamManager;

    private enum ImageFormat
    {
        Png,
        Jpg,
    }

    private static long CalculateEstimatedSize(int width, int height, ImageFormat format)
        => format switch
        {
            ImageFormat.Png => width * height * 2, // 估算 PNG 大小：像素数 * 4字节/像素 * 50%压缩率
            ImageFormat.Jpg => width * height,     // 估算 JPG 大小：像素数 * 4字节/像素 * 25%压缩率
            _ => throw new ArgumentOutOfRangeException(nameof(format), $"不支持的格式: {format}")
        };

    public async Task<Stream> ConvertFormatToPngAsync(Stream inputStream)
    {
        inputStream.Position = 0;

        // 估算输出 PNG 的大小，预分配足够的内存
        var info = await Image.IdentifyAsync(inputStream);
        long esimatedSize = CalculateEstimatedSize(info.Width, info.Height, ImageFormat.Png);

        var outputStream = _streamManager.GetStream("ConvertResultStream", esimatedSize);

        try
        {
            inputStream.Position = 0;

            using (var image = await Image.LoadAsync(inputStream))
            {
                await image.SaveAsync(outputStream, new PngEncoder());
            }

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

    public async Task<(Stream, bool isJpeg)> ModifyPpiAsync(Stream inputStream, int fixedPpi, bool useLinear)
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
        bool isPng = format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase);

        // 估算输出流大小，预分配足够的内存
        long estimatedSize = CalculateEstimatedSize(
            imageInfo.Width,
            imageInfo.Height,
            isJpeg ? ImageFormat.Jpg : ImageFormat.Png);

        var outputStream = _streamManager.GetStream("ModifyPpiResult", estimatedSize);

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
            else if (isPng)
            {
                // 此处暂时使用原本逻辑；后续将开发专用于 PNG 的逻辑。
                inputStream.Position = 0;
                using var image = await Image.LoadAsync(inputStream);

                image.Metadata.HorizontalResolution = targetPpi;
                image.Metadata.VerticalResolution = targetPpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

                await image.SaveAsync(outputStream, new PngEncoder());
            }
            else
            {
                // ImageSharp 逻辑
                inputStream.Position = 0;
                using var image = await Image.LoadAsync(inputStream);

                image.Metadata.HorizontalResolution = targetPpi;
                image.Metadata.VerticalResolution = targetPpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

                // 无视原始格式保存为 PNG 格式的输出流
                await image.SaveAsync(outputStream, new PngEncoder());
            }

            outputStream.Position = 0;
            return (outputStream, isJpeg);
        }
        catch
        {
            await outputStream.DisposeAsync();
            throw;
        }
    }
}
