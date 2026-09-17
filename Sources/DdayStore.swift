import Foundation

struct DdayEntry: Codable {
  var id: String
  var title: String
  var targetDate: String
  var pinned: Bool
  var archived: Bool
  var createdAt: String
  var updatedAt: String
}

private struct DdayDocument: Codable {
  var schemaVersion = 1
  var items: [DdayEntry] = []
}

final class DdayStore {
  private(set) var items: [DdayEntry] = []
  private(set) var loadError = ""
  private let url: URL?
  private let encoder: JSONEncoder = {
    let value = JSONEncoder()
    value.outputFormatting = [.prettyPrinted, .sortedKeys]
    return value
  }()

  init(url: URL? = DdayStore.defaultURL, seed: [DdayEntry]? = nil) {
    self.url = url
    if let seed { items = seed } else { load() }
  }

  func add(title: String, targetDate: String, pinned: Bool) throws {
    let title = try Self.validTitle(title)
    try Self.validDate(targetDate)
    let now = ISO8601DateFormatter().string(from: Date())
    try change { next in
      next.append(.init(
        id: UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased(),
        title: title, targetDate: targetDate, pinned: pinned, archived: false,
        createdAt: now, updatedAt: now))
    }
  }

  func update(id: String, title: String, targetDate: String, pinned: Bool) throws {
    let title = try Self.validTitle(title)
    try Self.validDate(targetDate)
    try change { next in
      let index = try Self.index(id, in: next)
      next[index].title = title
      next[index].targetDate = targetDate
      next[index].pinned = pinned
      next[index].updatedAt = ISO8601DateFormatter().string(from: Date())
    }
  }

  func archive(id: String, value: Bool) throws {
    try change { next in
      let index = try Self.index(id, in: next)
      next[index].archived = value
      next[index].updatedAt = ISO8601DateFormatter().string(from: Date())
    }
  }

  func delete(id: String) throws {
    try change { next in next.remove(at: try Self.index(id, in: next)) }
  }

  private func change(_ edit: (inout [DdayEntry]) throws -> Void) throws {
    guard loadError.isEmpty else { throw OrbitError(message: "중요 날짜 저장 파일을 먼저 확인하세요.") }
    var next = items
    try edit(&next)
    try save(next)
    items = next
  }

  private func load() {
    guard let url, FileManager.default.fileExists(atPath: url.path) else { return }
    do {
      let document = try JSONDecoder().decode(DdayDocument.self, from: Data(contentsOf: url))
      guard document.schemaVersion == 1 else { throw OrbitError(message: "schema") }
      var ids = Set<String>()
      for item in document.items {
        guard Self.validID(item.id), ids.insert(item.id).inserted,
          try Self.validTitle(item.title) == item.title,
          ISO8601DateFormatter().date(from: item.createdAt) != nil,
          ISO8601DateFormatter().date(from: item.updatedAt) != nil
        else { throw OrbitError(message: "entry") }
        try Self.validDate(item.targetDate)
      }
      items = document.items
    } catch {
      items = []
      loadError = "중요 날짜 저장 파일을 읽지 못했습니다. 원본을 보존했으므로 파일을 확인하세요."
    }
  }

  private func save(_ next: [DdayEntry]) throws {
    guard let url else { return }
    do {
      try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
      try encoder.encode(DdayDocument(items: next)).write(to: url, options: .atomic)
    } catch {
      throw OrbitError(message: "중요 날짜를 저장하지 못했습니다. 저장 공간과 권한을 확인하고 다시 시도하세요.")
    }
  }

  static func validTitle(_ value: String) throws -> String {
    let title = value.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !title.isEmpty, title.utf16.count <= 120,
      title.unicodeScalars.allSatisfy({ !CharacterSet.controlCharacters.contains($0) })
    else { throw OrbitError(message: "제목은 1자 이상 120자 이하로 입력하세요.") }
    return title
  }

  static func validDate(_ value: String) throws {
    let parts = value.split(separator: "-", omittingEmptySubsequences: false)
    guard value.range(of: "^[0-9]{4}-[0-9]{2}-[0-9]{2}$", options: .regularExpression) != nil,
      parts.count == 3, let year = Int(parts[0]), (1...9999).contains(year),
      let month = Int(parts[1]), let day = Int(parts[2])
    else { throw OrbitError(message: "실제 존재하는 날짜를 선택하세요.") }
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(secondsFromGMT: 0)!
    guard let date = calendar.date(from: DateComponents(year: year, month: month, day: day)) else {
      throw OrbitError(message: "실제 존재하는 날짜를 선택하세요.")
    }
    let components = calendar.dateComponents([.year, .month, .day], from: date)
    guard components.year == year, components.month == month, components.day == day else {
      throw OrbitError(message: "실제 존재하는 날짜를 선택하세요.")
    }
  }

  private static func index(_ id: String, in items: [DdayEntry]) throws -> Int {
    guard validID(id), let index = items.firstIndex(where: { $0.id == id }) else {
      throw OrbitError(message: "중요 날짜 목록을 새로고침하세요.")
    }
    return index
  }
  private static func validID(_ value: String) -> Bool {
    value.count == 32 && value.allSatisfy { $0.isHexDigit }
  }
  static var defaultURL: URL {
    FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
      .appendingPathComponent("Orbit", isDirectory: true).appendingPathComponent("ddays.json")
  }
}
