namespace OpenNist.Benchmarks.Fixtures;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OpenNist.Nfiq.Model;

internal static class PortableGrayMapFixture
{
    public static (byte[] Pixels, Nfiq2RawImageDescription RawImage) Read(string path)
    {
        using var stream = File.OpenRead(path);

        var magic = ReadToken(stream);
        if (!string.Equals(magic, "P5", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported portable gray map format '{magic}'.");
        }

        var width = int.Parse(ReadToken(stream), System.Globalization.CultureInfo.InvariantCulture);
        var height = int.Parse(ReadToken(stream), System.Globalization.CultureInfo.InvariantCulture);
        var maxValue = int.Parse(ReadToken(stream), System.Globalization.CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidOperationException($"Unsupported portable gray map max value '{maxValue}'.");
        }

        var expectedLength = checked(width * height);
        var pixels = new byte[expectedLength];
        stream.ReadExactly(pixels, 0, pixels.Length);

        return (pixels, new(width, height, BitsPerPixel: 8, PixelsPerInch: 500));
    }

    private static string ReadToken(Stream stream)
    {
        var bytes = new List<byte>(16);
        var inComment = false;

        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0)
            {
                throw new EndOfStreamException("Unexpected end of portable gray map header.");
            }

            var value = (byte)next;
            if (inComment)
            {
                if (value == (byte)'\n')
                {
                    inComment = false;
                }

                continue;
            }

            if (value == (byte)'#')
            {
                inComment = true;
                continue;
            }

            if (char.IsWhiteSpace((char)value))
            {
                if (bytes.Count > 0)
                {
                    return System.Text.Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(bytes));
                }

                continue;
            }

            bytes.Add(value);
        }
    }
}
