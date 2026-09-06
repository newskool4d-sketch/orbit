import AppKit
import CryptoKit
import Foundation
import Network
import Security

enum KeychainStore {
  static let service = "local.orbit.dashboard"
  static func read(_ key: String) throws -> Data? {
    let q: [String: Any] = [
      kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
      kSecAttrAccount as String: key, kSecReturnData as String: true,
      kSecMatchLimit as String: kSecMatchLimitOne,
    ]
    var out: CFTypeRef?
    let status = SecItemCopyMatching(q as CFDictionary, &out)
    if status == errSecItemNotFound { return nil }
    guard status == errSecSuccess else {
      throw OrbitError(message: "Orbit 키체인 접근을 허용해 주세요. (\(status))")
    }
    return out as? Data
  }
  static func write(_ data: Data, key: String) throws {
    let q: [String: Any] = [
      kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
      kSecAttrAccount as String: key,
    ]
    let update = SecItemUpdate(q as CFDictionary, [kSecValueData as String: data] as CFDictionary)
    if update == errSecSuccess { return }
    guard update == errSecItemNotFound else {
      throw OrbitError(message: "키체인 저장에 실패했습니다. (\(update))")
    }
    var insert = q
    insert[kSecValueData as String] = data
    insert[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
    let status = SecItemAdd(insert as CFDictionary, nil)
    guard status == errSecSuccess else { throw OrbitError(message: "키체인 저장에 실패했습니다. (\(status))") }
  }
  static func remove(_ key: String) throws {
    let status = SecItemDelete(
      [
        kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
        kSecAttrAccount as String: key,
      ] as CFDictionary)
    guard status == errSecSuccess || status == errSecItemNotFound else {
      throw OrbitError(message: "키체인 연결 해제에 실패했습니다.")
    }
  }
}
struct OAuthConfig: Codable {
  var clientID: String
  var clientSecret: String
  static func parse(_ data: Data) throws -> OAuthConfig {
    guard let json = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any],
      let installed = json["installed"] as? [String: Any],
      let id = installed["client_id"] as? String, id.hasSuffix(".apps.googleusercontent.com")
    else { throw OrbitError(message: "Google Cloud의 ‘데스크톱 앱’ OAuth 클라이언트 JSON을 선택하세요.") }
    return .init(clientID: id, clientSecret: installed["client_secret"] as? String ?? "")
  }
}
enum OAuthHelpers {
  static func base64url(_ data: Data) -> String {
    data.base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(
      of: "/", with: "_"
    ).replacingOccurrences(of: "=", with: "")
  }
  static func random() throws -> String {
    var bytes = [UInt8](repeating: 0, count: 32)
    guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else {
      throw OrbitError(message: "로그인 보안 값을 만들 수 없습니다.")
    }
    return base64url(Data(bytes))
  }
  static func challenge(_ verifier: String) -> String {
    base64url(Data(SHA256.hash(data: Data(verifier.utf8))))
  }
  static func form(_ values: [String: String]) -> Data {
    let allowed = CharacterSet(
      charactersIn: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~")
    return Data(
      values.sorted { $0.key < $1.key }.map {
        ($0.key.addingPercentEncoding(withAllowedCharacters: allowed) ?? "") + "="
          + ($0.value.addingPercentEncoding(withAllowedCharacters: allowed) ?? "")
      }.joined(separator: "&").utf8)
  }
  static func callbackCode(target: String, state: String) throws -> String {
    guard let c = URLComponents(string: "http://127.0.0.1" + target), c.path == "/oauth/callback"
    else { throw OrbitError(message: "로그인 응답 경로가 올바르지 않습니다.") }
    let items = c.queryItems ?? []
    guard items.filter({ $0.name == "state" }).count == 1,
      items.first(where: { $0.name == "state" })?.value == state
    else { throw OrbitError(message: "로그인 보안 확인에 실패했습니다. 다시 연결하세요.") }
    if items.contains(where: { $0.name == "error" }) {
      throw OrbitError(message: "Google 연결이 취소되었거나 거부되었습니다.")
    }
    guard items.filter({ $0.name == "code" }).count == 1,
      let code = items.first(where: { $0.name == "code" })?.value, !code.isEmpty
    else { throw OrbitError(message: "Google 로그인 코드가 없습니다.") }
    return code
  }
}

@MainActor final class OAuthFlow {
  typealias Authorization = (code: String, verifier: String, redirect: String)
  private var listener: NWListener?
  private var continuation: CheckedContinuation<Authorization, Error>?
  private var timeout: Task<Void, Never>?
  var active: Bool { continuation != nil }
  func cancel() { finish(.failure(OrbitError(message: "Google 연결을 취소했습니다."))) }
  func authorize(config: OAuthConfig) async throws -> (
    code: String, verifier: String, redirect: String
  ) {
    guard !active else { throw OrbitError(message: "브라우저에서 진행 중인 Google 연결을 완료하세요.") }
    let verifier = try OAuthHelpers.random()
    let state = try OAuthHelpers.random()
    let params = NWParameters.tcp
    params.requiredLocalEndpoint = .hostPort(host: "127.0.0.1", port: .any)
    let listener = try NWListener(using: params)
    self.listener = listener
    return try await withCheckedThrowingContinuation { cont in
      continuation = cont
      listener.stateUpdateHandler = { [weak self] status in
        Task { @MainActor in
          guard let self = self else { return }
          if case .ready = status, let port = listener.port {
            let redirect = "http://127.0.0.1:\(port.rawValue)/oauth/callback"
            var u = URLComponents(string: "https://accounts.google.com/o/oauth2/v2/auth")!
            let values = [
              "client_id": config.clientID, "redirect_uri": redirect, "response_type": "code",
              "scope":
                "https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/tasks",
              "code_challenge": OAuthHelpers.challenge(verifier), "code_challenge_method": "S256",
              "state": state, "access_type": "offline", "prompt": "consent",
            ]
            u.queryItems = values.sorted { $0.key < $1.key }.map {
              URLQueryItem(name: $0.key, value: $0.value)
            }
            guard let url = u.url, NSWorkspace.shared.open(url) else {
              self.finish(.failure(OrbitError(message: "로그인 브라우저를 열 수 없습니다.")))
              return
            }
          } else if case .failed = status {
            self.finish(.failure(OrbitError(message: "로컬 로그인 응답 창을 열 수 없습니다.")))
          }
        }
      }
      listener.newConnectionHandler = { [weak self] connection in
        Task { @MainActor in
          connection.start(queue: .main)
          self?.receive(connection, buffer: Data(), state: state, verifier: verifier)
        }
      }
      timeout = Task { [weak self] in
        try? await Task.sleep(nanoseconds: 180_000_000_000)
        if !Task.isCancelled {
          self?.finish(.failure(OrbitError(message: "Google 로그인이 시간 초과되었습니다. 다시 연결하세요.")))
        }
      }
      listener.start(queue: .main)
    }
  }
  private func receive(_ connection: NWConnection, buffer: Data, state: String, verifier: String) {
    connection.receive(minimumIncompleteLength: 1, maximumLength: 8192) {
      [weak self] data, _, complete, error in
      Task { @MainActor in
        guard let self = self else {
          connection.cancel()
          return
        }
        var body = buffer
        body.append(data ?? Data())
        guard body.count <= 16384, error == nil else {
          connection.cancel()
          return
        }
        let text = String(decoding: body, as: UTF8.self)
        guard text.contains("\r\n\r\n") else {
          if complete {
            connection.cancel()
          } else {
            self.receive(connection, buffer: body, state: state, verifier: verifier)
          }
          return
        }
        let first = String(text.split(separator: "\n").first ?? "").split(separator: " ")
        guard first.count >= 2, first[0] == "GET", first[1].hasPrefix("/oauth/callback?") else {
          self.respond(connection, "404 Not Found", "Not found")
          return
        }
        do {
          let code = try OAuthHelpers.callbackCode(target: String(first[1]), state: state)
          guard let port = self.listener?.port else {
            throw OrbitError(message: "로그인 요청이 만료되었습니다.")
          }
          self.respond(
            connection, "200 OK",
            "<!doctype html><meta charset=utf-8><title>Orbit</title><body style='font-family:system-ui;background:#111713;color:#d5f69c;padding:60px'><h1>Orbit으로 돌아가세요.</h1><p>로그인 응답을 받았습니다. 앱에서 연결 결과를 확인하세요.</p></body>"
          )
          self.finish(
            .success((code, verifier, "http://127.0.0.1:\(port.rawValue)/oauth/callback")))
        } catch {
          self.respond(
            connection, "400 Bad Request", "Login response rejected. Return to Orbit and retry.")
          self.finish(.failure(error))
        }
      }
    }
  }
  private func respond(_ c: NWConnection, _ status: String, _ body: String) {
    let data = Data(body.utf8)
    let header =
      "HTTP/1.1 \(status)\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: \(data.count)\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n"
    c.send(content: Data(header.utf8) + data, completion: .contentProcessed { _ in c.cancel() })
  }
  private func finish(_ result: Result<Authorization, Error>) {
    let cont = continuation
    continuation = nil
    timeout?.cancel()
    timeout = nil
    listener?.stateUpdateHandler = nil
    listener?.newConnectionHandler = nil
    listener?.cancel()
    listener = nil
    cont?.resume(with: result)
  }
}
