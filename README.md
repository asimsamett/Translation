# Translation Tool

Ekip içi web uygulaması: Azure DevOps üzerindeki UI repo’sundaki tüm `*.resx` dosyalarını (tr, en, varsayılan vb.) listeler, düzenler, commit/push eder ve pull request oluşturur.

## Mimari

| Katman | Teknoloji |
|--------|-----------|
| Backend | ASP.NET Core 8 (`Translation.Api`) |
| Core | LibGit2Sharp, Azure DevOps REST API (`Translation.Core`) |
| Frontend | React + Vite (`web/translation-ui`) |
| Kimlik | Microsoft Entra ID (kurumsal SSO) |
| Git / PR | Azure DevOps PAT (sunucu tarafı) |

**Not:** SSO kullanıcıların uygulamaya girmesini sağlar. Git clone/push ve PR işlemleri için sunucuda yapılandırılmış bir **Azure DevOps PAT** kullanılır (Key Vault / User Secrets önerilir).

## Hızlı başlangıç

### 1. Backend yapılandırması

`src/Translation.Api/appsettings.Development.json` veya User Secrets:

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
    "ResxSearchPattern": "*.resx"
  }
}
```

`AzureAd:TenantId` boş bırakılırsa geliştirme modunda otomatik kimlik doğrulama (Dev) kullanılır.

```bash
cd src/Translation.Api
dotnet user-secrets init
dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "<pat>"
dotnet run
```

Uygulama arayüzü: `https://localhost:7297` (Swagger yalnızca `/swagger`)

### 2. Entra ID (SSO)

1. **API uygulaması** kaydı: `Translation.Api` — scope `access_as_user`
2. **SPA uygulaması** kaydı: `translation-ui` — redirect `http://localhost:5173`
3. SPA’ya API scope izni verin
4. Frontend `.env`:

```env
VITE_AZURE_AD_TENANT_ID=...
VITE_AZURE_AD_CLIENT_ID=<spa-client-id>
VITE_AZURE_AD_API_SCOPE=api://<api-client-id>/access_as_user
```

### 3. Frontend

**Üretim / tek komut (API ile birlikte):**

```bash
cd web/translation-ui
npm install
npm run build
cd ../../src/Translation.Api
dotnet run
```

Tarayıcı doğrudan çeviri arayüzünü açar (`launchUrl` boş, Swagger değil).

**Geliştirme (hot reload):**

```bash
# Terminal 1
cd src/Translation.Api && dotnet run

# Terminal 2
cd web/translation-ui && npm run dev
```

UI: `http://localhost:5173` — API proxy: `https://localhost:7297`

## API özeti

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/api/resx` | Repodaki tüm `*.resx` dosyalarını listeler |
| GET | `/api/resx/{path}` | Dosya içeriği |
| PUT | `/api/resx/{path}` | Güncelleme |
| POST | `/api/git/pull` | `git pull` |
| POST | `/api/git/commit-push` | Commit + push |
| POST | `/api/pull-requests` | Azure DevOps PR |
| GET | `/api/me` | Oturum bilgisi |

## Üretim notları

- PAT’i ortam değişkeni veya Key Vault’tan okuyun; repoya commit etmeyin
- `Workspace:RootPath` için kalıcı disk ve yedekleme planlayın
- IIS / Azure App Service + aynı Entra tenant SSO
- CORS `Cors:Origins` ile UI origin’inizi ekleyin
