namespace OpenNist.Viewer.Maui.Services;

using System.Text;
using Models;

internal static class PortableGrayMapCodec
{
    private const byte CommentPrefix = (byte)'#';
    private const byte PortableGrayMapMagicNumberFirstByte = (byte)'P';
    private const byte PortableGrayMapMagicNumberSecondByte = (byte)'5';
    private const int MaxSampleValue = 255;

    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> pixels)
    {
        var header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n{MaxSampleValue}\n");
        var output = new byte[header.Length + pixels.Length];
        header.AsSpan().CopyTo(output);
        pixels.CopyTo(output.AsSpan(header.Length));
        return output;
    }

    public static PortableGrayMapImage Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4
            || data[0] != PortableGrayMapMagicNumberFirstByte
            || data[1] != PortableGrayMapMagicNumberSecondByte)
        {
            throw new InvalidDataException("The image is not a binary PGM document.");
        }

        var index = 2;
        var width = ReadNumber(data, ref index);
        var height = ReadNumber(data, ref index);
        var maxValue = ReadNumber(data, ref index);

        if (maxValue != MaxSampleValue)
        {
            throw new InvalidDataException($"Only 8-bit binary PGM images are supported. Found max value {maxValue}.");
        }

        SkipWhitespaceAndComments(data, ref index);

        var pixelCount = checked(width * height);
        if (data.Length - index != pixelCount)
        {
            throw new InvalidDataException("The binary PGM pixel payload length does not match the declared dimensions.");
        }

        return new(width, height, data[index..].ToArray());
    }

    private static int ReadNumber(ReadOnlySpan<byte> data, ref int index)
    {
        SkipWhitespaceAndComments(data, ref index);

        if (index >= data.Length || !char.IsAsciiDigit((char)data[index]))
        {
            throw new InvalidDataException("The binary PGM header contains an invalid numeric token.");
        }

        var value = 0;
        while (index < data.Length && char.IsAsciiDigit((char)data[index]))
        {
            value = checked((value * 10) + (data[index] - (byte)'0'));
            index++;
        }

        return value;
    }

    private static void SkipWhitespaceAndComments(ReadOnlySpan<byte> data, ref int index)
    {
        while (index < data.Length)
        {
            if (char.IsWhiteSpace((char)data[index]))
            {
                index++;
                continue;
            }

            if (data[index] != CommentPrefix)
            {
                return;
            }

            while (index < data.Length && data[index] != (byte)'\n')
            {
                index++;
            }
        }
    }
}