using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace ValleyEditor.Sprites;

/// <summary>A minimal RGBA PNG encoder, so icons can be encoded off the GPU and off the game thread.</summary>
internal static class Png
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encode pixels read from a game texture. Game textures use premultiplied alpha, which is undone here.</summary>
    public static byte[] Encode(Color[] pixels, int width, int height)
    {
        // raw scanlines, each prefixed with filter type 0 (none)
        byte[] raw = new byte[height * (width * 4 + 1)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0;
            for (int x = 0; x < width; x++)
            {
                Color c = pixels[y * width + x];
                byte a = c.A;
                raw[o++] = Unpremultiply(c.R, a);
                raw[o++] = Unpremultiply(c.G, a);
                raw[o++] = Unpremultiply(c.B, a);
                raw[o++] = a;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw);

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8; // bit depth
        header[9] = 6; // colour type: RGBA
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static byte Unpremultiply(byte channel, byte alpha)
    {
        return alpha is 0 or 255 ? channel : (byte)Math.Min(255, channel * 255 / alpha);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc ^ 0xFFFFFFFF);
        stream.Write(crcBytes);
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
