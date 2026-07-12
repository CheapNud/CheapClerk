<!--
  TODO.md — CheapClerk project work tracker
  Last updated: 2026-07-12 (title translation noted as future work)

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

- [x] (2026-07-11 → 2026-07-11) Localization — EN/NL UI (resx + culture picker) and taxonomy translation layer (SQLite map, LLM-filled, display-only) [user]
  - Writes stay canonical; name-keyed rows self-heal on tag renames; translate_taxonomy MCP tool backfills
- [ ] (2026-07-11) Add authentication to cheapclerk-web BEFORE any exposure beyond the trusted LAN [audit]
  - App has no auth by design (single-user LAN); all pages and the file proxy expose the full archive
  - Hard gate: never proxy through Hidden-Valley / NPM without ASP.NET Identity in place first
- [ ] (2026-07-06) Recover from cross-host entity-creation races in ClassificationApplier [audit]
  - Duplicate-name POST 400s → CreateTagAsync null → tag silently dropped from the filed doc (Applied still true)
  - On null create, force-refresh the lookup and rematch by name; same for correspondent/type
- [ ] (2026-07-06) Review queue capped at MaxDocumentsPerRun with no "more" indicator [audit]
  - GetQueueAsync reuses the batch knob as a page size; >20 queued docs are invisible in UI and MCP
- [x] (2026-07-06 → 2026-07-06) Review queue — stored suggestions, /review page (accept/edit/re-run), 3 MCP tools [user]
  - Suggestions persisted in SQLite at low-confidence time; filing shares the applier with auto-classification
- [x] (2026-07-06 → 2026-07-06) Instant webhook trigger — Paperless Document Added workflow → token-guarded endpoint → coalesced run [user]
  - Poller demoted to safety net (60 min in prod); endpoint dark without Classification__WebhookToken
- [ ] (2026-07-06) Typed SkippedReason so the coordinator can requeue webhook runs that lose the gate race [audit]
  - Webhook run contending with a poller-held semaphore is silently consumed; doc waits for next poll
  - Watch for "Triggered inbox run skipped: a processing run is already in progress" in Seq — promote if frequent
- [ ] (2026-07-06) Paginate tag fetch or use name__iexact lookup in EnsureWorkflowTagsAsync [audit]
  - GetTagsAsync caps at page_size=100; once the taxonomy outgrows page 1, workflow tags stop resolving and every run aborts
  - AutoCreateTags makes this a when, not an if
- [ ] (2026-07-06) Sanitize LLM-created tag/correspondent names (length cap, strip newlines) [audit]
  - Injected names feed back into every future classification prompt via the existing-tags list
- [ ] (2026-07-06) Short-circuit inbox run after N consecutive LLM failures [audit]
  - When the provider is down, each of up to 20 docs waits out a full HTTP timeout sequentially
- [x] (2026-07-06 → 2026-07-06) Automatic inbox classification — LLM titles/tags/correspondents/dates new Paperless documents [user]
  - Paperless inbox tag marks incoming docs; clerk polls + dashboard button + process_inbox MCP tool
  - Low-confidence docs get a Needs Review tag instead of guesses; existing taxonomy preferred over new tags
  - Requires Llm config (Anthropic key or Ollama) on the web host — without it the processor no-ops
- [ ] (2026-07-04) Regenerate GitHub mirror PAT with workflow scope and recreate Gitea push mirror [bug]
  - Mirror push fails (GH013) when commits touch .github/workflows/; credentials can't be edited in place
  - Until fixed: push workflow changes to GitHub first, then fast-forward Gitea
- [x] (2026-04-15 → 2026-07-04) Deploy Paperless-ngx and configure PAPERLESS_API_TOKEN for CheapClerk [plan]
  - Target changed Megaton → Sierra-Madre (Megaton RAM/disk exhausted; documents belong on mirrored bfa pool, not ssd-vms RAID0)
  - Stack at /opt/paperless on Sierra-Madre (:8010), media/export on /mnt/bfa-appdata/paperless, DB on Vault-Tec Postgres
  - cheapclerk-web on Megaton repointed via Paperless__BaseUrl + token in /opt/blazor-apps/cheapclerk/.env

## Future

- [ ] (2026-07-12) Per-culture document TITLE translations [user]
  - Deliberately skipped in the v1.5.0 localization layer (YAGNI): titles are per-document data, so the map grows one row per document per culture and each new document costs an extra LLM call per culture
  - Becomes worth it if CheapClerk is ever packaged for wider use; design would mirror NameTranslations (display-only, canonical writes) with translate-at-classification-time batching

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
- [x] (2026-04-11 → 2026-04-11) Expiry workflow — SQLite extraction cache via EF Core, find_expiring_documents + refresh_extraction_cache MCP tools, Expiring Blazor page with dashboard card [plan]
- [x] (2026-04-04 → 2026-04-11) Evaluate CheapHelpers — decided NOT to adopt. Marginal wins (SlidingExpirationCache, email) don't justify pulling SendGrid/MailKit/ClosedXML/iText/GoogleApi transitives. OCR is Azure-Vision-only (we use Claude). If email alerts are needed later, reference MailKit directly [plan]
- [x] (2026-04-15 → 2026-04-15) Initial deploy to Megaton — port 5030, NoOpChatClient fallback for missing API key, Error.razor fix for .NET 11 preview [plan]
- [x] (2026-04-15 → 2026-04-15) GHCR publish workflow — GitHub Actions builds on push to main/master, Megaton pulls pre-built image (no on-host builds) [plan]
