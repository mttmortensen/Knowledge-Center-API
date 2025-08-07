# Knowledge Center API

The Knowledge Center API is a custom-built ASP.NET Core Web API for capturing, organizing, and retrieving knowledge across domains, nodes, logs, and tags. Inspired by GitHub's green contribution squares, it supports daily progress tracking via `LogEntries` and links all knowledge together in a structured, queryable system.

## 🚀 Features

- **JWT Auth**: Secure authentication with JWT Bearer tokens
- **Log Entries**: Create, retrieve, tag, and analyze personal knowledge logs
- **Tags**: Categorize logs with customizable tags
- **Knowledge Nodes**: Link logs to specific knowledge categories
- **Domains**: Group related knowledge nodes under shared umbrellas
- **Demo Mode**: Read-only access for frontend testing and previews
- **Rate Limiting**: Basic client-side spam protection
- **Future Ready**: Planned `/search` and `/stats` endpoints to expand analytic insights

## 📦 Tech Stack

- **Language**: C#
- **Framework**: ASP.NET Core 9
- **Database**: SQL Server
- **Auth**: JWT
- **DI**: Built-in Dependency Injection
- **Data Access**: Raw ADO.NET w/ strongly typed queries
- **Docs**: Swagger auto-generated via annotations

## 🛠️ Project Structure
Knowledge-Center-API/  
│  
├── Controllers/ # API endpoints (Log, Auth, etc.)  
├── Services/ # Core logic and validation  
├── Models/ # DTOs and data entities  
├── DataAccess/ # SQL queries and DB wrapper  
├── Middleware/ # Auth / rate limit logic  
├── wwwroot/ # Static hosting (if needed)  
└── Program.cs # App setup + DI + Swagger  

## 🧪 Sample Endpoints

- `GET /api/logs/` — Get all logs
- `GET /api/logs/{id}` — Get a single log
- `POST /api/logs/` — Create new log
- `PUT /api/logs/{id}/tags` — Add or replace tags
- `DELETE /api/kns/{nodeId}/logs` — Remove all logs tied to a Knowledge Node

> All endpoints require JWT unless in demo mode.

## 🧱 Architecture Style

Follows a **layered architecture**:
- Controller → Service → DataAccess
- DTOs separate external contract from internal data shape
- Query files keep SQL logic centralized

## ✅ Auth Flow

1. `POST /api/auth/login` → returns JWT
2. Store token in session/localStorage
3. Attach `Authorization: Bearer <token>` to all future requests

## 💡 Planned Enhancements

- Full-text `/search` endpoint across Logs, Nodes, Tags, Domains
- `/stats` endpoint to mimic GitHub-style contribution heatmaps


## 🧑‍💻 Dev Setup

```bash
 git clone https://github.com/mttmortensen/Knowledge-Center-API.git
 cd Knowledge-Center-API
 dotnet restore
 dotnet build
 dotnet run
 Swagger should be available at:
 📎 http://localhost:7035/kc/swagger/index.html
```

### MIT License — 2025 © Matt Mortensen

---