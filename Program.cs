using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Scalar.AspNetCore;
using aliasbillyclasstime.Components;
using aliasbillyclasstime.Endpoints;
using aliasbillyclasstime.Services;
using aliasbillyclasstime.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("BillyClassTime", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["BillyClassTime:BaseAddress"]
        ?? throw new InvalidOperationException(
            "No se ha configurado 'BillyClassTime:BaseAddress'."));
});

builder.Services.AddOpenApi();

var aliasesUrl = builder.Configuration["Aliases:Url"]
    ?? throw new InvalidOperationException(
        "No se ha configurado 'Aliases:Url'.");

TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

builder.Services.AddSingleton(new BlobClient(new Uri(aliasesUrl), credential));

builder.Services.AddSingleton<AliasService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseMiddleware<EntraAdminMiddleware>();
}

app.MapAliasEndpoints();    

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();