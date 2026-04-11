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
│  Paperless-ngx              │  Docker on Megaton (192.168.1.x)
│  • Tesseract OCR            │  Behind Hidden-Valley nginx reverse proxy
│  • Full-text search (FTS)   │
│  • REST API (:8000)         │
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
      "Model": "claude-sonnet-4-20250514"
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

Deploy on Megaton alongside existing containers:

```yaml
services:
  paperless-broker:
    image: redis:7
    container_name: paperless-broker
    restart: unless-stopped
    volumes:
      - paperless-redis:/data

  paperless:
    image: ghcr.io/paperless-ngx/paperless-ngx:latest
    container_name: paperless
    restart: unless-stopped
    depends_on:
      - paperless-broker
    ports:
      - "8000:8000"
    volumes:
      - paperless-data:/usr/src/paperless/data
      - paperless-media:/usr/src/paperless/media
      - paperless-export:/usr/src/paperless/export
      - paperless-consume:/usr/src/paperless/consume
    environment:
      PAPERLESS_REDIS: redis://paperless-broker:6379
      PAPERLESS_OCR_LANGUAGE: nld+eng+fra+deu
      PAPERLESS_TIME_ZONE: Europe/Brussels
      PAPERLESS_SECRET_KEY: <generate-a-random-key>
      PAPERLESS_ADMIN_USER: brecht
      PAPERLESS_ADMIN_PASSWORD: <set-on-first-run>
      PAPERLESS_URL: https://docs.cheaplues.be
      PAPERLESS_OCR_MODE: skip_noarchive
      PAPERLESS_TASK_WORKERS: 2
      PAPERLESS_CONSUMER_RECURSIVE: "true"

volumes:
  paperless-redis:
  paperless-data:
  paperless-media:
  paperless-export:
  paperless-consume:
```

OCR languages: Dutch (primary), English, French, German — covers Belgian household documents.

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
