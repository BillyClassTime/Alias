using aliasbillyclasstime.Models;
using aliasbillyclasstime.Services;

namespace aliasbillyclasstime.Endpoints;

public static class AliasEndpoints
{
    public static void MapAliasEndpoints(this WebApplication app)
    {
        app.MapGet("/api/aliases", async(AliasService aliasService) =>
        {
            var aliases = await aliasService.GetAllAsync();

            return Results.Ok(aliases);
        });

        app.MapGet("/api/aliases/{alias}", async(string alias,AliasService aliasService) =>
        {
            var item = await aliasService.GetAsync(alias);

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

        app.MapPost("/api/aliases", async(CreateAliasRequest request,AliasService aliasService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Alias) ||
                string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new
                {
                    message = "Alias y URL son obligatorios"
                });
            }

            if (!IsValidUrl(request.Url))
            {
                return Results.BadRequest(new
                {
                    message = "La URL no es válida"
                });
            }

            var created = await aliasService.CreateAsync(
                request.Alias,
                request.Url);

            if (!created)
            {
                return Results.Conflict(new
                {
                    message = "El alias ya existe",
                    alias = request.Alias
                });
            }

            return Results.Created(
                $"/api/aliases/{request.Alias}",
                new
                {
                    alias = request.Alias,
                    url = request.Url
                });
        });

        app.MapPut("/api/aliases/{alias}", async(string alias,UpdateAliasRequest request,AliasService aliasService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new
                {
                    message = "La URL es obligatoria"
                });
            }

            if (!IsValidUrl(request.Url))
            {
                return Results.BadRequest(new
                {
                    message = "La URL no es válida"
                });
            }

            var updated = await aliasService.UpdateAsync(
                alias,
                request.Url);

            if (!updated)
            {
                return Results.NotFound(new
                {
                    message = "Alias no encontrado",
                    alias
                });
            }

            return Results.Ok(new
            {
                alias,
                url = request.Url
            });
        });

        app.MapDelete("/api/aliases/{alias}", async(string alias,AliasService aliasService) =>
        {
            var deleted = await aliasService.DeleteAsync(alias);

            if (!deleted)
            {
                return Results.NotFound(new
                {
                    message = "Alias no encontrado",
                    alias
                });
            }

            return Results.Ok(new
            {
                message = "Alias eliminado",
                alias
            });
        });

        app.MapGet("/{alias}", async (string alias,AliasService aliasService) =>
        {
            var item = await aliasService.GetAsync(alias);

            if (item.HasValue)
            {
                return Results.Redirect(item.Value.Value);
            }

            return Results.NotFound("Alias no encontrado");
        });
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(
                   url,
                   UriKind.Absolute,
                   out var uri)
               &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps);
    }
}