namespace RanaImageTool.Services;

public interface IImageService
{
    Task<Stream> ConvertFormatToPngAsync(Stream inputStream);

    Task<Stream> ModifyPpiAsync(Stream inputStream, int fixedPpi, bool useLinear);
}
