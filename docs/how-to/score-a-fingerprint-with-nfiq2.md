# How-to: Score a Fingerprint with NFIQ 2

Use `Nfiq2Algorithm` to score 500 PPI, 8-bit grayscale fingerprint images.

## Score a fingerprint file

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.AnalyzeFileAsync("fingerprint.pgm");

Console.WriteLine($"Score: {result.Score}");
```

## Score an in-memory raw image when you already have pixels

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.AnalyzeAsync(
    rawPixels,
    new Nfiq2RawImageDescription(
        Width: 500,
        Height: 500,
        BitsPerPixel: 8,
        PixelsPerInch: 500));

Console.WriteLine($"Score: {result.Score}");
```

## Important input rules

Managed NFIQ 2 analysis currently expects:

- 8-bit grayscale pixels
- 500 PPI input

If your source image is WSQ or another container format, decode or normalize it first so the NFIQ step receives raw grayscale image data in the expected shape.
