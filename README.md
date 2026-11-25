# BillyClassTime Alias

A lightweight URL alias and redirect service built with .NET 10 and Azure Container Apps.

The service provides short, memorable URLs such as:

`https://alias.billyclasstime.com/batterysc200-A-11-2026`

Each alias is resolved dynamically from a JSON file stored in Azure Blob Storage and redirected to its destination URL.

The idea is similar to Microsoft's `aka.ms`, but using your own domain.

## Architecture



No database is required. Since `aliases.json` is read dynamically, aliases can be added or changed without rebuilding the application or redeploying the container.

## Prerequisites

- .NET 10 SDK
- Docker Desktop
- Docker Hub account
- Azure subscription
- Azure CLI
- Azure Blob Storage
- A custom domain with DNS access

```powershell
dotnet --info
docker info
az version
```

# Step 1 - Create the .NET Minimal API

```powershell
mkdir aliasbillyclasstime
cd aliasbillyclasstime
dotnet new web
dotnet build
dotnet run
```

In this project the generated assembly is `aliasbillyclasstime.dll`. This is important for the Dockerfile.

# Step 2 - Create the redirect service

Replace `Program.cs` with:

```csharp
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{alias}", async (string alias) =>
{
    using var http = new HttpClient();

    var json = await http.GetStringAsync(
        "https://billyclasstimesa.blob.core.windows.net/scriptss/aliases.json");

    var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

    if (aliases != null && aliases.TryGetValue(alias, out var url))
        return Results.Redirect(url);

    return Results.NotFound("Alias not found");
});

app.Run();
```

The current implementation is case-sensitive.

# Step 3 - Create aliases.json in Azure Blob Storage

Example:

```json
{
  "batterysc200-A-11-2026": "https://example.blob.core.windows.net/files/battery.pdf"
}
```

Upload it to Azure Blob Storage and ensure the application can read it. Do not store secrets, passwords, API keys, or reusable SAS tokens in a publicly readable JSON file.

# Step 4 - Test locally

```powershell
dotnet run
```

Example:

`https://localhost:7278/batterysc200-A-11-2026`

The browser should redirect to the destination stored in `aliases.json`.

# Step 5 - Containerize the application

Create `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "aliasbillyclasstime.dll"]
```

Create `.dockerignore`:

```text
bin/
obj/
.vs/
.vscode/
```

Make sure the DLL in `ENTRYPOINT` matches the assembly produced by `dotnet build`.

# Step 6 - Build and test the Docker image

```powershell
$DockerUser = "billyclasstime"
$Image = "$DockerUser/billyclasstime-alias:1.0"
docker build -t $Image .
docker images
docker run --rm -p 8080:8080 $Image
```

Test:

`http://localhost:8080/batterysc200-A-11-2026`

After changing the Dockerfile, use a new version:

```powershell
docker build --no-cache -t billyclasstime/billyclasstime-alias:1.1 .
docker run --rm -p 8080:8080 billyclasstime/billyclasstime-alias:1.1
```

# Step 7 - Publish to Docker Hub

Create a public Docker Hub repository named `billyclasstime-alias`.

Suggested description:

> Lightweight .NET URL redirect service powered by aliases stored in Azure Blob Storage.

```powershell
docker login
docker push billyclasstime/billyclasstime-alias:1.1
```

# Step 8 - Prepare Azure Container Apps

Authenticate and select the subscription:

```powershell
az login
az account list -o table
az account set --subscription "<SUBSCRIPTION-ID>"
az account show --query "{Name:name, Subscription:id}" -o table
```

For a specific tenant or device-code MFA flow:

```powershell
az login --tenant "<TENANT-ID>"
az login --tenant "<TENANT-ID>" --use-device-code
```

Prepare Container Apps:

```powershell
az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App

$ResourceGroup = "rg-billyclasstime-alias"
$Location = "westeurope"
$Environment = "cae-billyclasstime-alias"
$AppName = "ca-billyclasstime-alias"

az group create `
  --name $ResourceGroup `
  --location $Location

az containerapp env create `
  --name $Environment `
  --resource-group $ResourceGroup `
  --location $Location `
  --logs-destination none
```

Check provisioning:

```powershell
az containerapp env show `
  --name $Environment `
  --resource-group $ResourceGroup `
  --query "{state:properties.provisioningState,location:location}" `
  -o table
```

Wait for `Succeeded`.

# Step 9 - Deploy the Container App

```powershell
az containerapp create `
  --name $AppName `
  --resource-group $ResourceGroup `
  --environment $Environment `
  --image billyclasstime/billyclasstime-alias:1.1 `
  --ingress external `
  --target-port 8080 `
  --min-replicas 0 `
  --max-replicas 1 `
  --cpu 0.25 `
  --memory 0.5Gi
```

Get the generated hostname:

```powershell
$Fqdn = az containerapp show `
  --name $AppName `
  --resource-group $ResourceGroup `
  --query properties.configuration.ingress.fqdn `
  --output tsv

$Fqdn
Start-Process "https://$Fqdn/batterysc200-A-11-2026"
```

To avoid cold starts, keep one replica available:

```powershell
az containerapp update `
  --name $AppName `
  --resource-group $ResourceGroup `
  --min-replicas 1 `
  --max-replicas 1
```

# Step 10 - Configure DNS

For `alias.billyclasstime.com`, create a CNAME:

| Type | Host | Value |
|---|---|---|
| CNAME | alias | `ca-billyclasstime-alias.wonderfulhill-e94bab68.westeurope.azurecontainerapps.io` |

Verify:

```powershell
nslookup alias.billyclasstime.com
```

In Azure Container Apps, go to **Networking > Custom domains > Add custom domain** and enter `alias.billyclasstime.com`.

Azure also requests a TXT verification record. For this host, create:

| Type | Host | Value |
|---|---|---|
| TXT | asuid.alias | Azure Custom Domain Verification ID shown in the Azure portal |

Do not literally create `asuid.{subdomain}`. For the `alias` host, use `asuid.alias`.

# Step 11 - Enable HTTPS

When adding the domain, choose **Managed certificate**.

The certificate can initially show `Pending`. Check it with:

```powershell
az containerapp env certificate list `
  --name $Environment `
  --resource-group $ResourceGroup `
  -o table
```

Once the certificate is issued and bound, the final URL is:

`https://alias.billyclasstime.com/batterysc200-A-11-2026`

## Adding a new alias

No application rebuild or redeployment is required. Upload the destination file and update `aliases.json`:

```json
{
  "batterysc200-A-11-2026": "https://example.blob.core.windows.net/files/sc200.pdf",
  "az104-01-2027": "https://example.blob.core.windows.net/files/az104.pdf",
  "sc200-lp9": "https://example.blob.core.windows.net/files/lp9.pdf"
}
```

Then use:

`https://alias.billyclasstime.com/az104-01-2027`

## Updating the application

Code changes require a new Docker image version:

```powershell
docker build --no-cache -t billyclasstime/billyclasstime-alias:1.2 .
docker run --rm -p 8080:8080 billyclasstime/billyclasstime-alias:1.2
docker push billyclasstime/billyclasstime-alias:1.2

az containerapp update `
  --name $AppName `
  --resource-group $ResourceGroup `
  --image billyclasstime/billyclasstime-alias:1.2
```

## Troubleshooting

Container logs:

```powershell
az containerapp logs show `
  --name $AppName `
  --resource-group $ResourceGroup `
  --type console `
  --tail 50
```

System logs:

```powershell
az containerapp logs show `
  --name $AppName `
  --resource-group $ResourceGroup `
  --type system `
  --tail 50
```

If you see `The application 'Alias.dll' does not exist`, verify the DLL name produced by `dotnet build` and correct the Dockerfile `ENTRYPOINT`.

## Repository structure

```text
aliasbillyclasstime/
|-- .dockerignore
|-- Dockerfile
|-- Program.cs
|-- aliasbillyclasstime.csproj
`-- README.md
```

`aliases.json` intentionally lives in Azure Blob Storage so redirects can be changed without redeploying the application.

## Publish to GitHub

```powershell
git init
git status
git add .
git commit -m "Initial release of BillyClassTime Alias"
git branch -M main
git remote add origin https://github.com/<YOUR-GITHUB-USER>/aliasbillyclasstime.git
git push -u origin main
```

## License

For a small public sample project intended for reuse, the MIT License is a common choice.
