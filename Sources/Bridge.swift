import AppKit
import ServiceManagement
import UniformTypeIdentifiers
import WebKit

@MainActor final class Bridge: NSObject, WKScriptMessageHandler, WKNavigationDelegate {
  unowned let app: OrbitApp
  let webView: WKWebView
  let prefs = Preferences()
  let google = GoogleClient()
  var local = LocalSnapshot()
  var events = [CalendarEntry]()
  var tasks = [TaskEntry]()
  var googleStatuses = [SourceStatus]()
  var localBusy = false
  var googleBusy = false
  var taskBusy = Set<String>()
  var lastFileScan = Date.distantPast
  var googleGeneration = 0
  var localGeneration = 0
  var ready = false
  let resources: URL
  init(app: OrbitApp) {
    self.app = app
    resources = Bundle.main.resourceURL!
    let config = WKWebViewConfiguration()
    config.websiteDataStore = .nonPersistent()
    webView = WKWebView(frame: NSRect(x: 0, y: 0, width: 560, height: 700), configuration: config)
    super.init()
    config.userContentController.add(self, name: "orbit")
    webView.navigationDelegate = self
    webView.setValue(false, forKey: "drawsBackground")
    webView.allowsBackForwardNavigationGestures = false
  }
  func load() {
    app.applyTheme(prefs.theme)
    webView.loadFileURL(
      resources.appendingPathComponent("index.html"), allowingReadAccessTo: resources)
  }
  func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
    ready = true
    publish()
  }
  func webView(
    _ webView: WKWebView, decidePolicyFor action: WKNavigationAction,
    decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
  ) {
    guard let url = action.request.url, url.isFileURL,
      url.standardizedFileURL.path
        == resources.appendingPathComponent("index.html").standardizedFileURL.path
    else {
      decisionHandler(.cancel)
      return
    }
    decisionHandler(.allow)
  }
  func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
    ready = false
    webView.reload()
  }
  func emit(_ value: [String: Any]) {
    guard ready,
      let data = try? JSONSerialization.data(withJSONObject: value, options: [.fragmentsAllowed]),
      let string = String(data: data, encoding: .utf8)
    else { return }
    webView.evaluateJavaScript("window.orbit.receive(\(string))", completionHandler: nil)
  }
  func reply(_ request: String, error: String? = nil, message: String? = nil) {
    var result: [String: Any] = ["kind": "reply", "request": request]
    if let e = error { result["error"] = e }
    if let m = message { result["message"] = m }
    emit(result)
  }
  func publish() {
    var statuses = local.statuses
    if googleStatuses.isEmpty {
      statuses += ["calendar", "tasks"].map {
        SourceStatus(
          source: $0, state: "disconnected",
          message: google.loadError.isEmpty ? "Google 계정을 연결하세요." : google.loadError, updated: 0)
      }
    } else {
      statuses += googleStatuses
    }
    emit([
      "kind": "snapshot", "files": local.files.map { $0.display() },
      "agents": local.agents.map { $0.display() }, "events": jsonObject(events),
      "tasks": jsonObject(tasks), "statuses": jsonObject(statuses), "theme": prefs.theme,
      "pinned": app.pinned, "refreshing": localBusy || googleBusy,
      "settings": [
        "drive": prefs.drive, "vault": prefs.vault, "googleConfigured": google.configured,
        "googleConnected": google.connected, "googleAuthorizing": google.authorizing,
        "launchAtLogin": SMAppService.mainApp.status == .enabled,
      ],
    ])
  }
  func refresh(forceFiles: Bool) async {
    if !localBusy {
      localBusy = true
      publish()
      let drive = prefs.drive
      let vault = prefs.vault
      let codex = prefs.codex
      let claude = prefs.claude
      let generation = localGeneration
      let old = local
      let scanFiles = forceFiles || Date().timeIntervalSince(lastFileScan) > 120
      let value = await Task.detached(priority: .utility) {
        if scanFiles {
          return LocalProviders.scan(drive: drive, vault: vault, codex: codex, claude: claude)
        }
        var v = LocalSnapshot()
        v.files = old.files
        v.statuses = old.statuses.filter { ["drive", "obsidian"].contains($0.source) }
        let now = Date().timeIntervalSince1970
        for provider in ["codex", "claude"] {
          do {
            v.agents +=
              try provider == "codex"
              ? LocalProviders.codexSessions(root: codex)
              : LocalProviders.claudeSessions(root: claude)
            v.statuses.append(
              .init(source: provider, state: "connected", message: "로컬 작업 기록", updated: now))
          } catch {
            v.statuses.append(
              .init(
                source: provider, state: "error", message: error.localizedDescription, updated: 0))
          }
        }
        v.agents.sort { $0.updated > $1.updated }
        return v
      }.value
      if generation != localGeneration {
        localBusy = false
        await refresh(forceFiles: true)
        return
      }
      local = value
      if scanFiles { lastFileScan = Date() }
      localBusy = false
      publish()
    }
    await refreshGoogle()
  }
  func refreshGoogle() async {
    guard google.connected, !googleBusy, taskBusy.isEmpty else {
      publish()
      return
    }
    googleBusy = true
    let gen = googleGeneration
    publish()
    for source in ["calendar", "tasks"] {
      do {
        if source == "calendar" {
          let next = try await google.events()
          if gen == googleGeneration { events = next }
        } else {
          let next = try await google.tasks()
          if gen == googleGeneration { tasks = next }
        }
        if gen == googleGeneration {
          setGoogleStatus(
            .init(
              source: source, state: "connected", message: "Google 연결됨",
              updated: Date().timeIntervalSince1970))
        }
      } catch {
        if gen == googleGeneration {
          let previous = googleStatuses.first { $0.source == source }?.updated ?? 0
          setGoogleStatus(
            .init(
              source: source, state: "error", message: error.localizedDescription, updated: previous
            ))
        }
      }
      if gen != googleGeneration { break }
      publish()
    }
    googleBusy = false
    publish()
  }
  func setGoogleStatus(_ status: SourceStatus) {
    googleStatuses.removeAll { $0.source == status.source }
    googleStatuses.append(status)
  }
  func userContentController(
    _ controller: WKUserContentController, didReceive message: WKScriptMessage
  ) {
    guard message.frameInfo.isMainFrame,
      message.frameInfo.request.url?.standardizedFileURL.path
        == resources.appendingPathComponent("index.html").standardizedFileURL.path,
      let body = message.body as? [String: Any], let action = body["action"] as? String,
      let request = body["request"] as? String, request.count < 100
    else { return }
    Task {
      do {
        let result = try await perform(action, body: body)
        reply(request, message: result)
      } catch { reply(request, error: error.localizedDescription) }
      publish()
    }
  }
  func perform(_ action: String, body: [String: Any]) async throws -> String? {
    switch action {
    case "ready": publish()
    case "refresh": await refresh(forceFiles: true)
    case "theme":
      guard let value = body["value"] as? String, ["moss", "pearl", "cobalt"].contains(value) else {
        throw OrbitError(message: "지원하지 않는 테마입니다.")
      }
      prefs.theme = value
      app.applyTheme(value)
    case "pin": app.setPinned(body["value"] as? Bool ?? false)
    case "close": app.popover.performClose(nil)
    case "quit": app.quit()
    case "openFile":
      guard let id = body["id"] as? String, let file = local.files.first(where: { $0.id == id })
      else { throw OrbitError(message: "파일 목록을 새로고침하세요.") }
      let url = URL(fileURLWithPath: file.path).resolvingSymlinksInPath()
      let root = URL(fileURLWithPath: file.source == "obsidian" ? prefs.vault : prefs.drive)
        .resolvingSymlinksInPath()
      guard url.path.hasPrefix(root.path + "/"), FileManager.default.fileExists(atPath: url.path)
      else { throw OrbitError(message: "파일이 이동되었거나 폴더 범위를 벗어났습니다.") }
      if file.source == "obsidian", url.pathExtension == "md" {
        var c = URLComponents()
        c.scheme = "obsidian"
        c.host = "open"
        c.queryItems = [.init(name: "path", value: url.path)]
        guard let u = c.url, NSWorkspace.shared.open(u) else {
          throw OrbitError(message: "Obsidian을 열 수 없습니다.")
        }
      } else {
        guard NSWorkspace.shared.open(url) else {
          throw OrbitError(message: "이 파일을 열 수 있는 앱을 확인하세요.")
        }
      }
    case "openAgent":
      guard let id = body["id"] as? String, let entry = local.agents.first(where: { $0.id == id }),
        UUID(uuidString: entry.sessionID) != nil
      else { throw OrbitError(message: "작업 목록을 새로고침하세요.") }
      if entry.provider == "codex" {
        guard let url = URL(string: "codex://threads/" + entry.sessionID),
          NSWorkspace.shared.open(url)
        else { throw OrbitError(message: "Codex 앱을 열 수 없습니다.") }
      } else {
        let executable = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(
          ".local/bin/claude"
        ).path
        guard FileManager.default.isExecutableFile(atPath: executable),
          let command = resumeCommand(entry, executable: executable)
        else { throw OrbitError(message: "Claude Code 실행 경로 또는 세션 작업 폴더를 확인하세요.") }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(command, forType: .string)
        let terminal = URL(fileURLWithPath: "/System/Applications/Utilities/Terminal.app")
        NSWorkspace.shared.openApplication(
          at: terminal, configuration: .init(), completionHandler: nil)
        return "이어하기 명령을 복사했습니다. Terminal에 붙여넣고 Enter를 누르세요."
      }
    case "openCalendar":
      NSWorkspace.shared.open(URL(string: "https://calendar.google.com/calendar/u/0/r")!)
    case "openTasks": NSWorkspace.shared.open(URL(string: "https://tasks.google.com")!)
    case "openEvent":
      guard let id = body["id"] as? String, let event = events.first(where: { $0.id == id }),
        let url = httpsURL(
          body["meeting"] as? Bool == true && !event.meetingURL.isEmpty
            ? event.meetingURL : event.url)
      else { throw OrbitError(message: "일정 링크가 없거나 변경되었습니다.") }
      NSWorkspace.shared.open(url)
    case "completeTask":
      guard let id = body["id"] as? String, let entry = tasks.first(where: { $0.id == id }),
        let value = body["value"] as? Bool
      else { throw OrbitError(message: "할 일 목록을 새로고침하세요.") }
      guard !googleBusy, !taskBusy.contains(id) else {
        throw OrbitError(message: "동기화 중입니다. 잠시 후 다시 시도하세요.")
      }
      taskBusy.insert(id)
      defer { taskBusy.remove(id) }
      let gen = googleGeneration
      try await google.complete(entry, value: value)
      if gen == googleGeneration, let index = tasks.firstIndex(where: { $0.id == id }) {
        tasks[index].completed = value
      }
      return value ? "Google Tasks에 완료로 저장했습니다." : "Google Tasks 할 일을 다시 열었습니다."
    case "chooseDrive": try chooseFolder(key: "drive")
    case "chooseVault": try chooseFolder(key: "vault")
    case "importGoogle": try importGoogle()
    case "connectGoogle":
      guard !googleBusy else { throw OrbitError(message: "Google 동기화가 끝난 후 다시 연결하세요.") }
      let operation = Task { try await google.connect() }
      await Task.yield()
      publish()
      try await operation.value
      googleGeneration += 1
      await refreshGoogle()
      return "Google 계정을 연결했습니다."
    case "cancelGoogle": google.oauth.cancel()
    case "disconnectGoogle":
      googleGeneration += 1
      try google.disconnect()
      events = []
      tasks = []
      googleStatuses = []
      return "이 맥의 Orbit에서 Google 연결을 해제했습니다."
    case "googleHelp":
      NSWorkspace.shared.open(resources.appendingPathComponent("google-setup.html"))
    case "launchAtLogin":
      let enable = body["value"] as? Bool ?? false
      if enable {
        try SMAppService.mainApp.register()
      } else {
        try await SMAppService.mainApp.unregister()
      }
    default: throw OrbitError(message: "지원하지 않는 동작입니다.")
    }
    return nil
  }
  func chooseFolder(key: String) throws {
    let wasPinned = app.pinned
    app.setPinned(true)
    defer { app.setPinned(wasPinned) }
    let panel = NSOpenPanel()
    panel.canChooseFiles = false
    panel.canChooseDirectories = true
    panel.allowsMultipleSelection = false
    panel.message =
      key == "drive" ? "최근 파일을 표시할 Google Drive 폴더를 선택하세요." : "Obsidian Vault 폴더를 선택하세요."
    panel.prompt = "이 폴더 사용"
    guard panel.runModal() == .OK, let url = panel.url else { return }
    if key == "drive" { prefs.drive = url.path } else { prefs.vault = url.path }
    localGeneration += 1
    lastFileScan = .distantPast
    Task { await refresh(forceFiles: true) }
  }
  func importGoogle() throws {
    let wasPinned = app.pinned
    app.setPinned(true)
    defer { app.setPinned(wasPinned) }
    let panel = NSOpenPanel()
    panel.allowedContentTypes = [.json]
    panel.allowsMultipleSelection = false
    panel.message = "Google Cloud의 데스크톱 OAuth 클라이언트 JSON을 선택하세요. 키체인에 안전하게 저장됩니다."
    panel.prompt = "가져오기"
    guard panel.runModal() == .OK, let url = panel.url else { return }
    guard !googleBusy else { throw OrbitError(message: "Google 동기화가 끝난 후 설정을 변경하세요.") }
    let data = try Data(contentsOf: url)
    guard data.count < 65536 else { throw OrbitError(message: "OAuth 설정 JSON 크기가 올바르지 않습니다.") }
    try google.importConfig(data)
    googleGeneration += 1
    events = []
    tasks = []
    googleStatuses = []
  }
}
