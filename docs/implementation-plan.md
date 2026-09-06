# Orbit Implementation Plan

> Inline execution in the current task. Approved design: docs/design.md.

**Goal:** Deliver a working, locally installed menu-bar dashboard including Claude Code.

**Architecture:** AppKit popover + local WebKit resources. Native providers read local metadata; a separate OAuth/API client handles Google.

**Tech Stack:** Swift 5 language mode, macOS 14+, AppKit, WebKit, Foundation, SQLite3, Network, Security, CryptoKit.

## Global constraints

- No third-party dependencies; no local server during ordinary app use.
- Do not mutate source-provider files or authenticate using other applications' credentials.
- Web UI contains no credentials and receives only display data and allowlisted action IDs.
- Preserve three approved themes and Korean interface text.

## Tasks

- [x] Models and local providers: typed metadata; read-only SQLite, bounded JSONL/file scan; synthetic tests and count-only local smoke test passed.
- [x] Google connection implementation: PKCE, state checks, Keychain, refresh, pagination and task PATCH. Offline fixtures passed; disconnected state verified. Live acceptance remains below.
- [x] Native shell implementation: installed AppKit popover, native pickers, allowlisted actions, single instance, offline WKWebView. Launch, pin and refresh checked in actual app.
- [x] Production UI implementation: today/files/AI, Claude filter, Korean search, three themes, settings. Native interactive checks passed for these controls. Fixed SVG/control duplicate IDs with regression test.
- [x] Packaging/closeout: build, ad-hoc signing verification, installation, 32 Swift + 4 web tests, README and handoff completed.

## Remaining live acceptance

- [ ] Import user-owned Desktop OAuth JSON and obtain explicit user consent for Calendar readonly + Tasks write. Verify live read, complete/reopen and reconnect. No other app credentials may be reused.
- [ ] User verifies exact Codex task opening and Claude resume command in Terminal. Target UI inspection is blocked by computer-use safety policy; do not bypass.
- [ ] User verifies a real file open and opt-in login startup. Provider files must remain unchanged.
