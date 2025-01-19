using System.Text.Json.Serialization;

namespace hackernews_digest_dotnet;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(HackerNewsItem))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(ConfigurationFilter))]
[JsonSerializable(typeof(ConfigurationSmtp))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}