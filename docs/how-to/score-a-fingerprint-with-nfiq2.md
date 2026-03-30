# How-to: Score a Fingerprint with NFIQ 2

Use `Nfiq2Algorithm` to score 500 PPI, 8-bit grayscale fingerprint images.

## Score a fingerprint file

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.AnalyzeFileAsync("fingerprint.pgm");

Console.WriteLine($"Score: {result.QualityScore}");
```

## Use the non-throwing API when validation failures are expected

```csharp
using OpenNist.Nfiq;

var algorithm = new Nfiq2Algorithm();
var result = await algorithm.TryAnalyzeAsync(
    rawPixels,
    new Nfiq2RawImageDescription(
        Width: width,
        Height: height,
        BitsPerPixel: 8,
        PixelsPerInch: 500));

if (!result.IsSuccess)
{
    Console.WriteLine(result.Error!.Code);

    foreach (var error in result.Error.ValidationErrors ?? [])
    {
        Console.WriteLine($"- {error.Code}: {error.Message}");
    }

    return;
}

Console.WriteLine($"Score: {result.Value!.QualityScore}");
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

Console.WriteLine($"Score: {result.QualityScore}");
```

## Important input rules

OpenNist.Nfiq currently expects:

- 8-bit grayscale pixels
- 500 PPI input

If your source image is WSQ or another container format, decode or normalize it first so the NFIQ step receives raw grayscale image data in the expected shape.

See also: [Error codes](../reference/error-codes.md)
