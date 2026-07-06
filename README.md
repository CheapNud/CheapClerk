# CheapClerk

A C# MCP server that bridges Paperless-ngx to Claude Code, turning your scanned home documents into a queryable knowledge base.

Part of the [CheapNud](https://github.com/CheapNud) open-source ecosystem.

---

## What This Is

CheapClerk lets you ask natural language questions about your household paperwork — insurance policies, utility contracts, tax documents, receipts, warranty cards — directly from Claude Code. No GUI needed. You scan documents into Paperless-ngx, CheapClerk exposes them as MCP tools, and Claude Code does the rest.

```
You: "What's the deductible on my home insurance?"
Claude Code → search_documents("home insurance deductible")
           → get_document_content(doc_id: 47)
           → "Your deductible is €500 per claim (KBC Woonverzekering, policy dated 2024-03-12)"
```

---

## Architecture

```
Physical documents
        │
        │  scan (Paperless mobile app / flatbed scanner)
        ▼
┌─────────────────────────────┐
│  Paperless-ngx              │  Docker on Sierra-Madre (:8010)
│  • Tesseract OCR            │  Media on mirrored bfa pool (RAID10)
│  • Full-text search (FTS)   │  Database on Vault-Tec PostgreSQL
│  • REST API                 │
│  • Tagging & correspondents │
└─────────────┬───────────────┘
              │ HTTP (internal network)
              ▼
┌─────────────────────────────┐
│  CheapClerk MCP Server      │  .NET 11 / C# console app
│  • Wraps Paperless REST API │  Runs as stdio MCP server
│  • Vision OCR fallback      │  Launched by Claude Code
│  • 5 tools exposed          │
└─────────────┬───────────────┘
              │ MCP (stdio)
              ▼
┌─────────────────────────────┐
│  Claude Code                │
│  "What's my electricity     │
│   contract end date?"       │
└─────────────────────────────┘
```

---

## Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Runtime | .NET 11 | Matches broader CheapNud ecosystem |
| MCP SDK | `ModelContextProtocol` NuGet | Official .NET MCP implementation |
| HTTP Client | `HttpClient` via DI | Talking to Paperless-ngx REST API |
| Shared plumbing | `CheapHelpers` NuGet | Shared utilities from the CheapNud ecosystem |
| OCR (primary) | Tesseract via Paperless-ngx | Built into Paperless, no extra config |
| OCR (fallback) | Claude Vision API | For handwritten or low-quality scans |
| Document store | Paperless-ngx | Docker deployment, handles all storage |
| Config | `appsettings.json` + env vars | Paperless URL, API token, vision thresholds |

---

## MCP Tools

### `search_documents`
Full-text search across all ingested documents.

```csharp
[Tool("search_documents")]
async Task<string> SearchDocuments(
    string query,
    string? tag = null,
    string? correspondent = null,
    int maxResults = 10
)
```

Returns: document ID, title, matched excerpt, tags, date, correspondent.

### `get_document_content`
Retrieve the full OCR text of a specific document. Triggers vision fallback if OCR quality is poor.

```csharp
[Tool("get_document_content")]
async Task<string> GetDocumentContent(
    int documentId,
    bool forceVisionOcr = false
)
```

Returns: full text content. If `forceVisionOcr` is true or Tesseract output is below confidence threshold, fetches original scan and runs Claude Vision.

### `list_documents`
Browse documents with filters. Useful for "show me all documents from KBC" or "what did I scan last month."

```csharp
[Tool("list_documents")]
async Task<string> ListDocuments(
    string? correspondent = null,
    string? tag = null,
    DateTime? addedAfter = null,
    DateTime? addedBefore = null,
    int maxResults = 25
)
```

Returns: summary list with ID, title, correspondent, tags, dates.

### `get_document_metadata`
Retrieve metadata without the full text — faster for bulk operations.

```csharp
[Tool("get_document_metadata")]
async Task<string> GetDocumentMetadata(int documentId)
```

Returns: title, correspondent, tags, dates (created, added, modified), archive serial number, original filename.

### `list_tags`
List all available tags in Paperless. Helps Claude Code understand the taxonomy.

```csharp
[Tool("list_tags")]
async Task<string> ListTags()
```

Returns: tag names with document counts.

---

## Vision OCR Fallback

The fallback triggers when Paperless OCR output looks unreliable:

```
1. get_document_content called
2. Fetch OCR text from Paperless REST API
3. Quality check:
   - Text length < 50 chars for a multi-page doc? → suspect
   - High ratio of garbage characters (□, �, ...)? → suspect
   - forceVisionOcr explicitly set? → skip check
4. If suspect → fetch original image/PDF from Paperless
5. Convert to base64, send to Claude Vision API
6. Return vision transcription instead
```

Threshold is conservative — Tesseract is fast and free, Vision API costs money. Only falls back when clearly needed.

---

## Paperless-ngx REST API Reference

Base URL: `http://<paperless-host>:8000/api/`
Auth: Token-based (`Authorization: Token <api-token>`)

Key endpoints:
- `GET /api/documents/` — list/search documents (supports `?query=`, `?tags__id=`, `?correspondent__id=`, `?ordering=`)
- `GET /api/documents/{id}/` — document metadata
- `GET /api/documents/{id}/download/` — original file
- `GET /api/documents/{id}/preview/` — archived (OCR'd) version
- `GET /api/documents/{id}/thumb/` — thumbnail
- `GET /api/tags/` — list tags
- `GET /api/correspondents/` — list correspondents
- `GET /api/document_types/` — list document types

Full API docs ship with Paperless at `/api/schema/swagger-ui/`.

---

## Project Structure

```
CheapClerk/
├── CheapClerk.csproj
├── CheapClerk.slnx
├── Program.cs                       # MCP server bootstrap
├── appsettings.json
├── README.md
├── TODO.md
├── LICENSE
├── .gitignore
├── .github/
│   └── workflows/
│       └── dotnet.yml
├── Configuration/
│   ├── PaperlessOptions.cs
│   └── VisionFallbackOptions.cs
├── Tools/
│   ├── SearchDocumentsTool.cs
│   ├── GetDocumentContentTool.cs
│   ├── ListDocumentsTool.cs
│   ├── GetDocumentMetadataTool.cs
│   └── ListTagsTool.cs
├── Services/
│   ├── PaperlessClient.cs           # HTTP client for Paperless REST API
│   ├── VisionOcrService.cs          # Claude Vision fallback
│   └── OcrQualityChecker.cs         # Confidence threshold logic
├── Models/
│   ├── PaperlessDocument.cs
│   ├── PaperlessTag.cs
│   ├── PaperlessCorrespondent.cs
│   ├── PaperlessPage.cs
│   └── DocumentMatch.cs
└── docker/
    └── docker-compose.yml           # Paperless-ngx deployment
```

---

## Claude Code MCP Configuration

Add to `~/.claude.json` (global) or `.claude/settings.json` (project):

```json
{
  "mcpServers": {
    "cheapclerk": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/CheapClerk"]
    }
  }
}
```

---

## Configuration (appsettings.json)

```json
{
  "Paperless": {
    "BaseUrl": "http://localhost:8000",
    "ApiToken": "<from-paperless-admin-panel>"
  },
  "VisionFallback": {
    "Enabled": true,
    "MinTextLength": 50,
    "MaxGarbageRatio": 0.15
  },
  "Llm": {
    "Provider": "Anthropic",
    "Anthropic": {
      "ApiKey": "<anthropic-api-key>",
      "Model": "claude-haiku-4-5-20251001"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.2"
    }
  }
}
```

**LLM providers:** Structured extraction uses the configured `Llm.Provider` (`Anthropic` or `Ollama`). Vision OCR fallback always uses Anthropic since local vision models are still unreliable for Belgian household documents. To run fully offline, set `Provider: Ollama` and disable `VisionFallback.Enabled`.

---

## Paperless-ngx Docker Compose

Deployed on Sierra-Madre at `/opt/paperless` (see `docker/docker-compose.yml` for the reference file). Key deployment choices:

- **Port 8010** on the host (8000 was taken by Portainer's edge tunnel)
- **Media and export on `/mnt/bfa-appdata/paperless`** — a mirrored ZFS pool, because scanned originals are the one copy that must survive a disk failure. Data/consume/redis stay in local Docker volumes (regenerable).
- **PostgreSQL on Vault-Tec** instead of SQLite, following the pattern of the other self-hosted services
- **Secrets in `/opt/paperless/.env`** (`PAPERLESS_DBPASS`, `PAPERLESS_SECRET_KEY`, `PAPERLESS_ADMIN_PASSWORD`)
- **`PAPERLESS_OCR_LANGUAGES: nld fra deu`** installs the tesseract language packs at container start (the image only ships English data; `PAPERLESS_OCR_LANGUAGE` alone fails the startup check). If the container comes up unhealthy right after an image update, restart it once — the language install can race the startup check on first boot.

OCR languages: Dutch (primary), English, French, German — covers Belgian household documents.

The `cheapclerk-web` container runs separately on Megaton (`/opt/blazor-apps/cheapclerk`) and points at Paperless via `Paperless__BaseUrl` + an API token generated with `manage.py drf_create_token`.

---

## Automatic Classification

New documents don't need manual filing. Paperless marks every consumed document with the `Inbox` tag (created automatically as an inbox-type tag on the clerk's first run); CheapClerk then reads the OCR text and asks the configured LLM for a title, correspondent, document type, topical tags and the document date, PATCHing the result back and removing the inbox tag. Existing taxonomy is strongly preferred — new tags are only created when nothing fits (capped, existing matches win). Garbled scans go through the Vision OCR fallback first. Anything the classifier isn't confident about gets a `Needs Review` tag instead of guesses.

Four triggers:
- **Background poll** — `InboxPollingService` in CheapClerk.Web, every `Classification:PollIntervalMinutes` (0 disables the poller)
- **Dashboard button** — "Process now" on the inbox card
- **MCP tool** — `process_inbox` from Claude Code
- **Webhook** — Paperless fires POST /api/inbox/process (token-guarded) the moment a document is added; the poll becomes a safety net. Prefer sending the token via the `X-Webhook-Token` header; the `?token=` query form works but relies on request-path logging staying suppressed (`Microsoft.AspNetCore` at Warning), and any reverse proxy in front would log query strings regardless.

Configuration (`Classification` section):

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch |
| `InboxTagName` | `Inbox` | Tag marking unprocessed documents |
| `ReviewTagName` | `Needs Review` | Applied instead of guesses below the confidence bar |
| `MinConfidence` | `0.6` | Below this, documents go to review |
| `PollIntervalMinutes` | `15` | Background poll cadence; `0` = manual only |
| `MaxTagsPerDocument` | `4` | Cap on applied tags, existing matches first |
| `AutoCreateTags` | `true` | Allow the LLM to introduce new tags |
| `MaxDocumentsPerRun` | `20` | Batch size per run; the poller drains over successive runs |
| `WebhookToken` | (unset) | Shared secret for the webhook endpoint; unset = endpoint returns 404 |

Classification uses the same `Llm.Provider` switch as extraction — without an Anthropic key (or Ollama endpoint) configured, the processor logs that the provider is unconfigured and leaves the inbox untouched.

---

## Example Queries

Once documents are scanned and indexed, these should all work from Claude Code:

- "What's my home insurance policy number?"
- "When does my electricity contract with Engie expire?"
- "How much was my last water bill?"
- "Show me all documents from KBC"
- "What warranty do I have on the Optoma projector?"
- "What's the cadastral income on my property tax assessment?"
- "Find anything related to my car insurance for the Focus ST"
- "What documents did I scan this week?"

---

## Roadmap

**Phase 1 (now):** MCP server + Paperless-ngx, Claude Code only
**Phase 2:** Blazor Server UI with MudBlazor (search, browse, quick actions)
**Phase 3:** Multi-LLM support (Ollama local models for non-sensitive queries)
**Phase 4:** Structured data extraction — recognize bill formats, extract amounts/dates into typed models (feeds back into Voltiq's bill parser)
**Phase 5:** Automated workflows — "notify me when a document tagged 'expiring' is within 30 days of its end date"

---

## Relationship to Voltiq

CheapClerk is a general-purpose document archive for personal use. Voltiq is a focused energy intelligence platform for Belgian SMEs.

The skills developed here — PDF parsing, OCR quality assessment, Vision API fallback, structured extraction from scanned documents — will be reapplied in Voltiq's bill ingestion feature. But the architectures are intentionally separate: CheapClerk uses Paperless-ngx for broad document management, Voltiq will have a focused `BillParserService` that extracts typed energy data directly into TimescaleDB.

---

## License

MIT — see [LICENSE](LICENSE).
