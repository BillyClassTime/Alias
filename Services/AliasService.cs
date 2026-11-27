using Azure.Storage.Blobs;
using System.Text;
using System.Text.Json;

namespace aliasbillyclasstime.Services;

public class AliasService
{
    private readonly BlobClient _blobClient;

    public AliasService(BlobClient blobClient)
    {
        _blobClient = blobClient;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var response = await _blobClient.DownloadContentAsync();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(
            response.Value.Content.ToString())
            ?? [];
    }

    public async Task<KeyValuePair<string, string>?> GetAsync(string alias)
    {
        var aliases = await GetAllAsync();

        var item = aliases.FirstOrDefault(x =>
            string.Equals(
                x.Key,
                alias,
                StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(item.Key)
            ? null
            : item;
    }

    public async Task<bool> ExistsAsync(string alias)
    {
        var aliases = await GetAllAsync();

        return aliases.Keys.Any(x =>
            string.Equals(
                x,
                alias,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CreateAsync(string alias, string url)
    {
        var aliases = await GetAllAsync();

        var exists = aliases.Keys.Any(x =>
            string.Equals(
                x,
                alias,
                StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return false;
        }

        aliases[alias] = url;

        await SaveAsync(aliases);

        return true;
    }

    public async Task<bool> UpdateAsync(string alias, string url)
    {
        var aliases = await GetAllAsync();

        var existingAlias = aliases.Keys.FirstOrDefault(x =>
            string.Equals(
                x,
                alias,
                StringComparison.OrdinalIgnoreCase));

        if (existingAlias is null)
        {
            return false;
        }

        aliases[existingAlias] = url;

        await SaveAsync(aliases);

        return true;
    }

    public async Task<bool> DeleteAsync(string alias)
    {
        var aliases = await GetAllAsync();

        var existingAlias = aliases.Keys.FirstOrDefault(x =>
            string.Equals(
                x,
                alias,
                StringComparison.OrdinalIgnoreCase));

        if (existingAlias is null)
        {
            return false;
        }

        aliases.Remove(existingAlias);

        await SaveAsync(aliases);

        return true;
    }

    private async Task SaveAsync(Dictionary<string, string> aliases)
    {
        var json = JsonSerializer.Serialize(
            aliases,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        await _blobClient.UploadAsync(
            stream,
            overwrite: true);
    }
}