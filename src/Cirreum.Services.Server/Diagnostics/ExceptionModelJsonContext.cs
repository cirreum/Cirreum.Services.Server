namespace Cirreum.Diagnostics;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(ExceptionModel))]
// Additional values are specified on JsonSerializerContext to support some values for extensions.
// For example, the DeveloperExceptionMiddleware serializes its complex type to JsonElement,
// which problem details then needs to serialize.
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class ExceptionModelJsonContext : JsonSerializerContext {

}