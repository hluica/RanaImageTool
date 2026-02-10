namespace RanaImageTool.Services;

public interface IImageService
{
    Task<Stream> ConvertFormatToPngAsync(Stream inputStream);

    Task<(Stream, bool isJpeg)> ModifyPpiAsync(Stream inputStream, int fixedPpi, bool useLinear);
}
