import Foundation

struct OAuthTokens: Codable {
  var access: String
  var refresh: String
  var expires: Double
}
typealias HTTPTransport = (URLRequest) async throws -> (Data, HTTPURLResponse)

@MainActor final class GoogleClient {
  let oauth = OAuthFlow()
  private var tokens: OAuthTokens?
  private var config: OAuthConfig?
  private var credentialGeneration = 0
  var loadError = ""
  var transport: HTTPTransport
  var configured: Bool { config != nil }
  var connected: Bool { tokens != nil }
  var authorizing: Bool { oauth.active }
  init(loadKeychain: Bool = true, transport: HTTPTransport? = nil) {
    self.transport =
      transport ?? { request in
        let (d, r) = try await URLSession.shared.data(for: request)
        guard let http = r as? HTTPURLResponse else {
          throw OrbitError(message: "Google 응답을 확인할 수 없습니다.")
        }
        return (d, http)
      }
    if loadKeychain {
      do {
        if let d = try KeychainStore.read("google-client") {
          config = try JSONDecoder().decode(OAuthConfig.self, from: d)
        }
        if let d = try KeychainStore.read("google-tokens") {
          tokens = try JSONDecoder().decode(OAuthTokens.self, from: d)
        }
      } catch { loadError = error.localizedDescription }
    }
  }
  func importConfig(_ data: Data) throws {
    guard !authorizing else { throw OrbitError(message: "진행 중인 Google 연결을 먼저 취소하세요.") }
    let new = try OAuthConfig.parse(data)
    if config?.clientID != new.clientID { try disconnect() }
    try KeychainStore.write(JSONEncoder().encode(new), key: "google-client")
    config = new
    loadError = ""
  }
  func connect() async throws {
    guard let config = config else {
      throw OrbitError(message: "먼저 Google 데스크톱 OAuth JSON을 가져오세요.")
    }
    let generation = credentialGeneration
    let auth = try await oauth.authorize(config: config)
    let response = try await exchange(
      [
        "grant_type": "authorization_code", "code": auth.code, "code_verifier": auth.verifier,
        "redirect_uri": auth.redirect,
      ], config: config)
    guard generation == credentialGeneration else {
      throw OrbitError(message: "Google 연결 요청이 취소되었습니다.")
    }
    try saveTokens(response)
  }
  func disconnect() throws {
    credentialGeneration += 1
    oauth.cancel()
    try KeychainStore.remove("google-tokens")
    tokens = nil
  }
  private func saveTokens(_ response: [String: Any]) throws {
    guard let access = response["access_token"] as? String, !access.isEmpty else {
      throw OrbitError(message: "Google 인증 토큰을 받지 못했습니다.")
    }
    let refresh = response["refresh_token"] as? String ?? tokens?.refresh ?? ""
    guard !refresh.isEmpty else {
      throw OrbitError(message: "자동 갱신 권한을 받지 못했습니다. Google 연결을 다시 진행하세요.")
    }
    let new = OAuthTokens(
      access: access, refresh: refresh,
      expires: Date().timeIntervalSince1970 + (response["expires_in"] as? Double ?? 3600))
    try KeychainStore.write(JSONEncoder().encode(new), key: "google-tokens")
    tokens = new
  }
  private func exchange(_ fields: [String: String], config: OAuthConfig) async throws -> [String:
    Any]
  {
    var values = fields
    values["client_id"] = config.clientID
    if !config.clientSecret.isEmpty { values["client_secret"] = config.clientSecret }
    var req = URLRequest(url: URL(string: "https://oauth2.googleapis.com/token")!)
    req.httpMethod = "POST"
    req.timeoutInterval = 20
    req.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
    req.httpBody = OAuthHelpers.form(values)
    let (data, http) = try await transport(req)
    guard http.statusCode == 200 else {
      throw OrbitError(
        message: http.statusCode == 400
          ? "Google 인증이 만료되었거나 클라이언트 설정이 올바르지 않습니다. 다시 연결하세요."
          : "Google 인증 서버 응답 오류 (\(http.statusCode))")
    }
    guard let body = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else {
      throw OrbitError(message: "Google 인증 응답 형식이 올바르지 않습니다.")
    }
    return body
  }
  private func accessToken() async throws -> String {
    guard let token = tokens, let config = config else {
      throw OrbitError(message: "Google 계정을 연결하세요.")
    }
    if token.expires > Date().timeIntervalSince1970 + 90 { return token.access }
    let generation = credentialGeneration
    let response = try await exchange(
      ["grant_type": "refresh_token", "refresh_token": token.refresh], config: config)
    guard generation == credentialGeneration else {
      throw OrbitError(message: "Google 연결이 해제되었습니다.")
    }
    try saveTokens(response)
    return tokens!.access
  }
  func request(
    path: String, query: [URLQueryItem] = [], method: String = "GET", body: [String: Any]? = nil
  ) async throws -> [String: Any] {
    let token = try await accessToken()
    return try await authorizedRequest(
      path: path, query: query, method: method, body: body, token: token)
  }
  func authorizedRequest(
    path: String, query: [URLQueryItem] = [], method: String = "GET", body: [String: Any]? = nil,
    token: String
  ) async throws -> [String: Any] {
    guard path.hasPrefix("/calendar/v3/") || path.hasPrefix("/tasks/v1/") else {
      throw OrbitError(message: "지원하지 않는 Google 요청입니다.")
    }
    var c = URLComponents()
    c.scheme = "https"
    c.host = "www.googleapis.com"
    c.path = path
    c.queryItems = query.isEmpty ? nil : query
    guard let url = c.url else { throw OrbitError(message: "Google 요청 주소가 올바르지 않습니다.") }
    var req = URLRequest(url: url)
    req.httpMethod = method
    req.timeoutInterval = 20
    req.setValue("Bearer " + token, forHTTPHeaderField: "Authorization")
    if let body = body {
      req.setValue("application/json", forHTTPHeaderField: "Content-Type")
      req.httpBody = try JSONSerialization.data(withJSONObject: body)
    }
    let (data, http) = try await transport(req)
    guard (200..<300).contains(http.statusCode) else {
      let message: String
      switch http.statusCode {
      case 401: message = "Google 로그인이 만료되었습니다. 다시 연결하세요."
      case 403: message = "Google API 권한을 확인하세요. Calendar·Tasks API 활성화 또는 재연결이 필요합니다."
      case 429: message = "Google 요청 한도를 잠시 초과했습니다. 잠시 후 새로고침하세요."
      default: message = "Google 응답 오류 (\(http.statusCode)). 잠시 후 다시 시도하세요."
      }
      throw OrbitError(message: message)
    }
    if data.isEmpty { return [:] }
    guard let json = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else {
      throw OrbitError(message: "Google 응답 형식이 올바르지 않습니다.")
    }
    return json
  }
  func pages(path: String, query: [URLQueryItem] = []) async throws -> [[String: Any]] {
    var results = [[String: Any]]()
    var page: String?
    var seen = Set<String>()
    for _ in 0..<100 {
      var q = query
      if let p = page { q.append(.init(name: "pageToken", value: p)) }
      let json = try await request(path: path, query: q)
      results += json["items"] as? [[String: Any]] ?? []
      page = json["nextPageToken"] as? String
      guard let p = page, !p.isEmpty else { return results }
      guard seen.insert(p).inserted else { throw OrbitError(message: "Google 페이지 응답이 반복되었습니다.") }
    }
    throw OrbitError(message: "목록이 매우 큽니다. Google에서 목록 범위를 정리한 후 다시 시도하세요.")
  }
  func events() async throws -> [CalendarEntry] {
    let calendars = try await pages(
      path: "/calendar/v3/users/me/calendarList", query: [.init(name: "maxResults", value: "250")])
    let today = Calendar.current.startOfDay(for: Date())
    let until = Calendar.current.date(byAdding: .day, value: 7, to: today)!
    let f = ISO8601DateFormatter()
    var result = [CalendarEntry]()
    for calendar in calendars
    where calendar["selected"] as? Bool != false && calendar["hidden"] as? Bool != true {
      guard let id = calendar["id"] as? String else { continue }
      let rows = try await pages(
        path: "/calendar/v3/calendars/\(id)/events",
        query: [
          .init(name: "timeMin", value: f.string(from: today)),
          .init(name: "timeMax", value: f.string(from: until)),
          .init(name: "singleEvents", value: "true"), .init(name: "orderBy", value: "startTime"),
          .init(name: "maxResults", value: "250"),
        ])
      result += rows.compactMap {
        Self.parseEvent(
          $0, calendarID: id, calendar: calendar["summary"] as? String ?? "Google Calendar")
      }
    }
    return result.sorted { $0.start < $1.start }
  }
  static func parseEvent(_ r: [String: Any], calendarID: String, calendar: String) -> CalendarEntry?
  {
    guard r["status"] as? String != "cancelled", let id = r["id"] as? String,
      let start = r["start"] as? [String: String], let end = r["end"] as? [String: String]
    else { return nil }
    if let people = r["attendees"] as? [[String: Any]],
      people.contains(where: {
        $0["self"] as? Bool == true && $0["responseStatus"] as? String == "declined"
      })
    {
      return nil
    }
    let allDay = start["date"] != nil
    guard let s = allDay ? dayDate(start["date"] ?? "") : isoDate(start["dateTime"] ?? ""),
      let e = allDay ? dayDate(end["date"] ?? "") : isoDate(end["dateTime"] ?? "")
    else { return nil }
    var meeting = r["hangoutLink"] as? String ?? ""
    if meeting.isEmpty, let conf = r["conferenceData"] as? [String: Any],
      let endpoints = conf["entryPoints"] as? [[String: Any]]
    {
      meeting =
        endpoints.first(where: { $0["entryPointType"] as? String == "video" })?["uri"] as? String
        ?? ""
    }
    return .init(
      id: calendarID + ":" + id, title: r["summary"] as? String ?? "제목 없는 일정",
      start: s.timeIntervalSince1970, end: e.timeIntervalSince1970, allDay: allDay,
      url: r["htmlLink"] as? String ?? "https://calendar.google.com",
      meetingURL: httpsURL(meeting) == nil ? "" : meeting, calendar: calendar)
  }
  func tasks() async throws -> [TaskEntry] {
    let lists = try await pages(
      path: "/tasks/v1/users/@me/lists", query: [.init(name: "maxResults", value: "100")])
    var result = [TaskEntry]()
    for list in lists {
      guard let id = list["id"] as? String else { continue }
      let rows = try await pages(
        path: "/tasks/v1/lists/\(id)/tasks",
        query: [
          .init(name: "maxResults", value: "100"), .init(name: "showCompleted", value: "true"),
          .init(name: "showHidden", value: "false"),
        ])
      result += rows.compactMap {
        Self.parseTask($0, listID: id, list: list["title"] as? String ?? "할 일")
      }
    }
    return result.sorted { a, b in
      if a.completed != b.completed { return !a.completed }
      return (a.due.isEmpty ? "9999" : a.due) < (b.due.isEmpty ? "9999" : b.due)
    }
  }
  static func parseTask(_ r: [String: Any], listID: String, list: String) -> TaskEntry? {
    guard let id = r["id"] as? String, r["deleted"] as? Bool != true, r["hidden"] as? Bool != true
    else { return nil }
    return .init(
      id: listID + ":" + id, taskID: id, listID: listID,
      title: r["title"] as? String ?? "제목 없는 할 일", list: list,
      due: String((r["due"] as? String ?? "").prefix(10)),
      completed: r["status"] as? String == "completed")
  }
  func complete(_ task: TaskEntry, value: Bool) async throws {
    var body: [String: Any] = ["status": value ? "completed" : "needsAction"]
    if !value { body["completed"] = NSNull() }
    _ = try await request(
      path: "/tasks/v1/lists/\(task.listID)/tasks/\(task.taskID)", method: "PATCH", body: body)
  }
}
