# Orbit design

Approved: 2026-09-05. User approved the Orbit menu-bar dashboard visual design and requested implementation, including Claude tasks.

## Product

An actual macOS menu-bar application with a 560px panel, three themes (Moss default, Pearl, Midnight), today's Google Calendar events and Google Tasks, recently modified Drive/Obsidian documents, Codex and Claude Code tasks. Simple actions: complete/reopen tasks, open files, return to AI sessions. No new AI messages or session execution without a click.

## Architecture

Swift AppKit status item and popover with a bundled WKWebView retaining the approved HTML/CSS. Local native message bridge accepts a fixed allowlist of actions and IDs; external content never executes as HTML. Only bundled pages can invoke the bridge. Resources load offline; no remote fonts or scripts.

Local providers: SQLite read-only queries for Codex metadata and latest turn status; bounded Claude Code JSONL metadata parsing; metadata-only file scans in selected synced Drive roots and the active Obsidian vault. No arbitrary shell execution from web content. Opening Claude copies a validated resume command and opens Terminal; it does not execute a session automatically. Claude Desktop chat history is not inferred from Claude Code records.

Google: desktop OAuth authorization code + PKCE, random state, loopback-only temporary callback, native browser consent, refresh tokens stored in Keychain. User imports their desktop OAuth JSON. Calendar read-only and Tasks read/write only; task completion is sent only after the user clicks. Absent credentials are visibly disconnected. API failure preserves last successful data with a stale/error label; disconnected accounts clear data. OAuth consent and Google Cloud client creation require user involvement.

## Reliability

App and source under user's home, no cloud changes. Refresh on opening and periodically while visible. Cache local metadata in memory, file scan bounded and backgrounded. Exact per-source status and refresh times. Do not infer 'running' from a recent modification; unknown state is labelled 'recent activity' or 'status unknown'. App may be quit from its own menu; startup at login is opt-in.

## Verification

Build with system Swift, test parsers/security/date helpers using synthetic fixtures, smoke-test local providers with count-only output, inspect actual native app UI. Google API logic uses injected HTTP fixtures. Live OAuth is unverified until a valid client is imported and the user consents.
