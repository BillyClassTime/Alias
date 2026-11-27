# Implementación del frontend CRUD

## BillyClassTime Alias Manager

## 1. Objetivo

`BillyClassTime Alias Manager` incorpora una interfaz web para administrar los alias almacenados en `jsons/aliases.json`.

La página de administración está desarrollada con Blazor Web App sobre .NET 10, utiliza renderizado Interactive Server y presenta la interfaz mediante Bootstrap 5.3.9 local.

La implementación permite:

- Consultar los alias existentes.
- Buscar alias por nombre.
- Crear nuevos alias.
- Modificar el destino de un alias.
- Eliminar alias con confirmación previa.
- Actualizar la interfaz sin recargar la página.

## 2. Componentes utilizados

| Componente | Uso |
|---|---|
| Blazor Web App .NET 10 | Interfaz administrativa |
| Interactive Server | Interactividad de los componentes Razor |
| Bootstrap 5.3.9 local | Diseño y comportamiento responsive |
| Minimal API | Operaciones CRUD sobre los alias |
| Azure Blob Storage | Persistencia de `jsons/aliases.json` |
| Managed Identity | Acceso al Blob Storage en Azure |
| `IHttpClientFactory` | Comunicación entre la página Blazor y la Minimal API |

## 3. Página de administración

La página administrativa se encuentra en:

```text
/admin
```

La cabecera del componente declara la ruta, el modo de renderizado y la factoría HTTP:

```razor
@page "/admin"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject IHttpClientFactory HttpClientFactory
```

La interfaz se organiza en cuatro áreas:

1. Cabecera y botón `Nuevo alias`.
2. Formulario de creación, visible únicamente durante una operación de alta.
3. Buscador de alias.
4. Listado de resultados con las acciones `Editar` y `Eliminar`.

## 4. Configuración de HttpClient

El cliente HTTP se registra en `Program.cs`:

```csharp
builder.Services.AddHttpClient("BillyClassTime", client =>
{
    client.BaseAddress = new Uri("http://localhost:5258");
});
```

El componente obtiene el cliente mediante `IHttpClientFactory`:

```csharp
var http = HttpClientFactory.CreateClient("BillyClassTime");
```

La dirección de desarrollo deberá sustituirse por una configuración válida para el entorno de despliegue antes de publicar la aplicación.

## 5. Modelo de datos utilizado por el frontend

El endpoint `GET /api/aliases` devuelve un objeto JSON cuyas propiedades representan los alias:

```json
{
  "Az-104": "https://example.com/az-104",
  "SC-900": "https://example.com/sc-900"
}
```

La página utiliza un diccionario para deserializar esta estructura:

```csharp
private Dictionary<string, string>? aliases;
```

Cada elemento contiene:

- `Key`: nombre del alias.
- `Value`: URL de destino.

## 6. Carga de alias

Los alias se cargan al inicializar el componente:

```csharp
protected override async Task OnInitializedAsync()
{
    var http = HttpClientFactory.CreateClient("BillyClassTime");

    aliases = await http.GetFromJsonAsync<Dictionary<string, string>>(
        "/api/aliases");
}
```

Mientras la operación está en curso, la página muestra un indicador de carga. Al finalizar, los alias se presentan en bloques responsive.

La URL de destino utiliza fondo oscuro, texto claro y ajuste de línea para evitar desbordamientos:

```razor
<div class="bg-dark text-light rounded px-3 py-2 text-break">
    @item.Value
</div>
```

## 7. Búsqueda

El campo de búsqueda actualiza el filtro en cada pulsación:

```razor
<input id="search"
       type="search"
       class="form-control"
       placeholder="Escribe parte del alias..."
       @bind="searchText"
       @bind:event="oninput" />
```

El filtrado no distingue entre mayúsculas y minúsculas:

```csharp
private IEnumerable<KeyValuePair<string, string>> FilteredAliases =>
    aliases is null
        ? Enumerable.Empty<KeyValuePair<string, string>>()
        : aliases.Where(a =>
            a.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase));
```

El contador utiliza el conjunto filtrado:

```razor
<p>Alias encontrados: @FilteredAliases.Count()</p>
```

De esta forma, el número mostrado coincide con los resultados visibles.

## 8. Creación de alias

El botón `Nuevo alias` muestra un formulario con los campos:

- Alias.
- Destino.
- Guardar.
- Cancelar.

Los valores se enlazan mediante `@bind`:

```razor
<input id="newAlias"
       type="text"
       class="form-control"
       placeholder="Ej. az-104"
       @bind="newAlias"
       autofocus />

<input id="newUrl"
       type="url"
       class="form-control bg-dark text-white"
       placeholder="https://..."
       @bind="newUrl" />
```

La creación utiliza `POST /api/aliases`:

```csharp
private async Task CreateAlias()
{
    var http = HttpClientFactory.CreateClient("BillyClassTime");

    var response = await http.PostAsJsonAsync(
        "/api/aliases",
        new
        {
            Alias = newAlias,
            Url = newUrl
        });

    if (response.IsSuccessStatusCode)
    {
        aliases ??= new Dictionary<string, string>();
        aliases[newAlias] = newUrl;

        newAlias = "";
        newUrl = "";
        showNewAliasForm = false;
    }
}
```

Tras una respuesta satisfactoria:

- El alias se incorpora al diccionario local.
- Los campos se limpian.
- El formulario se cierra.
- El resultado aparece sin recargar la página.

La cancelación descarta los valores introducidos:

```csharp
private void CancelNewAlias()
{
    newAlias = "";
    newUrl = "";
    showNewAliasForm = false;
}
```

## 9. Edición de alias

La edición mantiene el nombre del alias y permite modificar únicamente la URL de destino.

El estado de edición utiliza:

```csharp
private string? editingAlias;
private string editUrl = "";
```

Al seleccionar `Editar`, la URL actual se carga en el campo editable:

```csharp
private void EditAlias(string alias, string url)
{
    editingAlias = alias;
    editUrl = url;
}
```

La actualización utiliza `PUT /api/aliases/{alias}`:

```csharp
private async Task SaveEdit()
{
    if (editingAlias is null)
        return;

    var http = HttpClientFactory.CreateClient("BillyClassTime");

    var response = await http.PutAsJsonAsync(
        $"/api/aliases/{Uri.EscapeDataString(editingAlias)}",
        new
        {
            Url = editUrl
        });

    if (response.IsSuccessStatusCode)
    {
        if (aliases is not null)
        {
            aliases[editingAlias] = editUrl;
        }

        editingAlias = null;
        editUrl = "";
    }
}
```

La cancelación finaliza el modo de edición sin modificar el alias:

```csharp
private void CancelEdit()
{
    editingAlias = null;
    editUrl = "";
}
```

El campo de edición utiliza `autofocus` para situar el foco en la URL.

## 10. Eliminación de alias

La eliminación requiere confirmación dentro del bloque correspondiente al alias seleccionado.

El estado de confirmación utiliza:

```csharp
private string? aliasToDelete;
```

La selección y cancelación se gestionan mediante:

```csharp
private void ConfirmDelete(string alias)
{
    aliasToDelete = alias;
}

private void CancelDelete()
{
    aliasToDelete = null;
}
```

La eliminación utiliza `DELETE /api/aliases/{alias}`:

```csharp
private async Task DeleteAlias()
{
    if (aliasToDelete is null)
        return;

    var http = HttpClientFactory.CreateClient("BillyClassTime");
    var alias = aliasToDelete;

    var response = await http.DeleteAsync(
        $"/api/aliases/{Uri.EscapeDataString(alias)}");

    if (response.IsSuccessStatusCode)
    {
        aliases?.Remove(alias);
        aliasToDelete = null;
    }
}
```

Tras una respuesta satisfactoria, el alias se elimina del diccionario local y desaparece de la interfaz sin recargar la página.

## 11. Control del estado de la interfaz

La página permite una única operación activa. El estado se centraliza mediante:

```csharp
private bool IsEditing =>
    showNewAliasForm ||
    editingAlias is not null ||
    aliasToDelete is not null;
```

Mientras existe una operación activa:

- Se oculta el botón `Nuevo alias`.
- Se oculta el buscador.
- Se ocultan las acciones `Editar` y `Eliminar` del resto de los alias.
- Permanecen visibles únicamente los controles de la operación actual.

El texto de búsqueda no se elimina al iniciar una operación. Los elementos visibles permanecen dentro del filtro activo hasta completar o cancelar la operación.

Este comportamiento evita operaciones simultáneas y mantiene estable el conjunto de resultados mostrado.

## 12. Comportamiento responsive

El listado utiliza la cuadrícula de Bootstrap:

```text
col-12 col-md-3  -> alias
col-12 col-md-7  -> destino
col-12 col-md-2  -> acciones
```

En pantallas medianas y grandes, los datos se distribuyen horizontalmente. En pantallas pequeñas, los elementos se apilan verticalmente.

La clase `text-break` permite mostrar URLs extensas sin ampliar el ancho de la página.

## 13. Validación funcional

El frontend fue validado mediante un recorrido CRUD completo:

1. Carga inicial de alias.
2. Búsqueda y actualización del contador.
3. Creación y persistencia de un alias.
4. Cancelación de una creación.
5. Edición y persistencia de un destino.
6. Cancelación de una edición.
7. Inicio y cancelación de una eliminación.
8. Confirmación de una eliminación.
9. Recarga de `/admin` para verificar la persistencia en Blob Storage.

El recorrido se completó correctamente.

## 14. Estado resultante

La página `/admin` dispone de un frontend CRUD funcional para `BillyClassTime Alias Manager`:

- La API y la interfaz utilizan el mismo contrato de datos.
- Las operaciones actualizan Azure Blob Storage.
- La interfaz refleja los cambios inmediatamente.
- El filtro permanece estable durante las operaciones.
- Solo puede existir una operación activa.
- La presentación se adapta a escritorio y móvil.

## 15. Mejoras posteriores

Las siguientes mejoras quedan fuera de esta implementación inicial:

- Sustituir la dirección fija `http://localhost:5258` por configuración dependiente del entorno.
- Mostrar en la interfaz los errores `400`, `404` y `409` devueltos por la API.
- Deshabilitar botones mientras una solicitud está en curso.
- Añadir indicadores de guardado, actualización y eliminación.
- Incorporar autenticación y autorización para `/admin`.
- Aplicar control de concurrencia al Blob mediante ETag.
- Separar los modelos de solicitud y respuesta en archivos propios.
