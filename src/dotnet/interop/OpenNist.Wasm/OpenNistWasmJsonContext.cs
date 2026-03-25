namespace OpenNist.Wasm;

using System.Text.Json.Serialization;
using OpenNist.Wsq;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(WsqCommentInfo))]
[JsonSerializable(typeof(WsqFileInfo))]
[JsonSerializable(typeof(OpenNistNfiqAssessmentResult))]
internal sealed partial class OpenNistWasmJsonContext : JsonSerializerContext
{
}
