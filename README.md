# Translation Tool

**English** | [Turkce](README.tr.md)

Internal web application for editing resource files in an Azure DevOps UI repository, then committing, pushing, and creating pull requests.

Supported resource files:

- `.resx`
- `.json`

JSON files support both flat key-value and nested object structures:

```json
{
  "Save": "Save",
  "Home": {
    "Title": "Home"
  }
}
```

Nested JSON values are shown in the UI as paths such as `Home.Title`. When saved, the existing nested structure is preserved.

## Architecture

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8 (`Translation.Api`) |
| Core | LibGit2Sharp, Azure DevOps REST API (`Translation.Core`) |
| Frontend | React + Vite (`web/translation-ui`) |
| Identity | Microsoft Entra ID (corporate SSO) |
| Git / PR | Azure DevOps PAT (server-side) |

**Note:** SSO authenticates users into the app. Git clone/push and PR operations use an Azure DevOps PAT configured on the server. Store the PAT in Key Vault, environment variables, or User Secrets.

## Quick Start

### 1. Backend Configuration

Use `src/Translation.Api/appsettings.Development.json` or User Secrets:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<api-app-client-id>",
    "Audience": "api://<api-app-client-id>"
  },
  "AzureDevOps": {
    "Organization": "your-org",
    "Project": "your-project",
    "Repository": "your-ui-repo",
    "DefaultBranch": "main",
    "PersonalAccessToken": "<pat-with-code-read-write-and-pr>"
  },
  "Workspace": {
    "RootPath": "C:\\translation-workspaces",
    "ResxSearchPattern": "*.resx;*.json"
  }
}
```

`Workspace:ResxSearchPattern` accepts multiple patterns separated by semicolons or commas. The default is `*.resx;*.json`, so both RESX and JSON resource files are listed.

The app excludes common non-resource JSON files and folders such as `package.json`, `package-lock.json`, `tsconfig*.json`, `node_modules`, `bin`, `obj`, `dist`, and `build`.

If `AzureAd:TenantId` is empty, the app uses automatic development authentication.

```bash
cd src/Translation.Api
dotnet user-secrets init
dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "<pat>"
dotnet run
```

App UI: `https://localhost:7297`  
Swagger: `https://localhost:7297/swagger`

### 2. Entra ID (SSO)

1. Register an **API app** named `Translation.Api` with the `access_as_user` scope.
2. Register an **SPA app** named `translation-ui` with redirect URI `http://localhost:5173`.
3. Grant the SPA permission to the API scope.
4. Configure the frontend `.env`:

```env
VITE_AZURE_AD_TENANT_ID=...
VITE_AZURE_AD_CLIENT_ID=<spa-client-id>
VITE_AZURE_AD_API_SCOPE=api://<api-client-id>/access_as_user
```

### 3. Frontend

**Production / single app hosted by API:**

```bash
cd web/translation-ui
npm install
npm run build
cd ../../src/Translation.Api
dotnet run
```

The browser opens the translation UI directly.

**Development with hot reload:**

```bash
# Terminal 1
cd src/Translation.Api && dotnet run

# Terminal 2
cd web/translation-ui && npm run dev
```

UI: `http://localhost:5173`  
API proxy: `https://localhost:7297`

## API Summary

The endpoint names remain `/api/resx` for backward compatibility, but they now support both `.resx` and `.json` resource files.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/resx` | Lists supported resource files in the repository |
| GET | `/api/resx/{path}` | Gets resource file content |
| PUT | `/api/resx/{path}` | Updates a resource file |
| POST | `/api/git/pull` | Runs `git pull` |
| GET | `/api/git/branches` | Lists branches |
| GET | `/api/git/branch-defaults` | Gets default branch values |
| POST | `/api/git/branches` | Creates or checks out a local branch |
| POST | `/api/git/commit-push` | Commits and pushes changes |
| POST | `/api/pull-requests` | Creates an Azure DevOps PR |
| GET | `/api/me` | Gets session information |

## Production Notes

- Read the PAT from environment variables or Key Vault; do not commit it to the repository.
- Use persistent storage and backups for `Workspace:RootPath`.
- When hosting on IIS or Azure App Service, verify the Entra tenant and SSO settings.
- Add the UI origin to `Cors:Origins`.
- Non-string JSON values are not editable in the UI; string values under nested objects are editable.
