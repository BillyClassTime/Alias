using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{alias}", async (string alias) =>
{
    using var http = new HttpClient();

    var json = await http.GetStringAsync(
        "https://billyclasstimesa.blob.core.windows.net/scriptss/aliases.json");

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