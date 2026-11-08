# RaceDay — Part 3: MVC Web Application

> **Student:** Lehlogonolo | IIE Rosebank College
> **Module:** Programming — Portfolio of Evidence (Part 3)

---

## 🎥 Video Presentation

> **YouTube Link:** *(https://youtu.be/TfH3hNU6S9A)*

---

## 📋 System Description

**RaceDay** is a full-stack web-based event management system for the South African road running, walking, and cycling community.

### Two User Roles

| Role | Capabilities |
|------|-------------|
| **Organiser** | Create, edit, delete events; manage event categories; view all enrolments; capture finish times and results; upload event banner images |
| **Participant** | Browse and filter upcoming events; enrol in events; view personal entries with status; track personal race history; upload profile picture |

---

## 🚀 Setup Instructions

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- SQL Server (LocalDB or full instance)
- The **Part 2 API** must be running before starting the MVC app

### 1. Configure the Part 2 API

1. Open `RaceDay.API/appsettings.json` and confirm the SQL connection string
2. Add the Azure Blob Storage connection string:
   ```json
   "AzureBlob": {
     "ConnectionString": "YOUR_AZURE_STORAGE_CONNECTION_STRING",
     "ContainerName": "raceday-images"
   }
   ```
3. Copy the new files from `RaceDay.API.Extensions/` into your existing `RaceDay.API/` project
4. Add `BannerImageUrl` to `RaceDay.API/Models/RaceEvent.cs`:
   ```csharp
   [MaxLength(500)]
   public string? BannerImageUrl { get; set; }
   ```
5. Add `ProfilePictureUrl` to `RaceDay.API/Models/Participant.cs`:
   ```csharp
   [MaxLength(500)]
   public string? ProfilePictureUrl { get; set; }
   ```
6. Run database migrations in the API project:
   ```bash
   cd RaceDay.API
   dotnet ef migrations add AddBlobImageFields
   dotnet ef database update
   ```
7. Start the API:
   ```bash
   dotnet run --project RaceDay.API
   # API runs at http://localhost:5000
   ```

### 2. Configure the MVC Application

1. Open `RaceDay.MVC/appsettings.json` and confirm the API base URL:
   ```json
   "ApiSettings": { "BaseUrl": "http://localhost:5000/" }
   ```

2. Install dependencies and run:
   ```bash
   cd RaceDay.MVC
   dotnet restore
   dotnet run
   # MVC runs at http://localhost:5001
   ```

3. Open your browser at **http://localhost:5001**

---

## 🐳 Docker

### Run the MVC app in Docker

> The Part 2 API must already be running (locally or in its own container).
> Replace `https://localhost:55293/` with the actual API address reachable from the container.

```bash
docker build -t raceday-mvc ./RaceDay.MVC

docker run -d \
  -p 8080:8080 \
  -e "ApiSettings__BaseUrl=http://host.docker.internal:5000/" \
  --name raceday-mvc \
  raceday-mvc
```

Then open **http://localhost:8080**

---

## ☁️ Azure Blob Storage

Banner images and profile pictures are uploaded through the Part 2 API — the MVC app never talks to Azure directly.

To enable this feature:
1. Create an Azure Storage Account in the [Azure Portal](https://portal.azure.com)
2. Create a container named `raceday-images`
3. Copy the connection string from **Access Keys → Connection string**
4. Paste it into `RaceDay.API/appsettings.json` under `AzureBlob:ConnectionString`

---

## 📁 Repository Structure

```
/
├── docs/                        # ERD, SQL script, API endpoint plan (Part 1)
├── RaceDay.API/                 # Part 2 — RESTful API (ASP.NET Core + EF Core)
│   ├── Controllers/
│   ├── Models/
│   ├── DTOs/
│   ├── Services/                # BlobStorageService (new for Part 3)
│   └── ...
├── RaceDay.Tests/               # Part 2 — xUnit unit tests
├── RaceDay.MVC/                 # Part 3 — MVC web app
│   ├── Controllers/
│   │   ├── AccountController.cs
│   │   ├── HomeController.cs
│   │   ├── OrganiserController.cs
│   │   └── ParticipantController.cs
│   ├── Models/ViewModels.cs
│   ├── Services/RaceDayApiService.cs
│   ├── Views/
│   │   ├── Shared/_Layout.cshtml
│   │   ├── Account/
│   │   ├── Home/
│   │   ├── Organiser/
│   │   └── Participant/
│   ├── wwwroot/css/raceday.css
│   └── Dockerfile
├── .github/workflows/ci.yml     # GitHub Actions CI/CD
└── README.md
```

---

## 🔄 CI/CD Screenshot

> *(<img width="1907" height="581" alt="Screenshot 2026-11-08 114627" src="https://github.com/user-attachments/assets/5c129e6c-b5e7-49f9-b3c8-60d1eb16e6db" />
)*

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core 8 Web API, Entity Framework Core 8, SQL Server |
| MVC | ASP.NET Core 8 MVC, Razor Views |
| Auth | Session-based (server-side sessions, cookie forwarding) |
| Cloud Storage | Azure Blob Storage (via API) |
| UI | Bootstrap 5, Font Awesome |
| Containerisation | Docker (multi-stage build) |
| CI/CD | GitHub Actions |
| Testing | xUnit, EF Core In-Memory provider |

---

*© 2026 RaceDay — IIE Rosebank College Portfolio of Evidence*

<!-- README last updated: Part 3 complete -->
