import Foundation
import SQLite3

enum SelfTests {
  @MainActor static func run() async -> Bool {
    var passed = 0
    var failures = [String]()
    func check(_ name: String, _ result: Bool) {
      if result { passed += 1 } else { failures.append(name) }
    }
    func rejects(_ work: () throws -> Void) -> Bool {
      do {
        try work()
        return false
      } catch { return true }
    }
    check(
      "PKCE RFC7636 S256 vector",
      OAuthHelpers.challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")
        == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
    check(
      "callback matching state",
      (try? OAuthHelpers.callbackCode(
        target: "/oauth/callback?code=good%2Bcode&state=known", state: "known")) == "good+code")
    check(
      "reject mismatched state",
      rejects {
        _ = try OAuthHelpers.callbackCode(
          target: "/oauth/callback?code=x&state=wrong", state: "known")
      })
    check(
      "reject duplicate code",
      rejects {
        _ = try OAuthHelpers.callbackCode(
          target: "/oauth/callback?code=a&code=b&state=known", state: "known")
      })
    check(
      "reject wrong callback path",
      rejects {
        _ = try OAuthHelpers.callbackCode(target: "/other?code=x&state=known", state: "known")
      })
    check(
      "reject duplicate state",
      rejects {
        _ = try OAuthHelpers.callbackCode(
          target: "/oauth/callback?code=x&state=known&state=evil", state: "known")
      })
    check(
      "OAuth cancellation",
      rejects {
        _ = try OAuthHelpers.callbackCode(
          target: "/oauth/callback?error=access_denied&state=known", state: "known")
      })
    check(
      "form escaping",
      String(data: OAuthHelpers.form(["a": "x+y&z=1"]), encoding: .utf8) == "a=x%2By%26z%3D1")
    check(
      "desktop client only",
      rejects {
        _ = try OAuthConfig.parse(
          Data("{\"web\":{\"client_id\":\"test.apps.googleusercontent.com\"}}".utf8))
      })
    check(
      "desktop client parsing",
      (try? OAuthConfig.parse(
        Data("{\"installed\":{\"client_id\":\"test.apps.googleusercontent.com\"}}".utf8)))?.clientID
        == "test.apps.googleusercontent.com")
    check(
      "untrusted link rejected",
      httpsURL("javascript:alert(1)") == nil && httpsURL("file:///tmp/a") == nil
        && httpsURL("https://user:password@example.com") == nil)
    check("https link accepted", httpsURL("https://meet.google.com/aaa-bbbb-ccc") != nil)
    let id = "12345678-1234-1234-1234-123456789abc"
    let claude: [[String: Any]] = [
      ["type": "user", "cwd": "/tmp/Project", "message": ["content": "검토 요청"]],
      ["type": "assistant", "message": ["stop_reason": "end_turn", "content": []]],
    ]
    let entry = LocalProviders.parseClaude(claude, id: id, modified: 1)
    check("Claude end turn is completed", entry?.status == "응답 완료" && entry?.title == "검토 요청")
    let toolUse: [[String: Any]] = [["type": "assistant", "message": ["stop_reason": "tool_use"]]]
    check(
      "Claude uncertain is not running",
      LocalProviders.parseClaude(toolUse, id: id, modified: Date().timeIntervalSince1970)?.status
        == "최근 기록")
    check(
      "reject malformed session",
      LocalProviders.parseClaude(claude, id: "$(whoami)", modified: 1) == nil)
    let renamed = claude + [["type": "custom-title", "customTitle": "새 이름"]]
    check(
      "Claude custom title",
      LocalProviders.parseClaude(renamed, id: id, modified: 1)?.title == "새 이름")
    let adversarial = AgentEntry(
      id: "x", sessionID: id, title: "", provider: "claude", updated: 0, status: "", project: "",
      cwd: "/tmp/a'$(touch nope)")
    check(
      "shell argument quoting",
      resumeCommand(adversarial, executable: "/bin/claude")?.contains("'/tmp/a'\\''$(touch nope)'")
        == true)
    var invalid = adversarial
    invalid.sessionID = "x;exit"
    check("reject resume injection", resumeCommand(invalid, executable: "/bin/claude") == nil)
    let event: [String: Any] = [
      "id": "event", "summary": "<script>test</script>",
      "start": ["dateTime": "2026-09-05T10:00:00+09:00"],
      "end": ["dateTime": "2026-09-05T11:00:00+09:00"], "hangoutLink": "javascript:bad",
    ]
    let e = GoogleClient.parseEvent(event, calendarID: "c", calendar: "Calendar")
    check("event timezone", e?.start == isoDate("2026-09-05T01:00:00Z")?.timeIntervalSince1970)
    check("event malicious meeting rejected", e?.meetingURL == "")
    let allDay = GoogleClient.parseEvent(
      ["id": "a", "start": ["date": "2026-09-05"], "end": ["date": "2026-09-06"]], calendarID: "c",
      calendar: "C")
    check("all day exclusive end", allDay?.allDay == true && (allDay!.end - allDay!.start) == 86400)
    var cancelled = event
    cancelled["status"] = "cancelled"
    check(
      "cancelled event omitted",
      GoogleClient.parseEvent(cancelled, calendarID: "c", calendar: "C") == nil)
    var declined = event
    declined["attendees"] = [["self": true, "responseStatus": "declined"]]
    check(
      "declined self omitted",
      GoogleClient.parseEvent(declined, calendarID: "c", calendar: "C") == nil)
    let task = GoogleClient.parseTask(
      ["id": "a", "title": "할 일", "due": "2026-09-05T00:00:00.000Z", "status": "completed"],
      listID: "l", list: "목록")
    check("Tasks due remains calendar date", task?.due == "2026-09-05" && task?.completed == true)
    check(
      "deleted task omitted",
      GoogleClient.parseTask(["id": "a", "deleted": true], listID: "l", list: "L") == nil)
    do {
      let client = GoogleClient(
        loadKeychain: false,
        transport: { req in
          guard req.httpMethod == "PATCH", req.url?.host == "www.googleapis.com",
            req.value(forHTTPHeaderField: "Authorization") == "Bearer test-only"
          else { throw OrbitError(message: "Bad request fixture") }
          let payload = try JSONSerialization.jsonObject(with: req.httpBody!) as! [String: Any]
          guard payload["status"] as? String == "needsAction", payload["completed"] is NSNull else {
            throw OrbitError(message: "Bad patch fixture")
          }
          return (
            Data("{}".utf8),
            HTTPURLResponse(url: req.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!
          )
        })
      _ = try await client.authorizedRequest(
        path: "/tasks/v1/lists/l/tasks/t", method: "PATCH",
        body: ["status": "needsAction", "completed": NSNull()], token: "test-only")
      check("task reopen PATCH and no token URL", true)
      let fail = GoogleClient(
        loadKeychain: false,
        transport: { req in
          (
            Data(),
            HTTPURLResponse(url: req.url!, statusCode: 403, httpVersion: nil, headerFields: nil)!
          )
        })
      do {
        _ = try await fail.authorizedRequest(path: "/tasks/v1/test", token: "test-only")
        check("403 propagates", false)
      } catch { check("403 propagates", error.localizedDescription.contains("권한")) }
    } catch { check("HTTP fixtures", false) }
    do {
      let fm = FileManager.default
      let root = fm.temporaryDirectory.appendingPathComponent("orbit-test-" + UUID().uuidString)
      try fm.createDirectory(at: root, withIntermediateDirectories: true)
      defer { try? fm.removeItem(at: root) }
      try Data("fixture".utf8).write(to: root.appendingPathComponent("한글 문서.md"))
      try Data().write(to: root.appendingPathComponent("ignore.exe"))
      try fm.createSymbolicLink(
        at: root.appendingPathComponent("outside.md"),
        withDestinationURL: URL(fileURLWithPath: "/etc/hosts"))
      let files = try LocalProviders.recentFiles(path: root.path, source: "obsidian").0
      check(
        "metadata scan filters symlinks/extensions",
        files.count == 1 && files.first?.name == "한글 문서.md")
      let dbPath = root.appendingPathComponent("fixture.sqlite").path
      var db: OpaquePointer?
      sqlite3_open(dbPath, &db)
      sqlite3_exec(
        db, "CREATE TABLE items (name TEXT); INSERT INTO items VALUES ('한글');", nil, nil, nil)
      sqlite3_exec(db,"""
        CREATE TABLE threads (id TEXT,name TEXT,title TEXT,cwd TEXT,updated_at INTEGER,recency_at INTEGER,archived INTEGER,source TEXT,thread_source TEXT,agent_path TEXT);
        INSERT INTO threads VALUES ('user','Renamed','Original','/tmp',1,1,0,'vscode','user',NULL);
        INSERT INTO threads VALUES ('guardian',NULL,'Internal','/tmp',9,9,0,'exec','guardian_review',NULL);
        INSERT INTO threads VALUES ('child',NULL,'Child','/tmp',8,8,0,'exec','subagent',NULL);
        INSERT INTO threads VALUES ('archived',NULL,'Archived','/tmp',7,7,1,'vscode','user',NULL);
        INSERT INTO threads VALUES ('oldGuardian',NULL,'Internal','/tmp',6,6,0,'{"subagent":{"other":"guardian"}}',NULL,NULL);
        """,nil,nil,nil)
      sqlite3_close(db)
      let reader = try ReadDB(dbPath)
      check(
        "SQLite read-only Unicode", try reader.rows("SELECT name FROM items").first?["name"] == "한글"
      )
      check("SQLite writes rejected", rejects { _ = try reader.rows("DELETE FROM items") })
      let visible = try reader.rows(LocalProviders.codexQuery)
      check("hide internal and archived Codex sessions",visible.count==1 && visible.first?["id"]=="user")
      check("honor user-renamed Codex title",visible.first?["title"]=="Renamed")
    } catch { check("local fixture tests", false) }
    print("Orbit tests: \(passed) passed, \(failures.count) failed")
    for failure in failures { print("FAIL: \(failure)") }
    return failures.isEmpty
  }
}
