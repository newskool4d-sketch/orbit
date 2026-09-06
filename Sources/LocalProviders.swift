import Foundation
import SQLite3

final class ReadDB {
  var db: OpaquePointer?
  init(_ path: String) throws {
    guard sqlite3_open_v2(path, &db, SQLITE_OPEN_READONLY | SQLITE_OPEN_FULLMUTEX, nil) == SQLITE_OK
    else {
      if db != nil {
        sqlite3_close(db)
        db = nil
      }
      throw OrbitError(message: "로컬 작업 저장소를 읽을 수 없습니다.")
    }
    sqlite3_busy_timeout(db, 1000)
  }
  deinit { sqlite3_close(db) }
  func rows(_ sql: String, bindings: [String] = []) throws -> [[String: String]] {
    var stmt: OpaquePointer?
    guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else {
      throw OrbitError(message: "작업 저장소 형식이 변경되었습니다.")
    }
    defer { sqlite3_finalize(stmt) }
    for (i, b) in bindings.enumerated() {
      sqlite3_bind_text(
        stmt, Int32(i + 1), b, -1, unsafeBitCast(-1, to: sqlite3_destructor_type.self))
    }
    var result = [[String: String]]()
    var code = sqlite3_step(stmt)
    while code == SQLITE_ROW {
      var row = [String: String]()
      for i in 0..<sqlite3_column_count(stmt) {
        if let val = sqlite3_column_text(stmt, i) {
          row[String(cString: sqlite3_column_name(stmt, i))] = String(cString: val)
        }
      }
      result.append(row)
      code = sqlite3_step(stmt)
    }
    guard code == SQLITE_DONE else { throw OrbitError(message: "작업 저장소가 사용 중입니다. 잠시 후 다시 시도합니다.") }
    return result
  }
}

enum LocalProviders {
  static let codexQuery = "SELECT id,COALESCE(NULLIF(name,''),title) AS title,cwd,updated_at,recency_at FROM threads WHERE archived=0 AND source IN ('cli','vscode','exec') AND (thread_source IS NULL OR thread_source='user') AND (agent_path IS NULL OR agent_path='/root') ORDER BY recency_at DESC LIMIT 24"
  static func scan(drive: String, vault: String, codex: String, claude: String) -> LocalSnapshot {
    var snapshot = LocalSnapshot()
    let now = Date().timeIntervalSince1970
    for (source, path) in [("drive", drive), ("obsidian", vault)] {
      guard !path.isEmpty else {
        snapshot.statuses.append(
          .init(source: source, state: "disconnected", message: "폴더를 선택하세요.", updated: 0))
        continue
      }
      do {
        let (files, limited) = try recentFiles(path: path, source: source)
        snapshot.files += files
        snapshot.statuses.append(
          .init(
            source: source, state: "connected",
            message: limited ? "최근 수정 파일 · 일부 범위만 검색" : "최근 수정 파일", updated: now))
      } catch {
        snapshot.statuses.append(
          .init(source: source, state: "error", message: "폴더 접근 권한 또는 동기화 상태를 확인하세요.", updated: 0))
      }
    }
    let vaultPaths = Set(snapshot.files.filter { $0.source == "obsidian" }.map { $0.path })
    snapshot.files = snapshot.files.filter {
      $0.source == "obsidian" || !vaultPaths.contains($0.path)
    }.sorted { $0.modified > $1.modified }
    do {
      snapshot.agents += try codexSessions(root: codex)
      snapshot.statuses.append(
        .init(source: "codex", state: "connected", message: "로컬 작업 기록", updated: now))
    } catch {
      snapshot.statuses.append(
        .init(source: "codex", state: "error", message: error.localizedDescription, updated: 0))
    }
    do {
      snapshot.agents += try claudeSessions(root: claude)
      snapshot.statuses.append(
        .init(source: "claude", state: "connected", message: "Claude Code 로컬 세션", updated: now))
    } catch {
      snapshot.statuses.append(
        .init(
          source: "claude", state: "error", message: "Claude Code 로컬 세션을 읽을 수 없습니다.", updated: 0))
    }
    snapshot.agents.sort { $0.updated > $1.updated }
    return snapshot
  }
  static func recentFiles(path: String, source: String) throws -> ([FileEntry], Bool) {
    let fm = FileManager.default
    let root = URL(fileURLWithPath: path).resolvingSymlinksInPath()
    _ = try fm.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)
    let keys: Set<URLResourceKey> = [
      .isRegularFileKey, .isDirectoryKey, .isSymbolicLinkKey, .contentModificationDateKey,
      .isPackageKey,
    ]
    guard
      let items = fm.enumerator(
        at: root, includingPropertiesForKeys: Array(keys),
        options: [.skipsHiddenFiles, .skipsPackageDescendants])
    else { throw OrbitError(message: "폴더를 읽을 수 없습니다.") }
    let allowed: Set<String> = [
      "md", "pdf", "hwpx", "hwp", "docx", "xlsx", "pptx", "txt", "csv", "html", "gdoc", "gsheet",
      "gslides",
    ]
    var files = [FileEntry]()
    var count = 0
    var limited = false
    let deadline = Date().addingTimeInterval(4)
    for case let url as URL in items {
      count += 1
      if count > 20000 || Date() > deadline {
        limited = true
        break
      }
      if items.level > 7 {
        items.skipDescendants()
        limited = true
        continue
      }
      guard let v = try? url.resourceValues(forKeys: keys) else { continue }
      if v.isSymbolicLink == true {
        items.skipDescendants()
        continue
      }
      if v.isDirectory == true {
        if ["node_modules", "build", "dist", "vendor"].contains(url.lastPathComponent) {
          items.skipDescendants()
        }
        continue
      }
      guard v.isRegularFile == true, allowed.contains(url.pathExtension.lowercased()) else {
        continue
      }
      let stamp = v.contentModificationDate?.timeIntervalSince1970 ?? 0
      files.append(
        .init(
          id: UUID().uuidString, name: url.lastPathComponent, source: source, modified: stamp,
          parent: url.deletingLastPathComponent().lastPathComponent, path: url.path))
      if files.count > 100 {
        files.sort { $0.modified > $1.modified }
        files = Array(files.prefix(40))
      }
    }
    return (Array(files.sorted { $0.modified > $1.modified }.prefix(30)), limited)
  }
  static func codexSessions(root: String) throws -> [AgentEntry] {
    let dir = URL(fileURLWithPath: root)
    let db = try ReadDB(dir.appendingPathComponent("state_5.sqlite").path)
    let rows = try db.rows(
      codexQuery
    )
    let history = try? ReadDB(dir.appendingPathComponent("thread_history_1.sqlite").path)
    return rows.compactMap { r in
      guard let id = r["id"], UUID(uuidString: id) != nil else { return nil }
      let turn =
        (try? history?.rows(
          "SELECT status,started_at,completed_at FROM thread_turns WHERE thread_id=? ORDER BY rollout_ordinal DESC LIMIT 1",
          bindings: [id]))?.first
      let updated = Double(r["updated_at"] ?? "0") ?? 0
      let recent = max(updated, Double(r["recency_at"] ?? "0") ?? 0)
      let status: String
      switch turn?["status"] {
      case "completed": status = "응답 완료"
      case "inProgress": status = Date().timeIntervalSince1970 - updated < 600 ? "진행 중" : "상태 확인 필요"
      case "failed": status = "오류"
      case "interrupted": status = "중단됨"
      default: status = "최근 기록"
      }
      let cwd = r["cwd"] ?? ""
      let title = r["title"] ?? ""
      return AgentEntry(
        id: "codex:" + id, sessionID: id,
        title: title.isEmpty ? "이름 없는 작업" : String(title.prefix(160)), provider: "codex",
        updated: recent, status: status, project: URL(fileURLWithPath: cwd).lastPathComponent,
        cwd: cwd)
    }
  }
  static func claudeSessions(root: String) throws -> [AgentEntry] {
    let projects = URL(fileURLWithPath: root).appendingPathComponent("projects")
    let fm = FileManager.default
    let dirs = try fm.contentsOfDirectory(
      at: projects, includingPropertiesForKeys: [.isDirectoryKey], options: .skipsHiddenFiles)
    var candidates = [URL]()
    for dir in dirs.prefix(200) {
      candidates +=
        ((try? fm.contentsOfDirectory(
          at: dir, includingPropertiesForKeys: [.contentModificationDateKey],
          options: .skipsHiddenFiles)) ?? []).filter {
          $0.pathExtension == "jsonl"
            && UUID(uuidString: $0.deletingPathExtension().lastPathComponent) != nil
        }
    }
    candidates.sort { modified($0) > modified($1) }
    return candidates.prefix(24).compactMap { url in
      guard let handle = try? FileHandle(forReadingFrom: url) else { return nil }
      defer { try? handle.close() }
      let size = (try? handle.seekToEnd()) ?? 0
      try? handle.seek(toOffset: 0)
      let head = (try? handle.read(upToCount: 131072)) ?? Data()
      var chunks = [head]
      if size > 131072 {
        try? handle.seek(toOffset: max(131072, size > 262144 ? size - 262144 : 131072))
        chunks.append((try? handle.read(upToCount: 262144)) ?? Data())
      }
      let records = chunks.flatMap { data in
        String(decoding: data, as: UTF8.self).split(separator: "\n").compactMap {
          (try? JSONSerialization.jsonObject(with: Data($0.utf8))) as? [String: Any]
        }
      }
      return parseClaude(
        records, id: url.deletingPathExtension().lastPathComponent, modified: modified(url))
    }
  }
  static func modified(_ url: URL) -> Double {
    (try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate?
      .timeIntervalSince1970 ?? 0
  }
  static func parseClaude(_ records: [[String: Any]], id: String, modified: Double) -> AgentEntry? {
    guard UUID(uuidString: id) != nil else { return nil }
    var title = ""
    var named = ""
    var cwd = ""
    var stop = ""
    var lastType = ""
    var stamp = modified
    for r in records where r["isSidechain"] as? Bool != true {
      if let c = r["cwd"] as? String { cwd = c }
      let type = r["type"] as? String ?? ""
      if type == "custom-title", let t = r["customTitle"] as? String { named = t }
      if type == "summary", let t = r["summary"] as? String, title.isEmpty { title = t }
      if let message = r["message"] as? [String: Any] {
        if type == "user", r["isMeta"] as? Bool != true, let text = message["content"] as? String,
          !text.hasPrefix("<"), !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
          title.isEmpty
        {
          title = text
        }
        if type == "assistant" || type == "user" {
          lastType = type
          stop = message["stop_reason"] as? String ?? ""
        }
      }
      if let timestamp = r["timestamp"] as? String, let d = isoDate(timestamp) {
        stamp = max(stamp, d.timeIntervalSince1970)
      }
    }
    if !named.isEmpty { title = named }
    if title.isEmpty { title = "Claude Code 세션" }
    title = String(title.replacingOccurrences(of: "\n", with: " ").prefix(160))
    let status =
      (lastType == "assistant" && ["end_turn", "stop_sequence"].contains(stop)) ? "응답 완료" : "최근 기록"
    return .init(
      id: "claude:" + id, sessionID: id, title: title, provider: "claude", updated: stamp,
      status: status, project: cwd.isEmpty ? "로컬 세션" : URL(fileURLWithPath: cwd).lastPathComponent,
      cwd: cwd)
  }
}
