# Reference: Error Codes

OpenNist uses a mixed failure model:

- standard .NET exceptions for API misuse such as null, empty, disposed, or out-of-order calls
- structured `Result`-style failures for expected validation, malformed-input, and supported-format outcomes
- library-specific exceptions for strict APIs, with a stable error code and documentation link when available

## NIST failure model

For `OpenNist.Nist`, the public shapes are:

- `NistResult<T>` for non-throwing decode operations such as `TryDecode(...)`
- `NistException` for strict decode operations such as `Decode(...)`

Malformed transaction content is returned as a structured failure from `TryDecode(...)` and thrown as `NistException` from `Decode(...)`.

## WSQ failure model

For `OpenNist.Wsq`, the public shapes are:

- `WsqResult` and `WsqResult<T>` for non-throwing operations such as `TryEncodeAsync(...)`, `TryDecodeAsync(...)`, and `TryInspectAsync(...)`
- `WsqException` for strict operations such as `EncodeAsync(...)`, `DecodeAsync(...)`, and `InspectAsync(...)`
- `WsqValidationResult` and `WsqValidationError` for grouped encode validation details

WSQ encode validation issues are combined instead of being reported one at a time. For example, an image can fail width, height, pixel-depth, bit-rate, and encoder-number checks in a single response.

## NFIQ 2 failure model

For `OpenNist.Nfiq`, the public shapes are:

- `Nfiq2Result<T>` for non-throwing operations such as `TryAnalyzeAsync(...)`
- `Nfiq2Exception` for strict operations such as `AnalyzeAsync(...)`
- `Nfiq2ValidationResult` and `Nfiq2ValidationError` for grouped validation details

Validation issues are combined instead of being reported one at a time. For example, an image can fail width, height, pixel-depth, resolution, and buffer-length checks in a single response.

## Code format

- `OpenNist.Nist` uses the `ONNISTxxxx` prefix.
- `OpenNist.Wsq` uses the `ONWSQxxxx` prefix.
- `OpenNist.Nfiq` uses the `ONNFIQxxxx` prefix.

General ranges:

- `xxx9xxx`: unexpected or internal library failures
- `xxx1xxx`: validation and supported-input failures
- `xxx2xxx`: malformed input or format failures

## NIST codes

## ONNIST9000

Unexpected NIST library failure.

## ONNIST1000

Malformed NIST transaction content that did not match a more specific public decode code.

## ONNIST1001

Decoded record type did not match the type declared in the Type-1 CNT field.

## ONNIST1002

Logical record length exceeds the available remaining bytes.

## ONNIST1003

Logical record did not end with the expected file separator.

## ONNIST1004

Logical record did not start with a LEN field.

## ONNIST1005

LEN field did not contain a terminating separator.

## ONNIST1006

LEN field did not contain a valid integer.

## ONNIST1007

Binary logical record length header was incomplete.

## ONNIST1008

Field tag separator was not found while decoding a fielded logical record.

## ONNIST1009

Logical record did not contain any fields.

## ONNIST1010

The Type-1 CNT field contained an empty logical-record descriptor.

## ONNIST1011

The Type-1 CNT field contained an invalid logical-record type.

## ONNIST1012

The decoder could not infer the expected binary logical-record type from the Type-1 CNT field.

## WSQ codes

## ONWSQ9000

Unexpected WSQ library failure.

## ONWSQ1000

One or more WSQ encode validation checks failed.

When it occurs:

- `TryEncodeAsync(...)` returns `WsqResult.Error`
- `EncodeAsync(...)` throws `WsqException` with grouped validation details

How to fix it:

- inspect `ValidationErrors`
- fix every reported issue before retrying

Retryable: No

Classification: Validation

## ONWSQ1001

Raw-image width must be greater than zero.

## ONWSQ1002

Raw-image height must be greater than zero.

## ONWSQ1003

Raw-image bits per pixel must be 8.

## ONWSQ1004

WSQ bit rate must be greater than zero.

## ONWSQ1005

WSQ encoder number must fit in a byte.

## ONWSQ1006

WSQ software implementation number must fit in an unsigned 16-bit value.

## ONWSQ1007

The raw pixel stream length does not match `width × height`.

## ONWSQ2000

Malformed or unsupported WSQ bitstream.

When it occurs:

- `TryInspectAsync(...)` or `TryDecodeAsync(...)` receives invalid WSQ data
- `InspectAsync(...)` or `DecodeAsync(...)` throws `WsqException`

How to fix it:

- confirm the input really is a WSQ codestream
- ensure the stream starts at the beginning of the WSQ file
- retry with a known-good WSQ sample if you are validating your pipeline

Retryable: No

Classification: Format

## NFIQ 2 codes

## ONNFIQ9000

Unexpected library failure.

When it occurs:

- an `Nfiq2Exception` reached the public boundary without a more specific structured code

How to fix it:

- treat it as a bug or unexpected runtime failure
- capture the full exception, inner exception, and input characteristics
- check the latest docs and open an issue if the input is expected to be supported

Retryable: No

Classification: Internal failure

## ONNFIQ1000

One or more NFIQ 2 validation checks failed.

When it occurs:

- `TryAnalyzeAsync(...)` returns `Nfiq2Result<T>.Error`
- `AnalyzeAsync(...)` or `AnalyzeFileAsync(...)` throws `Nfiq2Exception` with grouped validation details

How to fix it:

- inspect `ValidationErrors`
- fix every reported issue before retrying

Retryable: No

Classification: Validation

## ONNFIQ1001

Raw-image width must be greater than zero.

## ONNFIQ1002

Raw-image height must be greater than zero.

## ONNFIQ1003

Raw-image bits per pixel must be 8.

## ONNFIQ1004

Raw-image resolution must be 500 PPI.

## ONNFIQ1005

Raw pixel buffer length does not match `width × height`.

## ONNFIQ1006

All image rows appear blank after near-white trimming.

## ONNFIQ1007

All image columns appear blank after near-white trimming.

## ONNFIQ1008

The trimmed fingerprint region is still wider than the supported limit.

Current limit: 800 pixels

## ONNFIQ1009

The trimmed fingerprint region is still taller than the supported limit.

Current limit: 1000 pixels

## ONNFIQ1010

FingerJet CreateFeatureSet dimensions exceed the supported native limit.

## ONNFIQ1011

FingerJet CreateFeatureSet resolution falls outside the supported native range.

## ONNFIQ1012

FingerJet CreateFeatureSet width falls outside the supported native range.

## ONNFIQ1013

FingerJet CreateFeatureSet height falls outside the supported native range.

## ONNFIQ1014

FingerJet extraction resolution falls outside the supported native range.

## ONNFIQ1015

FingerJet extraction width is smaller than the supported 500 PPI minimum.

## ONNFIQ1016

FingerJet extraction width exceeds the supported 500 PPI maximum.

## ONNFIQ1017

FingerJet extraction height is smaller than the supported 500 PPI minimum.

## ONNFIQ1018

FingerJet extraction height exceeds the supported 500 PPI maximum.

## ONNFIQ1019

Prepared pixel data is shorter than the declared prepared image size.

## ONNFIQ1020

FingerJet crop planning diverged from the expected native behavior.

When it occurs:

- internal crop planning reached an unexpected state while preparing the image

How to fix it:

- treat this as an unexpected library failure
- keep the input sample and report it if reproducible

Retryable: No

Classification: Internal failure

## ONNFIQ1021

FingerJet working-buffer requirements exceeded the supported native limit.

When it occurs:

- the prepared image shape would require a larger working buffer than the managed port allows

How to fix it:

- reduce the effective fingerprint crop dimensions
- ensure you are passing a supported 500 PPI grayscale fingerprint image

Retryable: No

Classification: Internal failure

## Related guides

- [Score a fingerprint with NFIQ 2](../how-to/score-a-fingerprint-with-nfiq2.md)
- [Use OpenNist from .NET](../how-to/use-opennist-from-dotnet.md)
- [Use OpenNist from TypeScript](../how-to/use-opennist-from-typescript.md)
