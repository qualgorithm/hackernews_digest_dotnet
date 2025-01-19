using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace hackernews_digest_dotnet;

class Program
{
    static Program()
    {
        GC.TryStartNoGCRegion(64 * 1024 * 1024); // a little bit cheating whatever
    }
    
    private readonly HttpClient _hnClient;
    private readonly SqliteConnection _db;
    private readonly Configuration _configuration;
    
    public Program(Configuration config)
    {
        if (!config.ApiBaseUrl.EndsWith("/"))
        {
            config.ApiBaseUrl += "/"; // dotnet HttpClient stupid thing
        }
        _hnClient = new HttpClient()
        {
            BaseAddress = new Uri(config.ApiBaseUrl)
        };
        _db = new SqliteConnection($"Data Source={config.DbDsn}");
        _db.Open();
        _configuration = config;
    }

    private async Task DoJobAsync()
    {
        var ids = await FetchDigestIds();
        var missingIds = FilterMissingIds(ids);
        var blockedDomains = new HashSet<string>(_configuration.BlacklistedDomains);
        var filters = BuildFilters(_configuration);
        var collectedItems = await FetchItemsAsync(missingIds, blockedDomains, filters);
        SaveAll(collectedItems);
    }

    private async Task<long[]> FetchDigestIds()
    {
        var response = await _hnClient.GetAsync("topstories.json");
        var ids = JsonSerializer.Deserialize<long[]>(await response.Content.ReadAsStreamAsync(),
            SourceGenerationContext.Default.Int64Array);
        return ids ?? throw new NullReferenceException();
    }
    
    // POC.
    private async Task<List<HackerNewsItem>> FetchItemsParallelAsync(List<long> ids, HashSet<string> blockedDomains,
        List<Regex> filters)
    {
        var collectedItems = new List<HackerNewsItem>(ids.Count);
        await Parallel.ForEachAsync(ids, async (id, _) =>
        {
            var response = await _hnClient.GetAsync($"item/{id}.json");
            var item = JsonSerializer.Deserialize(await response.Content.ReadAsStreamAsync(),
                SourceGenerationContext.Default.HackerNewsItem);
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.Url))
            {
                return; // wtf??
            }
            
            if (blockedDomains.Count > 0 && IsBlackListed(blockedDomains, item))
            {
                return;
            }

            if (filters.Count > 0)
            {
                if (!filters.Any(f => f.IsMatch(item.Title)))
                {
                    return;
                }
            }

            lock (collectedItems)
            {
                collectedItems.Add(item);
            }
        });
        return collectedItems;
    }

    private async Task<List<HackerNewsItem>> FetchItemsAsync(List<long> ids, HashSet<string> blockedDomains, List<Regex> filters)
    {
        var collectedItems = new List<HackerNewsItem>(ids.Count);
        foreach (var id in ids)
        { 
            var response = await _hnClient.GetAsync($"item/{id}.json");
            var item = JsonSerializer.Deserialize(await response.Content.ReadAsStreamAsync(),
                SourceGenerationContext.Default.HackerNewsItem);
            if (item == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(item.Url))
            {
                continue; // wtf??
            }
            
            if (blockedDomains.Count > 0 && IsBlackListed(blockedDomains, item))
            {
                continue;
            }

            if (filters.Count > 0)
            {
                if (!filters.Any(f => f.IsMatch(item.Title)))
                {
                    continue;
                }
            }
            
            collectedItems.Add(item);
        }
        return collectedItems;
    }

    private void SaveAll(List<HackerNewsItem> collectedItems)
    {
        using var tran = _db.BeginTransaction();
        foreach (var item in collectedItems)
        {
            using var cmd = CreateInsertCommand(item);
            cmd.Transaction = tran;
            cmd.ExecuteNonQuery();
        }
        tran.Commit();
    }
    
    private List<long> FilterMissingIds(long[] ids)
    {
        var result = new List<long>(ids.Length);
        using var cmd = CreateQueryCommand(ids);
        using var reader = cmd.ExecuteReader();
        var filter = new HashSet<long>();
        while (reader.Read())
        {
            filter.Add((long)reader[0]);
        }
        for (var i = 0; i < ids.Length; i++)
        {
            if (filter.Contains(ids[i]))
            {
                continue;
            }
            result.Add(ids[i]);
        }
        return result;
    }

    private SqliteCommand CreateQueryCommand(long[] ids)
    {
        var command = _db.CreateCommand();
        string commandText;
        if (ids.Length == 1)
        {
            commandText = ids[0].ToString();
        }
        else
        {
            var builder = new StringBuilder(1024 * 1024); // 1kb
            builder.Append("SELECT id FROM news_items WHERE id in (");
            for (var i = 0; i < ids.Length - 1; i++)
            {
                builder.Append(ids[i]).Append(',');
            }
            builder.Append(ids[^1]).Append(')');
            commandText = builder.ToString(); 
        }
        command.CommandText = commandText;
        return command;
    }
    

    private SqliteCommand CreateInsertCommand(HackerNewsItem item)
    {
        var command = _db.CreateCommand();
        var builder = new StringBuilder();
        builder.Append("INSERT INTO news_items (id, created_at, news_title, news_url) VALUES (@1, @2, @3, @4)");
        command.Parameters.Add("@1", SqliteType.Integer).Value = item.Id;
        command.Parameters.Add("@2", SqliteType.Integer).Value = item.Time;
        command.Parameters.Add("@3", SqliteType.Text).Value = item.Title;
        command.Parameters.Add("@4", SqliteType.Text).Value = item.Url;
        command.CommandText = builder.ToString();
        return command;
    }
    
    private static List<Regex> BuildFilters(Configuration configuration)
    {
        var result = new List<Regex>(configuration.Filters.Count);
        foreach (var filter in configuration.Filters)
        {
            result.Add(new Regex(filter.Value, RegexOptions.Compiled));
        }
        return result;
    }

    private static bool IsBlackListed(HashSet<string> blockedDomains, HackerNewsItem result)
    {
        return blockedDomains.Contains(new Uri(result.Url).Host);
    }
    
    static async Task Main(string[] args)
    {
        try
        {
            // todo: support command line and emails 
            if (!File.Exists("config.json"))
            {
                Console.WriteLine("Configuration file not found.");
                return;
            }
            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"),
                SourceGenerationContext.Default.Configuration);
            if (config == null)
            {
                Console.WriteLine("Configuration file is invalid.");
                return;
            }
            var program = new Program(config);
            await program.DoJobAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatality!\n{ex}");
        }
    }
}