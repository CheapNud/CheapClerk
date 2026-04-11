<!--
  TODO.md — CheapClerk project work tracker
  Last updated: 2026-04-11 (extraction)

  RULES FOR AI AGENTS:
  - Update the "Last updated" date above whenever you modify this file
  - Items use checkbox format: - [ ] incomplete, - [x] complete
  - Never remove completed items — they serve as history. Move them to "## Done" when a category gets cluttered.
  - Each item gets ONE line. Details go in sub-bullets indented with 2 spaces.
  - Prefix each item with the date it was added: - [ ] (2026-03-17) Description
  - When completing, change to: - [x] (2026-03-17 → 2026-03-18) Description
  - Tag the SOURCE of each item at the end in brackets:
      [code-todo] = from // TODO comment in source code
      [plan] = from a plan document or planning session
      [bug] = from a bug encountered during dev/deploy
      [audit] = from a code audit or review
      [user] = explicitly requested by the user
  - For [code-todo] items, ALWAYS include file:line reference so devs can navigate directly
  - Categories: Blocking, Planned, Future, Done
  - New items go at the TOP of their category
  - Do not create separate TODO_*.md files — everything goes here
  - Keep it terse. If it needs more than 3 sub-bullets, link to a plan document.
  - Do NOT create, rename, or remove categories — the fixed set is: Blocking, Planned, Future, Done
  - When asked for planned work or TODO analysis, ALWAYS include Future items too — list them below Planned and note them as future work
-->

# TODO — CheapClerk

## Blocking

_Nothing blocking._

## Planned

_Nothing planned._

## Future

- [ ] (2026-04-04) Evaluate CheapHelpers for shared plumbing and potential OCR delegation [plan]
- [ ] (2026-04-04) Automated workflows — notify on expiring documents within 30 days [plan]

## Done

- [x] (2026-04-04 → 2026-04-04) Scaffold .NET 11 console project with MCP server bootstrap [plan]
- [x] (2026-04-04 → 2026-04-04) Implement PaperlessClient service (HTTP client for Paperless-ngx REST API) [plan]
- [x] (2026-04-04 → 2026-04-04) Implement search_documents MCP tool [plan]
- [x] (2026-04-04 → 2026-04-04) Implement get_document_content MCP tool [plan]
- [x] (2026-04-04 → 2026-04-04) Implement list_documents MCP tool [plan]
- [x] (2026-04-04 → 2026-04-04) Implement get_document_metadata MCP tool [plan]
- [x] (2026-04-04 → 2026-04-04) Implement list_tags MCP tool [plan]
- [x] (2026-04-04 → 2026-04-04) Implement OcrQualityChecker service [plan]
- [x] (2026-04-04 → 2026-04-04) Implement VisionOcrService (Claude Vision fallback) [plan]
- [x] (2026-04-04 → 2026-04-04) Add appsettings.json configuration [plan]
- [x] (2026-04-04 → 2026-04-04) Add docker-compose.yml for Paperless-ngx deployment [plan]
- [x] (2026-04-04 → 2026-04-04) Fix naming violations per global CLAUDE.md variable naming rules [audit]
- [x] (2026-04-04 → 2026-04-04) Use SearchValues<char> in OcrQualityChecker for SIMD-accelerated scanning [audit]
- [x] (2026-04-04 → 2026-04-04) Move project from src/CheapClerk/ to solution root [user]
- [x] (2026-04-04 → 2026-04-04) Cache tag/correspondent lookups in PaperlessClient (5min ConcurrentDictionary TTL) [audit]
- [x] (2026-04-04 → 2026-04-04) Add GitHub Actions PR review workflow (ported from CheapHelpers) [plan]
- [x] (2026-04-04 → 2026-04-04) Create Gitea repo on Sierra-Madre with push mirror to GitHub (24h + sync on commit) [plan]
- [x] (2026-04-08 → 2026-04-11) Blazor Server UI with MudBlazor (dashboard, documents, search, tags, detail view with Vision OCR trigger) [plan]
- [x] (2026-04-11 → 2026-04-11) Structured data extraction — Invoice/Insurance/Contract typed models via Claude IChatClient.GetResponseAsync<T> [plan]
- [x] (2026-04-11 → 2026-04-11) Multi-LLM support — Llm.Provider config switch (Anthropic/Ollama) with OllamaSharp, refactored VisionOcrService to read Anthropic creds from LlmOptions [plan]
