import Foundation
import Security
import SwiftUI

@MainActor
final class AuthStore: ObservableObject {
    @Published private(set) var accessToken: String?
    @Published private(set) var member: AuthenticatedMember?
    @Published var isLoading = false
    @Published var errorMessage: String?

    private let keychain = KeychainStore(service: "fj.com.fijilaw.app")
    private let api = APIClient.shared
    private let tokenKey = "accessToken"

    init() {
        accessToken = keychain.string(forKey: tokenKey)
    }

    var isAuthenticated: Bool { accessToken != nil }

    func bootstrap() async {
        guard let token = accessToken else { return }
        isLoading = true
        defer { isLoading = false }
        do {
            member = try await api.member(token: token)
        } catch {
            clearLocalSession()
        }
    }

    func login(email: String, password: String) async {
        await authenticate {
            try await api.login(email: email, password: password)
        }
    }

    func register(email: String, password: String, displayName: String?) async {
        await authenticate {
            try await api.register(email: email, password: password, displayName: displayName)
        }
    }

    func refreshMember() async {
        guard let token = accessToken else { return }
        do { member = try await api.member(token: token) }
        catch { errorMessage = error.localizedDescription }
    }

    func logout() async {
        if let token = accessToken { await api.logout(token: token) }
        clearLocalSession()
    }

    private func authenticate(_ operation: () async throws -> AuthSessionResult) async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            let session = try await operation()
            try keychain.set(session.accessToken, forKey: tokenKey)
            accessToken = session.accessToken
            member = try await api.member(token: session.accessToken)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func clearLocalSession() {
        keychain.remove(tokenKey)
        accessToken = nil
        member = nil
    }
}

final class KeychainStore {
    private let service: String

    init(service: String) { self.service = service }

    func set(_ value: String, forKey key: String) throws {
        let data = Data(value.utf8)
        remove(key)
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        ]
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else { throw KeychainError.status(status) }
    }

    func string(forKey key: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
              let data = item as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    func remove(_ key: String) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key
        ]
        SecItemDelete(query as CFDictionary)
    }
}

enum KeychainError: Error {
    case status(OSStatus)
}
