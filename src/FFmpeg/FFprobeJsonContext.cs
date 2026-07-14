using System.Text.Json.Serialization;

namespace VideoOptimizer.FFmpeg;

[JsonSerializable(typeof(FFprobeResult))]
[JsonSerializable(typeof(FFprobeStream))]
[JsonSerializable(typeof(FFprobeFormat))]
[JsonSerializable(typeof(List<FFprobeStream>))]
internal partial class FFprobeJsonContext : JsonSerializerContext;
