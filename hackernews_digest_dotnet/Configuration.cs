using System.Text.Json.Serialization;

namespace hackernews_digest_dotnet;

public class ConfigurationFilter
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}

public class Configuration
{
    [JsonPropertyName("api_base_url")]
    public string ApiBaseUrl { get; set; }

    [JsonPropertyName("purge_after_days")]
    public int PurgeAfterDays { get; set; }

    [JsonPropertyName("db_dsn")]
    public string DbDsn { get; set; }

    [JsonPropertyName("blacklisted_domains")]
    public List<string> BlacklistedDomains { get; set; }

    [JsonPropertyName("filters")]
    public List<ConfigurationFilter> Filters { get; set; }

    [JsonPropertyName("email_to")]
    public string EmailTo { get; set; }

    [JsonPropertyName("smtp")]
    public ConfigurationSmtp Smtp { get; set; }
}

public class ConfigurationSmtp
{
    [JsonPropertyName("host")]
    public string Host { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    [JsonPropertyName("from")]
    public string From { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("use_tls")]
    public bool UseTls { get; set; }

    [JsonPropertyName("use_ssl")]
    public bool UseSsl { get; set; }
}