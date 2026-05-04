using System;
using System.IO;
using System.IO.Compression;

public static class QuickLZDecompression
{
    private const int BufferSize = 1048576; // 1 MB

    /// <summary>
    /// Decompresses from <paramref name="input"/> into <paramref name="output"/>.
    /// </summary>
    public static void zdecompress(Stream input, Stream output, bool leaveOpen = false)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (output == null) throw new ArgumentNullException(nameof(output));

        // Read the mandatory 8-byte uncompressed-length header
        byte[] header = new byte[8];
        int totalRead = 0;
        while (totalRead < 8)
        {
            int n = input.Read(header, totalRead, 8 - totalRead);
            if (n == 0)
                throw new InvalidDataException("Input does not contain a valid length header.");
            totalRead += n;
        }

        // The header value is informational only; we let the stream grow naturally.
        // long uncompressedLength = BitConverter.ToInt64(header, 0);

        using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen))
        {
            deflate.CopyTo(output, BufferSize);
        }
    }

    /// <summary>
    /// Decompresses a byte array produced by <see cref="DeflateCompressor.CompressBytes"/>.
    /// </summary>
    public static byte[] decompress(byte[] compressed)
    {
        if (compressed == null || compressed.Length == 0)
            return Array.Empty<byte>();

        using (MemoryStream inputStream = new MemoryStream(compressed, writable: false))
        using (MemoryStream outputStream = new MemoryStream())
        {
            zdecompress(inputStream, outputStream, leaveOpen: true);
            return outputStream.ToArray();
        }
    }
}