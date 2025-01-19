using System.Text.Json.Serialization;

namespace hackernews_digest_dotnet;

public class HackerNewsItem
{
    [JsonPropertyName("id")]
    public long Id { set; get; }
    [JsonPropertyName("title")]
    public string Title { set; get; }
    [JsonPropertyName("url")]
    public string Url { set; get; }
    [JsonPropertyName("time")]
    public long Time { set; get; }
}