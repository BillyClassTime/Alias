# Refactorización y separación de responsabilidades

## BillyClassTime Alias Manager

## 1. Objetivo

La aplicación BillyClassTime Alias Manager adopta una estructura modular para separar configuración, API HTTP, acceso a datos, contratos y componentes de interfaz.

La organización resultante aproxima el proyecto a los principios SOLID sin incorporar capas adicionales que no sean necesarias para el alcance actual.

## 2. Estructura resultante

```text
aliasbillyclasstime/
|
|-- Components/
|   |-- Admin/
|   |   |-- AliasCreate.razor
|   |   |-- AliasList.razor
|   |   `-- AliasSearch.razor
|   |
|   `-- Pages/
|       |-- Admin.razor
|       `-- Admin.razor.cs
|
|-- Endpoints/
|   `-- AliasEndpoints.cs
|
|-- Models/
|   |-- CreateAliasRequest.cs
|   `-- UpdateAliasRequest.cs
|
|-- Services/
|   `-- AliasService.cs
|
|-- Program.cs
|-- appsettings.json
`-- appsettings.Development.json
```

## 3. Program.cs

`Program.cs` queda limitado al arranque y configuración general de la aplicación.

Responsabilidades principales:

- Registro de Blazor Web App e Interactive Server.
- Registro de `HttpClient`.
- Configuración de OpenAPI y Scalar en desarrollo.
- Selección de credenciales de Azure según el entorno.
- Registro de `BlobClient`.
- Registro de `AliasService`.
- Configuración del middleware.
- Registro de los endpoints mediante `MapAliasEndpoints()`.
- Registro de los componentes Razor.

La creación de `BlobClient` se integra en el contenedor de dependencias:

```csharp
builder.Services.AddSingleton(
    new BlobClient(new Uri(aliasesUrl), credential));

builder.Services.AddSingleton<AliasService>();
```

La API se registra mediante una única llamada:

```csharp
app.MapAliasEndpoints();
```

`Program.cs` deja de contener la implementación del CRUD y la manipulación directa del fichero JSON.

## 4. Models

Los contratos utilizados por la API se definen independientemente de los endpoints.

### CreateAliasRequest.cs

```csharp
namespace aliasbillyclasstime.Models;

public record CreateAliasRequest(string Alias, string Url);
```

### UpdateAliasRequest.cs

```csharp
namespace aliasbillyclasstime.Models;

public record UpdateAliasRequest(string Url);
```

Los modelos representan exclusivamente los datos recibidos por las operaciones POST y PUT.

## 5. AliasService.cs

`AliasService` concentra el acceso y persistencia de los alias en Azure Blob Storage.

El servicio recibe `BlobClient` mediante inyección de dependencias:

```csharp
public AliasService(BlobClient blobClient)
{
    _blobClient = blobClient;
}
```

Responsabilidades:

| Método | Responsabilidad |
|---|---|
| `GetAllAsync()` | Obtener todos los alias |
| `GetAsync()` | Obtener un alias por nombre |
| `ExistsAsync()` | Comprobar la existencia de un alias |
| `CreateAsync()` | Crear un alias |
| `UpdateAsync()` | Actualizar el destino |
| `DeleteAsync()` | Eliminar un alias |
| `SaveAsync()` | Serializar y guardar `aliases.json` |

La comparación de nombres se realiza sin distinguir mayúsculas y minúsculas mediante `StringComparison.OrdinalIgnoreCase`.

La serialización conserva el JSON indentado:

```csharp
var json = JsonSerializer.Serialize(
    aliases,
    new JsonSerializerOptions
    {
        WriteIndented = true
    });
```

El acceso a Blob Storage queda desacoplado de la capa HTTP.

## 6. AliasEndpoints.cs

`AliasEndpoints` contiene la definición de la Minimal API.

Los endpoints disponibles son:

```text
GET    /api/aliases
GET    /api/aliases/{alias}
POST   /api/aliases
PUT    /api/aliases/{alias}
DELETE /api/aliases/{alias}
GET    /{alias}
```

Cada endpoint recibe `AliasService` mediante inyección de dependencias.

Las responsabilidades HTTP permanecen en esta capa:

- Lectura de parámetros y cuerpos de solicitud.
- Validación de campos obligatorios.
- Validación de URL HTTP/HTTPS.
- Selección del código de estado HTTP.
- Construcción de respuestas JSON.
- Redirección de los alias públicos.

La persistencia se delega a `AliasService`.

La validación de URL se centraliza en:

```csharp
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
```

## 7. Admin.razor

`Admin.razor` actúa como componente de composición de la página `/admin`.

Contiene:

- Título y descripción.
- Botón `Nuevo alias`.
- Inclusión condicional de `AliasCreate`.
- Inclusión condicional de `AliasSearch`.
- Inclusión de `AliasList`.
- Enlace de propiedades y eventos con el code-behind.

La lógica de aplicación queda fuera del marcado Razor.

La composición principal utiliza componentes independientes:

```razor
<AliasCreate ... />
<AliasSearch ... />
<AliasList ... />
```

## 8. Admin.razor.cs

`Admin.razor.cs` contiene el estado y la coordinación del frontend.

Responsabilidades:

- Carga inicial de alias.
- Estado del formulario de creación.
- Estado de edición.
- Estado de confirmación de eliminación.
- Texto de búsqueda.
- Filtrado de alias.
- Llamadas HTTP al backend.
- Actualización del estado local después de operaciones satisfactorias.
- Gestión de mensajes de error.
- Coordinación de eventos procedentes de los componentes visuales.

La página mantiene una única operación activa mediante:

```csharp
private bool IsEditing =>
    showNewAliasForm ||
    editingAlias is not null ||
    aliasToDelete is not null;
```

Los errores HTTP se leen desde la respuesta de la API y se trasladan a la interfaz.

## 9. AliasCreate.razor

`AliasCreate` representa exclusivamente el formulario de creación.

El componente recibe:

- Alias.
- URL.
- Mensaje de error.
- Eventos de guardado y cancelación.

La comunicación con `Admin` utiliza parámetros y `EventCallback`.

El foco se establece sobre el campo Alias después del renderizado mediante `ElementReference` y `FocusAsync()`.

El componente no realiza llamadas HTTP ni accede directamente al almacenamiento.

## 10. AliasSearch.razor

`AliasSearch` encapsula el campo de búsqueda.

El componente recibe el texto actual y notifica las modificaciones mediante `EventCallback<string>`.

La actualización se realiza durante la escritura del usuario.

El foco inicial se establece sobre el buscador mediante `ElementReference` y `FocusAsync()`.

El filtrado efectivo permanece en `Admin.razor.cs`.

## 11. AliasList.razor

`AliasList` representa el conjunto de resultados y los controles asociados a cada alias.

Responsabilidades visuales:

- Mostrar el número de resultados.
- Mostrar alias y destinos.
- Activar el modo de edición de un elemento.
- Mostrar `Guardar` y `Cancelar` durante una edición.
- Mostrar la confirmación de eliminación.
- Mostrar errores de actualización o eliminación.
- Ocultar acciones de otros elementos mientras existe una operación activa.

La selección de acciones se determina mediante `EditingAlias`, `AliasToDelete` e `IsOperationActive`.

`IsOperationActive` se recibe desde `Admin.razor` y representa cualquier operación activa de creación, edición o eliminación. Cuando su valor es `true`, las acciones `Editar` y `Eliminar` de los elementos quedan ocultas. Los controles específicos de EDIT y DELETE permanecen asociados al elemento seleccionado.

El componente no ejecuta directamente POST, PUT o DELETE. Las acciones se notifican al componente padre mediante `EventCallback`.

El foco del campo URL durante la edición se aplica con `ElementReference` y `FocusAsync()` después del renderizado.

## 12. Comportamiento responsive

El listado utiliza la cuadrícula responsive de Bootstrap 5.3.9:

```text
col-12 col-md-3
col-12 col-md-7
col-12 col-md-2
```

En dispositivos móviles, alias, destino y acciones se distribuyen verticalmente.

Las URLs extensas se contienen mediante:

```css
min-width: 0;
overflow-wrap: anywhere;
```

Los botones admiten reorganización mediante `flex-wrap`.

La presentación se mantiene funcional en anchos equivalentes a dispositivos móviles de aproximadamente 375 y 412 píxeles.

## 13. Gestión de estado de interfaz

La interfaz mantiene un único flujo de modificación activo.

En estado normal se encuentran disponibles:

- `Nuevo alias`.
- Búsqueda.
- `Editar`.
- `Eliminar`.

Durante NEW:

- Se muestra el formulario de creación.
- Se oculta el buscador.
- `IsOperationActive` se establece a partir del estado coordinado por `Admin.razor.cs`.
- Se ocultan `Editar` y `Eliminar` en todos los alias existentes.
- Permanecen visibles únicamente `Guardar` y `Cancelar` del formulario de creación.

Durante EDIT:

- Se conserva el conjunto actualmente filtrado.
- El elemento seleccionado muestra el campo URL editable.
- Se muestran `Guardar` y `Cancelar` en el elemento seleccionado.
- Las acciones del resto de elementos permanecen ocultas.

Durante DELETE:

- Se mantiene el conjunto actualmente filtrado.
- Se muestra la confirmación en el elemento seleccionado.
- Las acciones del resto de elementos permanecen ocultas.

## 14. Tratamiento de errores

La interfaz muestra los mensajes devueltos por la API cuando una operación no finaliza correctamente.

Entre los casos gestionados se incluyen:

- Alias y URL obligatorios.
- URL no válida.
- Alias duplicado.
- Alias no encontrado.
- Error genérico de creación.
- Error genérico de actualización.
- Error genérico de eliminación.

La respuesta HTTP se procesa mediante un método común en `Admin.razor.cs`.

## 15. Aproximación a SOLID

### Responsabilidad única

Las principales responsabilidades quedan repartidas entre unidades específicas:

```text
Program.cs          -> configuración y arranque
AliasEndpoints.cs   -> capa HTTP
AliasService.cs     -> persistencia
Models              -> contratos
Admin.razor         -> composición
Admin.razor.cs      -> estado y coordinación
AliasCreate.razor   -> interfaz de creación
AliasSearch.razor   -> interfaz de búsqueda
AliasList.razor     -> listado y acciones visuales
```

### Abierto/cerrado

La separación permite incorporar nuevas operaciones o componentes reduciendo modificaciones en archivos no relacionados.

Nuevos endpoints pueden añadirse en `AliasEndpoints` y nuevas operaciones de almacenamiento pueden añadirse en `AliasService` sin ampliar `Program.cs`.

### Sustitución de Liskov

La implementación actual no utiliza jerarquías de herencia para el dominio de alias. No se introduce una abstracción artificial para este principio.

### Segregación de interfaces

No se incorporan interfaces adicionales debido al tamaño actual del servicio. Cada componente Razor recibe únicamente los parámetros y eventos necesarios para su función.

### Inversión de dependencias

`BlobClient` y `AliasService` se obtienen mediante el contenedor de dependencias de ASP.NET Core.

Los endpoints no construyen directamente las dependencias de almacenamiento.

Los componentes visuales no dependen de `BlobClient`, Azure Storage ni de la implementación de la Minimal API.

## 16. Flujo resultante

El flujo general queda definido de la siguiente forma:

```text
Admin.razor
    |
    +-- AliasCreate.razor
    +-- AliasSearch.razor
    `-- AliasList.razor
            |
            v
     Admin.razor.cs
            |
            v
       Minimal API
            |
            v
   AliasEndpoints.cs
            |
            v
    AliasService.cs
            |
            v
       BlobClient
            |
            v
 jsons/aliases.json
```

La redirección pública mantiene un flujo directo:

```text
/{alias}
    |
    v
AliasEndpoints
    |
    v
AliasService
    |
    v
URL de destino
```

## 17. Estado de la implementación

La estructura resultante mantiene el CRUD completo de alias con:

- Consulta.
- Búsqueda en tiempo real.
- Creación.
- Edición.
- Eliminación con confirmación.
- Presentación de errores.
- Actualización de interfaz sin recarga.
- Gestión de foco.
- Diseño responsive.
- Persistencia en Azure Blob Storage.
- Acceso a Blob Storage mediante identidad administrada en Azure.

La implementación conserva una arquitectura deliberadamente sencilla y separa las responsabilidades necesarias para el alcance actual de BillyClassTime Alias Manager.
