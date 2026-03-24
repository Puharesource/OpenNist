namespace OpenNist.Tests.Nfiq.TestSupport;

using System.Globalization;
using System.Text;

internal static class Nfiq2PortableGrayMapReader
{
    public static (byte[] Pixels, int Width, int Height) Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magicNumber = ReadToken(reader);
        if (!magicNumber.Equals("P5", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{path}' is not a binary Portable Gray Map image.");
        }

        var width = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        var height = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        var maxValue = int.Parse(ReadToken(reader), CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidOperationException($"'{path}' does not contain 8-bit grayscale data.");
        }

        var pixels = reader.ReadBytes(checked(width * height));
        if (pixels.Length != width * height)
        {
            throw new InvalidOperationException($"'{path}' ended before all pixels could be read.");
        }

        return (pixels, width, height);
    }

    private static string ReadToken(BinaryReader reader)
    {
        var builder = new StringBuilder();
        while (true)
        {
            var current = reader.ReadChar();
            if (char.IsWhiteSpace(current))
            {
                continue;
            }

            if (current == '#')
            {
                while (reader.ReadChar() is not '\n')
                {
                    // Skip Portable Gray Map comment bytes until the line terminator.
                }

                continue;
            }

            builder.Append(current);
            break;
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var current = reader.ReadChar();
            if (char.IsWhiteSpace(current))
            {
                break;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
