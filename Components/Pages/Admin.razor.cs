using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace aliasbillyclasstime.Components.Pages;

public partial class Admin
{
    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    private Dictionary<string, string>? aliases;

    private string editUrl = "";
    private string newAlias = "";
    private string newUrl = "";
    private string searchText = "";
    private string? editingAlias;
    private string? aliasToDelete;
    private string? createError;
    private string? editError;
    private string? deleteError;

    private bool showNewAliasForm;

    private bool IsEditing =>
        showNewAliasForm ||
        editingAlias is not null ||
        aliasToDelete is not null;

    private IEnumerable<KeyValuePair<string, string>> FilteredAliases =>
        aliases is null
            ? Enumerable.Empty<KeyValuePair<string, string>>()
            : aliases.Where(a =>
                a.Key.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        var http = HttpClientFactory.CreateClient("BillyClassTime");

        aliases =
            await http.GetFromJsonAsync<Dictionary<string, string>>(
                "/api/aliases");
    }

    private void ShowNewAliasForm()
    {
        createError = null;
        showNewAliasForm = true;
    }

    private async Task CreateAlias()
    {
        createError = null;

        var http = HttpClientFactory.CreateClient("BillyClassTime");

        var response = await http.PostAsJsonAsync(
            "/api/aliases",
            new
            {
                Alias = newAlias,
                Url = newUrl
            });

        if (!response.IsSuccessStatusCode)
        {
            createError = await ReadErrorMessageAsync(
                response,
                "No se ha podido crear el alias.");

            return;
        }

        aliases ??= new Dictionary<string, string>();
        aliases[newAlias] = newUrl;

        newAlias = "";
        newUrl = "";
        createError = null;
        showNewAliasForm = false;
    }

    private void CancelNewAlias()
    {
        newAlias = "";
        newUrl = "";
        createError = null;
        showNewAliasForm = false;
    }

    private void HandleEdit(KeyValuePair<string, string> item)
    {
        EditAlias(item.Key, item.Value);
    }

    private void EditAlias(string alias, string url)
    {
        editError = null;
        editingAlias = alias;
        editUrl = url;
    }

    private async Task SaveEdit()
    {
        if (editingAlias is null)
        {
            return;
        }

        editError = null;

        var http = HttpClientFactory.CreateClient("BillyClassTime");

        var response = await http.PutAsJsonAsync(
            $"/api/aliases/{Uri.EscapeDataString(editingAlias)}",
            new
            {
                Url = editUrl
            });

        if (!response.IsSuccessStatusCode)
        {
            editError = await ReadErrorMessageAsync(
                response,
                "No se ha podido actualizar el alias.");

            return;
        }

        if (aliases is not null)
        {
            aliases[editingAlias] = editUrl;
        }

        editingAlias = null;
        editUrl = "";
        editError = null;
    }

    private void CancelEdit()
    {
        editingAlias = null;
        editUrl = "";
        editError = null;
    }

    private void ConfirmDelete(string alias)
    {
        deleteError = null;
        aliasToDelete = alias;
    }

    private async Task DeleteAlias()
    {
        if (aliasToDelete is null)
        {
            return;
        }

        deleteError = null;

        var http = HttpClientFactory.CreateClient("BillyClassTime");

        var alias = aliasToDelete;

        var response = await http.DeleteAsync(
            $"/api/aliases/{Uri.EscapeDataString(alias)}");

        if (!response.IsSuccessStatusCode)
        {
            deleteError = await ReadErrorMessageAsync(
                response,
                "No se ha podido eliminar el alias.");

            return;
        }

        aliases?.Remove(alias);

        aliasToDelete = null;
        deleteError = null;
    }

    private void CancelDelete()
    {
        aliasToDelete = null;
        deleteError = null;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, string fallbackMessage)
    {
        try
        {
            var error =
                await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch
        {
            // Se utiliza el mensaje alternativo.
        }

        return fallbackMessage;
    }

    private sealed record ApiErrorResponse(string Message);
}