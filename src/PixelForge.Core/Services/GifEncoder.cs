using System.Text;
using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// High-performance, zero-external-dependency GIF89a multi-frame animated encoder in pure C#.
/// Supports per-frame durations, transparency keys, Netscape 2.0 loop extension, and adaptive palette quantization.
/// </summary>
public static class GifEncoder
{
    /// <summary>
    /// Encodes a sequence of pixel frames into an animated GIF89a stream.
    /// </summary>
    /// <param name="frames">Collection of animation frames with pixel data, dimensions, and duration in milliseconds.</param>
    /// <param name="outputStream">Destination stream to write GIF data to.</param>
    /// <param name="scale">Integer scaling multiplier applied to frames (nearest-neighbor).</param>
    /// <param name="cancellationToken">Cancellation token to abort the encoding process.</param>
    public static async Task EncodeAsync(
        IEnumerable<(PixelColor[] pixels, int width, int height, int durationMs)> frames,
        Stream outputStream,
        int scale = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(outputStream);

        if (scale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be >= 1.");
        }

        var frameList = frames.ToList();
        if (frameList.Count == 0)
        {
            throw new ArgumentException("At least one frame must be provided.", nameof(frames));
        }

        int baseWidth = frameList[0].width;
        int baseHeight = frameList[0].height;

        if (baseWidth <= 0 || baseHeight <= 0)
        {
            throw new ArgumentException("Frame dimensions must be positive.", nameof(frames));
        }

        int scaledWidth = baseWidth * scale;
        int scaledHeight = baseHeight * scale;

        foreach (var frame in frameList)
        {
            if (frame.width != baseWidth || frame.height != baseHeight)
            {
                throw new ArgumentException("All animation frames must share the same canvas dimensions.", nameof(frames));
            }

            if (frame.pixels == null || frame.pixels.Length != baseWidth * baseHeight)
            {
                throw new ArgumentException("Frame pixel buffer is null or does not match frame dimensions.", nameof(frames));
            }
        }

        using var memoryStream = new MemoryStream();

        // 1. Header: GIF89a (6 bytes)
        byte[] header = "GIF89a"u8.ToArray();
        memoryStream.Write(header, 0, header.Length);

        // 2. Logical Screen Descriptor (7 bytes)
        // Width (2 bytes), Height (2 bytes), Packed (1 byte), BgColorIndex (1 byte), PixelAspectRatio (1 byte)
        WriteUInt16(memoryStream, (ushort)scaledWidth);
        WriteUInt16(memoryStream, (ushort)scaledHeight);
        // Packed: No Global Color Table (bit 7 = 0), color resolution 7 (8 bits), sort 0, size 0 => 0x70
        memoryStream.WriteByte(0x70);
        memoryStream.WriteByte(0x00); // Background Color Index
        memoryStream.WriteByte(0x00); // Pixel Aspect Ratio

        // 3. Netscape 2.0 Application Extension Block (Loop indefinitely)
        // Introducer 0x21, Label 0xFF, Block Size 0x0B
        memoryStream.WriteByte(0x21);
        memoryStream.WriteByte(0xFF);
        memoryStream.WriteByte(0x0B);
        byte[] netscapeId = "NETSCAPE2.0"u8.ToArray();
        memoryStream.Write(netscapeId, 0, netscapeId.Length);
        memoryStream.WriteByte(0x03); // Sub-block length
        memoryStream.WriteByte(0x01); // Loop sub-block index
        WriteUInt16(memoryStream, 0); // Loop count = 0 (infinite)
        memoryStream.WriteByte(0x00); // Block terminator

        // 4. Encode each frame
        for (int frameIdx = 0; frameIdx < frameList.Count; frameIdx++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = frameList[frameIdx];
            var scaledPixels = ScalePixels(frame.pixels, baseWidth, baseHeight, scale);

            var (colorTable, colorTablePower, indexedPixels, transparentIndex, hasTransparency) =
                QuantizeAndIndexFrame(scaledPixels);

            // 4.1 Graphic Control Extension (8 bytes)
            memoryStream.WriteByte(0x21); // Extension Introducer
            memoryStream.WriteByte(0xF9); // Graphic Control Label
            memoryStream.WriteByte(0x04); // Block Size

            // Packed Fields:
            // Bits 4-2: Disposal Method = 2 (Restore to background color)
            // Bit 0: Transparent Color Flag (1 if hasTransparency, 0 otherwise)
            byte gcePacked = (byte)((2 << 2) | (hasTransparency ? 1 : 0));
            memoryStream.WriteByte(gcePacked);

            // Delay time in 1/100ths of a second (centiseconds)
            ushort delayCs = (ushort)Math.Max(1, frame.durationMs / 10);
            WriteUInt16(memoryStream, delayCs);

            // Transparent color index
            memoryStream.WriteByte(hasTransparency ? (byte)transparentIndex : (byte)0);
            memoryStream.WriteByte(0x00); // Block terminator

            // 4.2 Image Descriptor (10 bytes)
            memoryStream.WriteByte(0x2C); // Image Separator
            WriteUInt16(memoryStream, 0); // Image Left
            WriteUInt16(memoryStream, 0); // Image Top
            WriteUInt16(memoryStream, (ushort)scaledWidth);
            WriteUInt16(memoryStream, (ushort)scaledHeight);

            // Packed Fields:
            // Bit 7: Local Color Table Flag (1)
            // Bit 6: Interlace Flag (0)
            // Bit 5: Sort Flag (0)
            // Bits 0-2: Size of Local Color Table (colorTablePower)
            byte idPacked = (byte)(0x80 | (colorTablePower & 0x07));
            memoryStream.WriteByte(idPacked);

            // 4.3 Local Color Table
            memoryStream.Write(colorTable, 0, colorTable.Length);

            // 4.4 Image Data (LZW compressed)
            WriteLzwImageData(memoryStream, indexedPixels, colorTablePower);
        }

        // 5. GIF Trailer
        memoryStream.WriteByte(0x3B);

        // 6. Write out to destination stream
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static (byte[] colorTable, int colorTablePower, byte[] indexedPixels, int transparentIndex, bool hasTransparency)
        QuantizeAndIndexFrame(PixelColor[] pixels)
    {
        bool hasTransparency = false;
        var colorCounts = new Dictionary<uint, int>();

        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            if (p.A < 128)
            {
                hasTransparency = true;
            }
            else
            {
                uint rgb = ((uint)p.R << 16) | ((uint)p.G << 8) | p.B;
                colorCounts[rgb] = colorCounts.TryGetValue(rgb, out int count) ? count + 1 : 1;
            }
        }

        int maxOpaque = hasTransparency ? 255 : 256;
        int transparentIndex = hasTransparency ? 0 : -1;
        var colorToIndex = new Dictionary<uint, byte>(colorCounts.Count);

        List<uint> paletteRgbList;

        if (colorCounts.Count <= maxOpaque)
        {
            paletteRgbList = new List<uint>(colorCounts.Keys);
            for (int i = 0; i < paletteRgbList.Count; i++)
            {
                byte index = (byte)(hasTransparency ? i + 1 : i);
                colorToIndex[paletteRgbList[i]] = index;
            }
        }
        else
        {
            // Popularity quantization: select top maxOpaque most frequent colors
            paletteRgbList = colorCounts
                .OrderByDescending(kv => kv.Value)
                .Take(maxOpaque)
                .Select(kv => kv.Key)
                .ToList();

            for (int i = 0; i < paletteRgbList.Count; i++)
            {
                byte index = (byte)(hasTransparency ? i + 1 : i);
                colorToIndex[paletteRgbList[i]] = index;
            }

            // Map remaining colors to nearest neighbor by weighted Euclidean distance
            foreach (var kv in colorCounts)
            {
                if (!colorToIndex.ContainsKey(kv.Key))
                {
                    uint color = kv.Key;
                    int bestDistance = int.MaxValue;
                    byte bestIndex = (byte)(hasTransparency ? 1 : 0);

                    for (int pIdx = 0; pIdx < paletteRgbList.Count; pIdx++)
                    {
                        int dist = CalculateColorDistance(color, paletteRgbList[pIdx]);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestIndex = (byte)(hasTransparency ? pIdx + 1 : pIdx);
                        }
                    }

                    colorToIndex[color] = bestIndex;
                }
            }
        }

        int totalEntries = (hasTransparency ? 1 : 0) + paletteRgbList.Count;
        totalEntries = Math.Max(2, totalEntries); // GIF spec requires minimum 2 entries

        // Find smallest power N where 2^(N+1) >= totalEntries
        int power = 0;
        while ((1 << (power + 1)) < totalEntries && power < 7)
        {
            power++;
        }

        int tableCapacity = 1 << (power + 1);
        byte[] colorTable = new byte[tableCapacity * 3];

        if (hasTransparency)
        {
            // Entry 0 is transparent key (0, 0, 0)
            colorTable[0] = 0;
            colorTable[1] = 0;
            colorTable[2] = 0;

            for (int i = 0; i < paletteRgbList.Count; i++)
            {
                uint rgb = paletteRgbList[i];
                int offset = (i + 1) * 3;
                colorTable[offset] = (byte)((rgb >> 16) & 0xFF);     // R
                colorTable[offset + 1] = (byte)((rgb >> 8) & 0xFF);  // G
                colorTable[offset + 2] = (byte)(rgb & 0xFF);         // B
            }
        }
        else
        {
            for (int i = 0; i < paletteRgbList.Count; i++)
            {
                uint rgb = paletteRgbList[i];
                int offset = i * 3;
                colorTable[offset] = (byte)((rgb >> 16) & 0xFF);     // R
                colorTable[offset + 1] = (byte)((rgb >> 8) & 0xFF);  // G
                colorTable[offset + 2] = (byte)(rgb & 0xFF);         // B
            }
        }

        byte[] indexedPixels = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            if (p.A < 128)
            {
                indexedPixels[i] = (byte)transparentIndex;
            }
            else
            {
                uint rgb = ((uint)p.R << 16) | ((uint)p.G << 8) | p.B;
                indexedPixels[i] = colorToIndex[rgb];
            }
        }

        return (colorTable, power, indexedPixels, transparentIndex, hasTransparency);
    }

    private static int CalculateColorDistance(uint c1, uint c2)
    {
        int r1 = (int)((c1 >> 16) & 0xFF);
        int g1 = (int)((c1 >> 8) & 0xFF);
        int b1 = (int)(c1 & 0xFF);

        int r2 = (int)((c2 >> 16) & 0xFF);
        int g2 = (int)((c2 >> 8) & 0xFF);
        int b2 = (int)(c2 & 0xFF);

        int dr = r1 - r2;
        int dg = g1 - g2;
        int db = b1 - b2;

        return (dr * dr * 2) + (dg * dg * 4) + (db * db * 3);
    }

    private static void WriteLzwImageData(Stream stream, byte[] indexedPixels, int colorTablePower)
    {
        int minCodeSize = Math.Max(2, colorTablePower + 1);
        stream.WriteByte((byte)minCodeSize);

        int clearCode = 1 << minCodeSize;
        int eoiCode = clearCode + 1;
        int firstCode = eoiCode + 1;

        int codeSize = minCodeSize + 1;
        int maxCode = (1 << codeSize) - 1;
        int nextCode = firstCode;

        int curAccum = 0;
        int curBits = 0;
        byte[] subBlock = new byte[255];
        int subBlockCount = 0;

        void WriteCode(int code, int bits)
        {
            curAccum |= (code << curBits);
            curBits += bits;

            while (curBits >= 8)
            {
                subBlock[subBlockCount++] = (byte)(curAccum & 0xFF);
                if (subBlockCount == 255)
                {
                    stream.WriteByte(255);
                    stream.Write(subBlock, 0, 255);
                    subBlockCount = 0;
                }
                curAccum >>= 8;
                curBits -= 8;
            }
        }

        void Flush()
        {
            if (curBits > 0)
            {
                subBlock[subBlockCount++] = (byte)(curAccum & 0xFF);
                if (subBlockCount == 255)
                {
                    stream.WriteByte(255);
                    stream.Write(subBlock, 0, 255);
                    subBlockCount = 0;
                }
                curAccum = 0;
                curBits = 0;
            }

            if (subBlockCount > 0)
            {
                stream.WriteByte((byte)subBlockCount);
                stream.Write(subBlock, 0, subBlockCount);
                subBlockCount = 0;
            }

            stream.WriteByte(0x00); // 0-length sub-block terminator
        }

        WriteCode(clearCode, codeSize);

        if (indexedPixels.Length == 0)
        {
            WriteCode(eoiCode, codeSize);
            Flush();
            return;
        }

        var dict = new Dictionary<int, int>(4096);
        int prefix = indexedPixels[0];

        for (int i = 1; i < indexedPixels.Length; i++)
        {
            int k = indexedPixels[i];
            int key = (prefix << 8) | k;

            if (dict.TryGetValue(key, out int code))
            {
                prefix = code;
            }
            else
            {
                WriteCode(prefix, codeSize);

                if (nextCode < 4096)
                {
                    dict[key] = nextCode++;
                    if (nextCode > maxCode && codeSize < 12)
                    {
                        codeSize++;
                        maxCode = (1 << codeSize) - 1;
                    }
                }
                else
                {
                    WriteCode(clearCode, codeSize);
                    dict.Clear();
                    codeSize = minCodeSize + 1;
                    maxCode = (1 << codeSize) - 1;
                    nextCode = firstCode;
                }

                prefix = k;
            }
        }

        WriteCode(prefix, codeSize);
        WriteCode(eoiCode, codeSize);
        Flush();
    }

    private static PixelColor[] ScalePixels(PixelColor[] source, int width, int height, int scale)
    {
        if (scale == 1)
        {
            return source;
        }

        int scaledWidth = width * scale;
        int scaledHeight = height * scale;
        var result = new PixelColor[scaledWidth * scaledHeight];

        for (int y = 0; y < scaledHeight; y++)
        {
            int srcY = y / scale;
            int yOffset = y * scaledWidth;
            int srcYOffset = srcY * width;

            for (int x = 0; x < scaledWidth; x++)
            {
                int srcX = x / scale;
                result[yOffset + x] = source[srcYOffset + srcX];
            }
        }

        return result;
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
    }
}
