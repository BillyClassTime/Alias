using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var aliasesUrl = builder.Configuration["Aliases:Url"]
    ?? throw new InvalidOperationException(
        "No se ha configurado 'Aliases:Url'.");

var app = builder.Build();

app.MapGet("/{alias}", async (string alias) =>
{
    using var http = new HttpClient();

    var json = await http.GetStringAsync(aliasesUrl);

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

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

app.Run();