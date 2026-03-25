namespace OpenNist.Wasm;

using System.Text.Json.Serialization;
using OpenNist.Wsq;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(WsqCommentInfo))]
[JsonSerializable(typeof(WsqFileInfo))]
[JsonSerializable(typeof(OpenNistNfiqAssessmentResult))]
[JsonSerializable(typeof(OpenNistNistFileResult))]
[JsonSerializable(typeof(OpenNistNistRecordResult))]
[JsonSerializable(typeof(OpenNistNistFieldResult))]
[JsonSerializable(typeof(OpenNistNistFileInput))]
[JsonSerializable(typeof(OpenNistNistRecordInput))]
[JsonSerializable(typeof(OpenNistNistFieldInput))]
internal sealed partial class OpenNistWasmJsonContext : JsonSerializerContext
{
}
