using System;
using System.IO;
using System.IO.Compression;

namespace Core
{
    public static class DeflateCompressor
    {
        private const int BufferSize = 1048576;

        public static void Compress(Stream input, Stream output, CompressionLevel level = CompressionLevel.Fastest, bool leaveOpen = false)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            long uncompressedLength = input.CanSeek ? input.Length - input.Position : -1L;
            byte[] header = BitConverter.GetBytes(uncompressedLength);
            output.Write(header, 0, header.Length);

            using (DeflateStream deflate = new DeflateStream(output, level, leaveOpen: true))
            {
                input.CopyTo(deflate, BufferSize);
            }

            if (!leaveOpen)
                output.Dispose();
        }

        public static byte[] CompressBytes(byte[] inputBytes, CompressionLevel level = CompressionLevel.Fastest)
        {
            if (inputBytes == null || inputBytes.Length == 0)
                return Array.Empty<byte>();

            using (MemoryStream inputStream = new MemoryStream(inputBytes, writable: false))
            using (MemoryStream outputStream = new MemoryStream())
            {
                byte[] header = BitConverter.GetBytes((long)inputBytes.Length);
                outputStream.Write(header, 0, header.Length);

                using (DeflateStream deflate = new DeflateStream(outputStream, level, leaveOpen: true))
                {
                    inputStream.CopyTo(deflate, BufferSize);
                }

                return outputStream.ToArray();
            }
        }
    }
}