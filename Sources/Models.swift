import Foundation

struct FileEntry: Codable {
  var id: String
  var name: String
  var source: String
  var modified: Double
  var parent: String
  var path: String  // Used only by native code. Public display omits this through display().
  func display() -> [String: Any] {
    ["id": id, "name": name, "source": source, "modified": modified, "parent": parent]
  }
}
struct AgentEntry: Codable {
  var id: String
  var sessionID: String
  var title: String
  var provider: String
  var updated: Double
  var status: String
  var project: String
  var cwd: String
  func display() -> [String: Any] {
    [
      "id": id, "title": title, "provider": provider, "updated": updated, "status": status,
      "project": project,
    ]
  }
}
struct CalendarEntry: Codable {
  var id: String
  var title: String
  var start: Double
  var end: Double
  var allDay: Bool
  var url: String
  var meetingURL: String
  var calendar: String
}
struct TaskEntry: Codable {
  var id: String
  var taskID: String
  var listID: String
  var title: String
  var list: String
  var due: String
  var completed: Bool
}
struct SourceStatus: Codable {
  var source: String
  var state: String
  var message: String
  var updated: Double
}
struct LocalSnapshot {
  var files: [FileEntry] = []
  var agents: [AgentEntry] = []
  var statuses: [SourceStatus] = []
}
struct OrbitError: LocalizedError {
  let message: String
  var errorDescription: String? { message }
}
func jsonObject<T: Encodable>(_ value: T) -> Any {
  (try? JSONSerialization.jsonObject(with: JSONEncoder().encode(value))) ?? NSNull()
}
func isoDate(_ text: String) -> Date? {
  let format = ISO8601DateFormatter()
  format.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
  if let d = format.date(from: text) { return d }
  format.formatOptions = [.withInternetDateTime]
  return format.date(from: text)
}
func dayDate(_ text: String, timeZone: TimeZone = .current) -> Date? {
  let f = DateFormatter()
  f.locale = Locale(identifier: "en_US_POSIX")
  f.timeZone = timeZone
  f.dateFormat = "yyyy-MM-dd"
  return f.date(from: String(text.prefix(10)))
}
func shellQuote(_ text: String) -> String {
  "'" + text.replacingOccurrences(of: "'", with: "'\\''") + "'"
}
func resumeCommand(_ entry: AgentEntry, executable: String) -> String? {
  guard UUID(uuidString: entry.sessionID) != nil, entry.cwd.hasPrefix("/"),
    !entry.cwd.contains("\n"), !entry.cwd.contains("\r")
  else { return nil }
  return "cd -- " + shellQuote(entry.cwd) + " && " + shellQuote(executable) + " --resume "
    + shellQuote(entry.sessionID)
}
func httpsURL(_ value: String) -> URL? {
  guard let u = URL(string: value), u.scheme == "https", u.host != nil, u.user == nil,
    u.password == nil
  else { return nil }
  return u
}

final class Preferences {
  let defaults = UserDefaults.standard
  var theme: String {
    get { defaults.string(forKey: "theme") ?? "moss" }
    set {
      if ["moss", "pearl", "cobalt"].contains(newValue) { defaults.set(newValue, forKey: "theme") }
    }
  }
  var drive: String {
    get { defaults.string(forKey: "drive") ?? Self.detectDrive() }
    set { defaults.set(newValue, forKey: "drive") }
  }
  var vault: String {
    get { defaults.string(forKey: "vault") ?? Self.detectVault() }
    set { defaults.set(newValue, forKey: "vault") }
  }
  var codex: String {
    FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex").path
  }
  var claude: String {
    FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude").path
  }
  static func detectVault() -> String {
    let p = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(
      "Library/Application Support/obsidian/obsidian.json")
    guard let d = try? Data(contentsOf: p),
      let o = (try? JSONSerialization.jsonObject(with: d)) as? [String: Any],
      let vaults = o["vaults"] as? [String: [String: Any]]
    else { return "" }
    let sorted = vaults.values.sorted { ($0["ts"] as? Double ?? 0) > ($1["ts"] as? Double ?? 0) }
    return (sorted.first(where: { $0["open"] as? Bool == true }) ?? sorted.first)?["path"]
      as? String ?? ""
  }
  static func detectDrive() -> String {
    let fm = FileManager.default
    let base = fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/CloudStorage")
    guard let accounts = try? fm.contentsOfDirectory(at: base, includingPropertiesForKeys: nil)
    else { return "" }
    for account in accounts where account.lastPathComponent.hasPrefix("GoogleDrive-") {
      for child in (try? fm.contentsOfDirectory(at: account, includingPropertiesForKeys: nil)) ?? []
      {
        let name = child.lastPathComponent.precomposedStringWithCanonicalMapping
        if name == "내 드라이브" || name == "My Drive" {
          let roots =
            (try? fm.contentsOfDirectory(at: child, includingPropertiesForKeys: nil)) ?? []
          return roots.first(where: {
            $0.lastPathComponent.precomposedStringWithCanonicalMapping.hasPrefix("04 인천광역시교육청학생교육원")
          })?.path ?? child.path
        }
      }
    }
    return ""
  }
}
