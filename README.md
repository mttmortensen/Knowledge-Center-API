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

### Learn (progressive-learning notebooks)

A session is an ordered timeline of markdown entries. Entry type and links are
parsed from the source on every read: `?? ...` is a question, `@answers(<entryId>)`
is a follow-up (which marks that question answered). Learn never creates or edits
Logs: a session's optional `NodeId` is just a label, and an entry's `LogId` only records
the log the web app made from it (cleared if that log is deleted).

- `GET /api/learn/sessions` — List sessions (with entry / open-question counts)
- `POST /api/learn/sessions` — Create a session (`Title`, optional `Topic`, `NodeId`)
- `GET /api/learn/sessions/{id}` — Get a session with its entries in timeline order
- `PUT /api/learn/sessions/{id}` — Update title / topic / Knowledge Node
- `DELETE /api/learn/sessions/{id}` — Delete a session and its entries
- `POST /api/learn/sessions/{id}/entries` — Add an entry (`Source`, optional `BeforeEntryId` to insert above an entry)
- `GET|PUT|DELETE /api/learn/entries/{id}` — Read, edit, or delete one entry
- `PUT /api/learn/entries/{id}/log` — Record the log made from an entry (`LogId`; null or 0 clears it)
- `GET /api/learn/questions/open?sessionId=` — Questions with no follow-up yet
- `GET /api/learn/sessions/{id}/export` — The session as one `.md` file

Schema: `SQL/create_learn_tables.sql`, then `SQL/add_learn_entry_log_link.sql` (both additive; back up `kc_db` before applying).

### Moving things

Moved items keep their ids, so tags, Learn links and child logs/actions come along.
The target must exist and not be archived (400 otherwise).

- `PUT /api/logs/{id}/move` — Move a log to another Knowledge Node (`NodeId`)
- `PUT /api/actions/{id}/move` — Move an action to another Knowledge Node (`KnowledgeNodeId`)
- `PUT /api/knowledge-nodes/{id}/move` — Move a Knowledge Node to another Domain (`DomainId`)

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


## 🧪 Tests

```bash
 dotnet test Tests/Knowledge-Center-API.Tests
```

Parser, export and demo-mode tests always run. The database-backed endpoint tests
need `KC_TEST_DB` set to an Npgsql connection string for a **throwaway** database
whose name contains `test` (e.g. `kc_learn_test`); they drop and rebuild its schema,
and refuse to run against any other database name.

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