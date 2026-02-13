using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;

namespace RanaImageTool.Utils;

public static class PngUtil
{
    // PNG 文件签名
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // 关键 Chunk Type 常量 (ASCII)
    private const uint CHUNK_IHDR = 0x49484452; // 'IHDR'
    private const uint CHUNK_PHYS = 0x70485973; // 'pHYs'
    private const uint CHUNK_IEND = 0x49454E44; // 'IEND'

    /// <summary>
    /// 修改 PNG 图片的分辨率信息 (pHYs 块)。
    /// </summary>
    /// <param name="inputStream">可 Seek 的 PNG 输入流。</param>
    /// <param name="outputStream">输出流。</param>
    /// <param name="targetPpi">目标分辨率 (Pixels Per Inch)。</param>
    public static async Task ModifyPngPpiAsync(Stream inputStream, Stream outputStream, int targetPpi)
    {
        // 1. 验证输入流支持 Seek (根据需求假设一定支持，但做基础防御)
        if (!inputStream.CanSeek)
            throw new ArgumentException("InputStream must be seekable.", nameof(inputStream));
        if (!inputStream.CanRead)
            throw new ArgumentException("InputStream must be readable.", nameof(inputStream));
        if (!outputStream.CanWrite)
            throw new ArgumentException("OutputStream must be writable.", nameof(outputStream));


        // 2. 准备新的 pHYs 块数据
        // 计算 Pixels Per Meter. 1 inch = 0.0254 meters.
        uint ppm = (uint)Math.Round(targetPpi / 0.0254);
        var newPhysChunkBytes = CreatePhysChunk(ppm);
        bool physWritten = false;

        // 3. 处理 PNG 签名
        // 分配一个小缓冲区用于读取头部 (Signature 8 bytes, Chunk Header 8 bytes)
        byte[] headerBuffer = new byte[8];

        // 读取并验证签名
        int read = await inputStream.ReadAsync(headerBuffer.AsMemory(0, 8));
        if (read < 8)
            throw new EndOfStreamException("Input stream is too short to be a PNG.");

        // 验证并写入签名
        if (!headerBuffer.AsSpan(0, 8).SequenceEqual(PngSignature))
            throw new InvalidDataException("Invalid PNG signature.");
        await outputStream.WriteAsync(headerBuffer.AsMemory(0, 8));

        // 借用共享内存池用于数据拷贝，避免大对象分配
        // 大小设为 81920 (80KB) 是一个经验值，能在性能和内存间取得平衡
        byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            // 4. 循环处理块 (Chunks)
            while (true)
            {
                // 读取 Chunk Header: Length (4 bytes) + Type (4 bytes)
                read = await inputStream.ReadAsync(headerBuffer.AsMemory(0, 8));
                if (read == 0)
                    break; // 流结束
                if (read < 8)
                    throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");

                uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(headerBuffer, 0, 4));
                uint chunkType = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(headerBuffer, 4, 4));

                // 核心逻辑分支
                if (chunkType == CHUNK_IHDR)
                {
                    // 1. 写入原本的 IHDR
                    await outputStream.WriteAsync(headerBuffer.AsMemory(0, 8)); // Header
                    await CopyBytesAsync(inputStream, outputStream, chunkLength + 4, copyBuffer); // Data + CRC

                    // 2. 紧接着 IHDR 之后，强制插入新的 pHYs 块
                    // 这是最安全的位置，必然在 IDAT 之前
                    await outputStream.WriteAsync(newPhysChunkBytes);
                    physWritten = true;
                }
                else if (chunkType == CHUNK_PHYS)
                {
                    // 发现旧的 pHYs，直接跳过 (相当于删除)
                    // 跳过长度 = Data Length + CRC (4 bytes)
                    _ = inputStream.Seek(chunkLength + 4, SeekOrigin.Current);
                }
                else if (chunkType == CHUNK_IEND)
                {
                    // 兜底：如果还没写 pHYs (例如流中没有 IHDR)，虽然是非法 PNG，仍在 IEND 前补一个
                    if (!physWritten)
                    {
                        await outputStream.WriteAsync(newPhysChunkBytes);
                        physWritten = true;
                    }

                    // 写入 IEND 并结束
                    await outputStream.WriteAsync(headerBuffer.AsMemory(0, 8));
                    await CopyBytesAsync(inputStream, outputStream, chunkLength + 4, copyBuffer);
                    break;
                }
                else
                {
                    // 普通块 (IDAT, tEXt, etc.)：直接复制
                    await outputStream.WriteAsync(headerBuffer.AsMemory(0, 8));
                    await CopyBytesAsync(inputStream, outputStream, chunkLength + 4, copyBuffer);
                }
            }
        }
        finally
        {
            // 归还内存池
            ArrayPool<byte>.Shared.Return(copyBuffer);
        }
    }

    /// <summary>
    /// 构建完整的 pHYs 块字节数组 (包含 Length, Type, Data, CRC)。
    /// </summary>
    private static ReadOnlyMemory<byte> CreatePhysChunk(uint ppm)
    {
        // pHYs Data Structure (9 bytes):
        // 4 bytes: Pixels per unit, X axis (Big Endian)
        // 4 bytes: Pixels per unit, Y axis (Big Endian)
        // 1 byte : Unit specifier (1 = meter)

        // Chunk Structure:
        // 4 bytes: Length (9)
        // 4 bytes: Type (pHYs)
        // 9 bytes: Data
        // 4 bytes: CRC

        byte[] chunk = new byte[4 + 4 + 9 + 4];
        Span<byte> span = chunk;

        // 1. Length = 9
        BinaryPrimitives.WriteUInt32BigEndian(span[..4], 9);

        // 2. Type = pHYs
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(4, 4), CHUNK_PHYS);

        // 3. Data
        // X Axis PPM
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(8, 4), ppm);
        // Y Axis PPM
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(12, 4), ppm);
        // Unit (1 = meter)
        span[16] = 1;

        // 4. CRC Calculation
        // CRC covers Chunk Type (4 bytes) + Chunk Data (9 bytes) -> Total 13 bytes
        // Offset starts at 4 (Type)
        var crcData = span.Slice(4, 13);

        // 使用 System.IO.Hashing 计算 CRC32
        // 为了绝对安全，我们计算出 UInt32 然后手动 WriteBigEndian。
        uint crcValue = Crc32.HashToUInt32(crcData);
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(17, 4), crcValue);

        return chunk;
    }

    /// <summary>
    /// 从输入流精确复制指定长度的字节到输出流。
    /// </summary>
    private static async Task CopyBytesAsync(Stream input, Stream output, long bytesToCopy, byte[] buffer)
    {
        // bytesToCopy 包含 Data + CRC
        long remaining = bytesToCopy;
        while (remaining > 0)
        {
            // 每次读取 min(buffer.Length, remaining)
            int count = (int)Math.Min(buffer.Length, remaining);

            // 必须确保读够 count 个字节，处理网络流或分片读取的情况
            int bytesRead = 0;
            while (bytesRead < count)
            {
                int n = await input.ReadAsync(buffer.AsMemory(bytesRead, count - bytesRead));
                if (n == 0)
                    throw new EndOfStreamException("Unexpected end of stream during chunk data copy.");
                bytesRead += n;
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead));
            remaining -= bytesRead;
        }
    }
}
