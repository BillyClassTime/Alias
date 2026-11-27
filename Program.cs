using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using System.Text.Json;
using Scalar.AspNetCore;
using aliasbillyclasstime.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOpenApi();

var aliasesUrl = builder.Configuration["Aliases:Url"]
    ?? throw new InvalidOperationException(
        "No se ha configurado 'Aliases:Url'.");

TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

var blobClient = new BlobClient(new Uri(aliasesUrl),credential);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/api/aliases", async () =>
{
    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString());

    return Results.Ok(aliases);
});

app.MapGet("/api/aliases/{alias}", async (string alias) =>
{
    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString());

    var item = aliases?
        .FirstOrDefault(x =>
            string.Equals(x.Key, alias, StringComparison.OrdinalIgnoreCase));

    if (item.HasValue)
    {
        return Results.Ok(new
        {
            alias = item.Value.Key,
            url = item.Value.Value
        });
    }

    return Results.NotFound(new
    {
        message = "Alias no encontrado",
        alias
    });
});

app.MapPost("/api/aliases", async (CreateAliasRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Alias) ||
        string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new
        {
            message = "Alias y URL son obligatorios"
        });
    }

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new
        {
            message = "La URL no es válida"
        });
    }

    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString())
        ?? new Dictionary<string, string>();

    var exists = aliases.Keys.Any(x =>
        string.Equals(x, request.Alias, StringComparison.OrdinalIgnoreCase));

    if (exists)
    {
        return Results.Conflict(new
        {
            message = "El alias ya existe",
            alias = request.Alias
        });
    }

    aliases[request.Alias] = request.Url;

    var json = JsonSerializer.Serialize(
        aliases,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });

    using var stream = new MemoryStream(
        System.Text.Encoding.UTF8.GetBytes(json));

    await blobClient.UploadAsync(
        stream,
        overwrite: true);

    return Results.Created(
        $"/api/aliases/{request.Alias}",
        new
        {
            alias = request.Alias,
            url = request.Url
        });
});

app.MapPut("/api/aliases/{alias}", async (string alias, UpdateAliasRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new
        {
            message = "La URL es obligatoria"
        });
    }

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new
        {
            message = "La URL no es válida"
        });
    }

    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString())
        ?? new Dictionary<string, string>();

    var existingAlias = aliases.Keys.FirstOrDefault(x =>
        string.Equals(x, alias, StringComparison.OrdinalIgnoreCase));

    if (existingAlias == null)
    {
        return Results.NotFound(new
        {
            message = "Alias no encontrado",
            alias
        });
    }

    aliases[existingAlias] = request.Url;

    var json = JsonSerializer.Serialize(
        aliases,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });

    using var stream = new MemoryStream(
        System.Text.Encoding.UTF8.GetBytes(json));

    await blobClient.UploadAsync(
        stream,
        overwrite: true);

    return Results.Ok(new
    {
        alias = existingAlias,
        url = request.Url
    });
});

app.MapDelete("/api/aliases/{alias}", async (string alias) =>
{
    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString())
        ?? new Dictionary<string, string>();

    var existingAlias = aliases.Keys.FirstOrDefault(x =>
        string.Equals(x, alias, StringComparison.OrdinalIgnoreCase));

    if (existingAlias == null)
    {
        return Results.NotFound(new
        {
            message = "Alias no encontrado",
            alias
        });
    }

    aliases.Remove(existingAlias);

    var json = JsonSerializer.Serialize(
        aliases,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });

    using var stream = new MemoryStream(
        System.Text.Encoding.UTF8.GetBytes(json));

    await blobClient.UploadAsync(
        stream,
        overwrite: true);

    return Results.Ok(new
    {
        message = "Alias eliminado",
        alias = existingAlias
    });
});

app.MapGet("/{alias}", async (string alias) =>
{
    var response = await blobClient.DownloadContentAsync();

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(
        response.Value.Content.ToString());

    var url = aliases?
        .FirstOrDefault(x =>
            string.Equals(x.Key, alias, StringComparison.OrdinalIgnoreCase))
        .Value;

    if (url != null)
    {
        return Results.Redirect(url);
    }

    return Results.NotFound("Alias no encontrado");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record CreateAliasRequest(string Alias, string Url);

record UpdateAliasRequest(string Url);