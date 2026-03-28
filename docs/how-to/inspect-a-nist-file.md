# How-to: Inspect a NIST File

Use `NistDecoder` to load an ANSI/NIST-style transaction into the OpenNist object model.

## Decode from a stream

```csharp
using OpenNist.Nist;

await using var stream = File.OpenRead("sample.an2");
var result = NistDecoder.TryDecode(stream);

if (!result.IsSuccess)
{
    Console.WriteLine(result.Error!.Code);
    Console.WriteLine(result.Error.Message);
    return;
}

var file = result.Value!;
```

## Walk records and fields

```csharp
foreach (var record in file.Records)
{
    Console.WriteLine($"Type-{record.Type}");

    if (record.IsOpaqueBinaryRecord)
    {
        Console.WriteLine($"Opaque bytes: {record.EncodedBytes.Length}");
        continue;
    }

    foreach (var field in record.Fields)
    {
        Console.WriteLine($"{field.Tag}: {field.Value}");
    }
}
```

## Re-encode after inspection

```csharp
using OpenNist.Nist;

await using var output = File.Create("roundtrip.an2");
NistEncoder.Encode(output, file);
```

OpenNist preserves opaque binary records specifically so transaction files can survive decode/encode workflows without losing raw payload bytes.

See also: [Error codes](../reference/error-codes.md)
