# Secured Alias Manager

## BillyClassTime Alias Manager

## 1. Objetivo

BillyClassTime Alias Manager incorpora autenticación con Microsoft Entra ID para proteger la interfaz administrativa sin modificar el comportamiento público de los alias.

La configuración mantiene dos superficies diferenciadas:

```text
/{alias}       -> acceso público
/admin         -> acceso autenticado
```

La aplicación continúa utilizando Managed Identity para acceder al Blob privado que contiene `jsons/aliases.json`.

La autenticación del usuario y la identidad administrada de la aplicación constituyen mecanismos independientes.

## 2. Arquitectura

```text
Internet
   |
   v
alias.billyclasstime.com
   |
   +-- /{alias}
   |      |
   |      `-- acceso público
   |
   `-- /admin
          |
          `-- Microsoft Entra ID
                 |
                 v
        BillyClassTime Alias Manager
                 |
                 v
            Minimal API
                 |
                 v
           AliasService
                 |
                 v
         Managed Identity
                 |
                 v
        Azure Blob Storage
        jsons/aliases.json
```

## 3. Azure Container Apps

La aplicación se ejecuta en Azure Container Apps.

La autenticación integrada de Container Apps se configura con Microsoft como proveedor de identidad.

La configuración utiliza:

- Proveedor: Microsoft.
- Tipo de directorio: Workforce.
- Registro de aplicación dedicado al Alias Manager.
- Tipo de cuenta: Single tenant.
- Acceso general de Container Apps: `Allow unauthenticated access`.

El acceso anónimo se mantiene habilitado a nivel de Container Apps porque los endpoints `/{alias}` deben permanecer públicos.

La decisión de exigir autenticación para `/admin` se realiza dentro de la aplicación.

## 4. Microsoft Entra ID

Se utiliza un App Registration específico para BillyClassTime Alias Manager.

El registro dispone de una Redirect URI de tipo Web:

```text
https://alias.billyclasstime.com/.auth/login/aad/callback
```

La URI corresponde al callback utilizado por la autenticación integrada de Azure Container Apps para Microsoft Entra ID.

El endpoint de inicio de sesión utilizado por la aplicación es:

```text
/.auth/login/aad
```

El proveedor de identidad se configura para el tenant actual.

## 5. Acceso público a alias

Las rutas públicas conservan el comportamiento de redirección original:

```text
https://alias.billyclasstime.com/Az-104
https://alias.billyclasstime.com/SC-900
https://alias.billyclasstime.com/{alias}
```

Estas solicitudes no requieren autenticación.

El flujo público es:

```text
GET /{alias}
    |
    v
AliasEndpoints
    |
    v
AliasService.GetAsync()
    |
    v
URL de destino
    |
    v
HTTP Redirect
```

Un alias inexistente mantiene la respuesta `404`.

## 6. Protección de /admin

La ruta administrativa se protege mediante un middleware propio.

Archivo:

```text
Middleware/EntraAdminMiddleware.cs
```

El middleware comprueba la identidad proporcionada por la autenticación integrada de Azure Container Apps.

La comprobación se realiza sobre la cabecera:

```text
X-MS-CLIENT-PRINCIPAL
```

Cuando `/admin` recibe una solicitud sin identidad autenticada, la aplicación redirige al endpoint de autenticación de Microsoft:

```text
/.auth/login/aad
```

Después de una autenticación correcta, Microsoft Entra ID devuelve la solicitud al Alias Manager.

## 7. Middleware de administración

La implementación base del middleware es:

```csharp
namespace aliasbillyclasstime.Middleware;

public class EntraAdminMiddleware
{
    private readonly RequestDelegate _next;

    public EntraAdminMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            var principal =
                context.Request.Headers["X-MS-CLIENT-PRINCIPAL"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(principal))
            {
                var returnUrl = Uri.EscapeDataString(
                    context.Request.Path +
                    context.Request.QueryString);

                context.Response.Redirect(
                    $"/.auth/login/aad?post_login_redirect_uri={returnUrl}");

                return;
            }
        }

        await _next(context);
    }
}
```

La finalidad del middleware es limitar la protección a la superficie administrativa y mantener las redirecciones públicas fuera del flujo de autenticación.

## 8. Registro del middleware

`Program.cs` referencia el namespace:

```csharp
using aliasbillyclasstime.Middleware;
```

El middleware se habilita únicamente fuera del entorno Development:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseMiddleware<EntraAdminMiddleware>();
}
```

La configuración permite mantener el desarrollo local sin dependencia de Microsoft Entra ID.

## 9. Desarrollo local

En Development, `/admin` permanece accesible directamente:

```text
http://localhost:5258/admin
```

La autenticación integrada de Azure Container Apps no forma parte del entorno local.

El middleware de Entra no se registra cuando:

```csharp
app.Environment.IsDevelopment()
```

El CRUD puede probarse localmente sin iniciar sesión.

## 10. Configuración de HttpClient por entorno

El frontend Blazor utiliza un HttpClient con nombre:

```text
BillyClassTime
```

`Program.cs` obtiene la dirección base desde configuración:

```csharp
builder.Services.AddHttpClient("BillyClassTime", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["BillyClassTime:BaseAddress"]
        ?? throw new InvalidOperationException(
            "No se ha configurado 'BillyClassTime:BaseAddress'."));
});
```

### Development

`appsettings.Development.json` contiene:

```json
{
  "BillyClassTime": {
    "BaseAddress": "http://localhost:5258"
  }
}
```

`localhost:5258` queda limitado al entorno Development.

### Producción

Azure Container Apps define la variable de entorno:

```text
BillyClassTime__BaseAddress=https://alias.billyclasstime.com
```

La doble barra baja representa el separador `:` de configuración de .NET.

La configuración equivalente es:

```text
BillyClassTime:BaseAddress=https://alias.billyclasstime.com
```

## 11. Persistencia segura en Azure Blob Storage

La aplicación utiliza un Blob privado para almacenar los alias:

```text
jsons/aliases.json
```

El acceso de producción no utiliza una clave de cuenta ni una cadena de conexión dentro del código.

La aplicación utiliza System Assigned Managed Identity.

La selección de credenciales depende del entorno:

```csharp
TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new ManagedIdentityCredential(
        ManagedIdentityId.SystemAssigned);
```

El comportamiento es:

```text
Development -> AzureCliCredential
Production  -> ManagedIdentityCredential
```

El `BlobClient` se registra mediante inyección de dependencias:

```csharp
builder.Services.AddSingleton(
    new BlobClient(new Uri(aliasesUrl), credential));
```

`AliasService` recibe el `BlobClient` desde el contenedor de dependencias.

## 12. Separación de responsabilidades de seguridad

La implementación utiliza dos identidades independientes.

### Identidad del usuario

Microsoft Entra ID autentica a la persona que solicita `/admin`.

```text
Usuario
   |
   v
Microsoft Entra ID
   |
   v
/admin
```

### Identidad de la aplicación

Managed Identity autoriza al Container App para acceder al Blob privado.

```text
BillyClassTime Alias
   |
   v
Managed Identity
   |
   v
Azure Blob Storage
```

La identidad del usuario no se utiliza para acceder directamente a Blob Storage.

## 13. Despliegue

La versión con la protección inicial de `/admin` se publica como imagen Docker y se despliega mediante una nueva revisión de Azure Container Apps.

El flujo de despliegue es:

```text
dotnet build
    |
    v
docker build
    |
    v
docker push
    |
    v
Azure Container Apps
    |
    v
Nueva revisión
```

La configuración específica de producción permanece en Azure Container Apps mediante variables de entorno.

## 14. Validación funcional

La configuración se valida utilizando navegador de escritorio y dispositivo móvil.

### Ruta pública

```text
https://alias.billyclasstime.com/{alias}
```

Resultado esperado:

- No solicita autenticación.
- Resuelve el alias.
- Ejecuta la redirección.

### Administración

```text
https://alias.billyclasstime.com/admin
```

Resultado esperado sin sesión:

```text
/admin
   |
   v
/.auth/login/aad
   |
   v
Microsoft Entra ID
   |
   v
/admin
```

Resultado esperado con sesión válida:

- Se carga BillyClassTime Alias Manager.
- Se mantiene Blazor Interactive Server.
- Se mantiene el CRUD.
- Se mantiene el diseño responsive.

## 15. Estado de la API administrativa

La API CRUD utiliza las rutas:

```text
GET    /api/aliases
GET    /api/aliases/{alias}
POST   /api/aliases
PUT    /api/aliases/{alias}
DELETE /api/aliases/{alias}
```

La protección específica de `/api/aliases` debe mantenerse alineada con la protección de `/admin`.

La configuración final debe impedir que un cliente no autenticado pueda omitir la interfaz administrativa y ejecutar directamente operaciones de creación, modificación o eliminación.

La ruta pública `/{alias}` debe permanecer excluida de esta protección.

## 16. Comportamiento objetivo

```text
Ruta                         Acceso
-------------------------------------------------
/{alias}                     Público
/admin                       Microsoft Entra ID
/api/aliases                 Microsoft Entra ID
/api/aliases/{alias}         Microsoft Entra ID
Blob jsons/aliases.json      Managed Identity
```

Este modelo mantiene separadas la navegación pública, la administración autenticada y la autorización de la aplicación sobre Azure Storage.

## 17. Mejora futura: cierre de sesión

El cierre de sesión explícito en la interfaz se mantiene como mejora posterior.

Azure Container Apps dispone de los endpoints de autenticación integrada necesarios para incorporar un control de logout sin implementar un flujo OAuth propio dentro de Blazor.

La incorporación de un botón de cierre de sesión no forma parte de la implementación actual.

## 18. Estado resultante

BillyClassTime Alias Manager dispone de una base de seguridad compuesta por:

- Microsoft Entra ID como proveedor de identidad administrativo.
- Autenticación integrada de Azure Container Apps.
- Registro de aplicación Single tenant.
- Callback Web asociado al dominio `alias.billyclasstime.com`.
- Acceso público conservado para `/{alias}`.
- Protección selectiva de `/admin` desde la aplicación.
- Desarrollo local sin dependencia de Entra ID.
- Configuración de BaseAddress separada por entorno.
- Azure Managed Identity para acceder a Blob Storage.
- Blob `jsons/aliases.json` privado.
- Separación entre identidad de usuario e identidad de aplicación.
- Validación funcional desde escritorio y dispositivo móvil.

La protección de la API CRUD forma parte del cierre de seguridad necesario para impedir el acceso administrativo directo fuera de `/admin`.
