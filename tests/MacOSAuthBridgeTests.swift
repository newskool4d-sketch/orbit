// Compiled only by scripts/test-macos-auth-bridge.sh, with the production client and bridge.
// OAuth, Keychain, D-day storage and the app shell are memory-only test doubles.
import AppKit
import WebKit

struct OAuthConfig: Codable {
  var clientID: String
  var clientSecret: String
  static func parse(_ data: Data) throws -> Self { throw OrbitError(message: "Unused fixture action") }
}
@MainActor final class OAuthFlow {
  var active = false
  func cancel() {}
  func authorize(config: OAuthConfig) async throws -> (code: String, verifier: String, redirect: String) {
    ("fixture-code", "fixture-verifier", "http://127.0.0.1/callback")
  }
}
enum OAuthHelpers {
  static func form(_ values: [String: String]) -> Data {
    Data(values.sorted(by: { $0.key < $1.key }).map { "\($0.key)=\($0.value)" }.joined(separator: "&").utf8)
  }
}
enum KeychainStore {
  static var values: [String: Data] = [:]
  static var failWrites = false
  static func read(_ key: String) throws -> Data? { values[key] }
  static func write(_ data: Data, key: String) throws {
    if failWrites { throw OrbitError(message: "Fixture storage failure") }
    values[key] = data
  }
  static func remove(_ key: String) throws { values.removeValue(forKey: key) }
}
final class DdayStore {
  var items = [String]()
  var loadError = ""
  func add(title: String, targetDate: String, pinned: Bool) throws {}
  func update(id: String, title: String, targetDate: String, pinned: Bool) throws {}
  func archive(id: String, value: Bool) throws {}
  func delete(id: String) throws {}
}
@MainActor final class OrbitApp {
  var pinned = false
  let popover = NSPopover()
  func applyTheme(_ value: String) {}
  func setPinned(_ value: Bool) { pinned = value }
  func quit() {}
}

@MainActor final class AuthBridgeTests {
  var passed = 0
  var failures = [String]()

  func check(_ name: String, _ result: Bool) {
    if result { passed += 1 } else { failures.append(name) }
  }
  func seed() throws {
    KeychainStore.failWrites = false
    KeychainStore.values = [
      "google-client": try JSONEncoder().encode(OAuthConfig(clientID: "fixture-client", clientSecret: "fixture-secret")),
      "google-tokens": try JSONEncoder().encode(OAuthTokens(access: "fixture-access-a", refresh: "fixture-refresh-a", expires: Date().timeIntervalSince1970 + 3600)),
    ]
  }
  func response(_ request: URLRequest, _ body: [String: Any], status: Int = 200) throws -> (Data, HTTPURLResponse) {
    (try JSONSerialization.data(withJSONObject: body), HTTPURLResponse(url: request.url!, statusCode: status, httpVersion: nil, headerFields: nil)!)
  }

  func testAuth() async throws {
    for (name, extra) in [("missing", [String: Any]()), ("empty", ["refresh_token": ""])] {
      try seed()
      let before = KeychainStore.values["google-tokens"]
      var lastAuthorization = ""
      let client = GoogleClient(transport: { request in
        if request.url?.host == "oauth2.googleapis.com" {
          return try self.response(request, ["access_token": "fixture-access-b", "expires_in": 3600].merging(extra) { _, value in value })
        }
        lastAuthorization = request.value(forHTTPHeaderField: "Authorization") ?? ""
        return try self.response(request, [:])
      })
      var rejected = false
      do { try await client.connect() } catch { rejected = true }
      check("fresh auth rejects \(name) refresh token", rejected)
      check("rejected \(name) refresh preserves stored account", KeychainStore.values["google-tokens"] == before)
      _ = try await client.request(path: "/tasks/v1/users/@me/lists")
      check("rejected \(name) refresh preserves active account", lastAuthorization == "Bearer fixture-access-a")
    }

    try seed()
    var refreshGrant = ""
    var authorization = ""
    let switching = GoogleClient(transport: { request in
      let fields = String(data: request.httpBody ?? Data(), encoding: .utf8) ?? ""
      if fields.contains("grant_type=authorization_code") {
        return try self.response(request, ["access_token": "fixture-access-b", "refresh_token": "fixture-refresh-b", "expires_in": 0])
      }
      if fields.contains("grant_type=refresh_token") {
        refreshGrant = fields
        return try self.response(request, ["access_token": "fixture-refreshed-b", "expires_in": 3600])
      }
      authorization = request.value(forHTTPHeaderField: "Authorization") ?? ""
      return try self.response(request, [:])
    })
    try await switching.connect()
    _ = try await switching.request(path: "/tasks/v1/users/@me/lists")
    check("new account refresh does not use prior account", refreshGrant.contains("refresh_token=fixture-refresh-b") && !refreshGrant.contains("fixture-refresh-a"))
    check("new account API uses refreshed account", authorization == "Bearer fixture-refreshed-b")
    let stored = try JSONDecoder().decode(OAuthTokens.self, from: KeychainStore.values["google-tokens"]!)
    check("refresh grant retains omitted refresh token", stored.refresh == "fixture-refresh-b")

    try seed()
    let beforeFailure = KeychainStore.values["google-tokens"]
    var afterFailureAuthorization = ""
    let failing = GoogleClient(transport: { request in
      if request.url?.host == "oauth2.googleapis.com" {
        return try self.response(request, ["access_token": "fixture-access-b", "refresh_token": "fixture-refresh-b"])
      }
      afterFailureAuthorization = request.value(forHTTPHeaderField: "Authorization") ?? ""
      return try self.response(request, [:])
    })
    KeychainStore.failWrites = true
    var failedSave = false
    do { try await failing.connect() } catch { failedSave = true }
    KeychainStore.failWrites = false
    _ = try await failing.request(path: "/tasks/v1/users/@me/lists")
    check("failed credential save preserves account", failedSave && KeychainStore.values["google-tokens"] == beforeFailure && afterFailureAuthorization == "Bearer fixture-access-a")
    KeychainStore.values = [:]
  }

  func waitForReply(_ bridge: Bridge, count: Int) async -> Bool {
    for _ in 0..<80 {
      try? await Task.sleep(nanoseconds: 50_000_000)
      guard !bridge.webView.isLoading,
        let value = try? await bridge.webView.evaluateJavaScript("typeof pending !== 'undefined' && pending.size === 0 && window.__fixtureReplies.length === \(count)") as? Bool
      else { continue }
      if value { return true }
    }
    return false
  }

  func testBridge() async {
    // Volatile arguments override preferences only in this test process; no discovery or settings writes.
    UserDefaults.standard.setVolatileDomain(["drive": "", "vault": "", "theme": "moss", "textSize": "normal"], forName: UserDefaults.argumentDomain)
    let app = OrbitApp()
    let bridge = Bridge(app: app)
    bridge.webView.configuration.userContentController.addUserScript(WKUserScript(source: """
      window.__fixtureReplies = [];
      let fixtureOrbit;
      Object.defineProperty(window, 'orbit', {
        get() { return fixtureOrbit; },
        set(value) {
          const receive = value.receive;
          value.receive = message => {
            if (message.kind === 'reply') window.__fixtureReplies.push(message.request);
            return receive(message);
          };
          fixtureOrbit = value;
        }
      });
      """, injectionTime: .atDocumentStart, forMainFrameOnly: true))
    bridge.load()
    let initial = await waitForReply(bridge, count: 1)
    check("initial ready acknowledged exactly once without pending request", initial)
    if initial {
      _ = try? await bridge.webView.evaluateJavaScript("act('ready')")
      let repeated = await waitForReply(bridge, count: 2)
      check("ready after navigation finish acknowledged exactly once", repeated)
    } else { check("ready after navigation finish acknowledged exactly once", false) }
    // Exercise the production WebContent recovery delegate without killing a system process.
    bridge.webViewWebContentProcessDidTerminate(bridge.webView)
    let reloaded = await waitForReply(bridge, count: 1)
    check("reload resets and acknowledges ready without pending request", reloaded)
    let window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 560, height: 700), styleMask: [.titled], backing: .buffered, defer: false)
    window.title = "Orbit synthetic regression checks"
    window.contentView = bridge.webView
    window.makeKeyAndOrderFront(nil)
    NSApp.activate(ignoringOtherApps: true)
    defer { window.orderOut(nil) }
    for _ in 0..<20 {
      if (try? await bridge.webView.evaluateJavaScript("!document.hidden")) as? Bool == true { break }
      try? await Task.sleep(nanoseconds: 50_000_000)
    }
    do {
      let fixture = try String(contentsOfFile: CommandLine.arguments[1], encoding: .utf8)
      let result = try await bridge.webView.callAsyncJavaScript(
        "return await " + fixture, arguments: [:], in: nil, contentWorld: .page)
      guard let cases = result as? [[String: Any]] else { throw OrbitError(message: "Invalid UI fixture result") }
      for item in cases { check(item["name"] as? String ?? "UI fixture", item["passed"] as? Bool == true) }
    } catch { failures.append("UI focus fixture execution") }
  }

  func run() async {
    do { try await testAuth() } catch { failures.append("OAuth fixture execution") }
    await testBridge()
    print("Orbit auth/bridge tests: \(passed) passed, \(failures.count) failed")
    for failure in failures { print("FAIL: \(failure)") }
    exit(failures.isEmpty ? 0 : 1)
  }
}

@main struct TestMain {
  @MainActor static func main() {
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    let tests = AuthBridgeTests()
    Task { await tests.run() }
    DispatchQueue.main.asyncAfter(deadline: .now() + 30) { print("FAIL: native fixture timeout"); exit(2) }
    app.run()
  }
}
