# Windows implementation

The user authorized Windows compatibility checks, native implementation, UI fixes and integration validation in this task.
Use C#/.NET 10, WPF, NotifyIcon and WebView2 here. Store credentials only in Windows Credential Manager.
Preserve Sources/ and the macOS build. Share Resources/ with backward-compatible fields.
Do not delegate. Do not register startup, invoke an AI CLI, copy resume commands or send AI messages.
Read provider data only; diagnostics must not contain filenames, task titles, paths or credentials.
Run Windows/scripts/test.ps1 and build.ps1. Actual Google writes require the user's designated test item.
Document unavailable native/macOS/manual validation honestly.
