import Foundation

actor APIClient {
    static let shared = APIClient()
    static let productionBaseURL = URL(string: "https://fijilaw-api-production-production.up.railway.app")!

    private let baseURL: URL
    private let session: URLSession
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder

    init(baseURL: URL = APIClient.productionBaseURL, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
        self.encoder = JSONEncoder()
        self.decoder = JSONDecoder()
    }

    func login(email: String, password: String) async throws -> AuthSessionResult {
        try await send("/api/auth/login", method: "POST", body: LoginRequest(email: email, password: password), token: nil)
    }

    func register(email: String, password: String, displayName: String?) async throws -> AuthSessionResult {
        try await send(
            "/api/auth/register",
            method: "POST",
            body: RegisterRequest(email: email, password: password, displayName: displayName, requestedPlanCode: "free"),
            token: nil
        )
    }

    func member(token: String) async throws -> AuthenticatedMember {
        try await get("/api/membership/me", token: token)
    }

    func wallet(token: String) async throws -> CreditWalletEnvelope {
        try await get("/api/credits/wallet", token: token)
    }

    func catalog() async throws -> CreditCatalogResponse {
        try await get("/api/credits/catalog", token: nil)
    }

    func checkout(packageCode: String, token: String) async throws -> CreditCheckoutResponse {
        try await send("/api/credits/checkout", method: "POST", body: CreditCheckoutRequest(packageCode: packageCode), token: token)
    }

    func triage(_ request: LegalTriageRequest, token: String) async throws -> LegalTriageResult {
        try await send("/api/legal/triage", method: "POST", body: request, token: token)
    }

    func legalServices(query: String = "", city: String = "") async throws -> LegalServicesResponse {
        var components = URLComponents(url: baseURL.appendingPathComponent("api/legal-services"), resolvingAgainstBaseURL: false)!
        var items: [URLQueryItem] = []
        if !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            items.append(URLQueryItem(name: "q", value: query))
        }
        if !city.isEmpty {
            items.append(URLQueryItem(name: "city", value: city))
        }
        components.queryItems = items.isEmpty ? nil : items
        guard let url = components.url else { throw APIError.invalidURL }
        return try await execute(URLRequest(url: url), as: LegalServicesResponse.self)
    }

    func analyseDocument(fileURL: URL, token: String) async throws -> DocumentAnalysisResponse {
        let scoped = fileURL.startAccessingSecurityScopedResource()
        defer { if scoped { fileURL.stopAccessingSecurityScopedResource() } }
        let data = try Data(contentsOf: fileURL)
        let boundary = "Boundary-\(UUID().uuidString)"
        var body = Data()
        body.appendString("--\(boundary)\r\n")
        body.appendString("Content-Disposition: form-data; name=\"file\"; filename=\"\(fileURL.lastPathComponent)\"\r\n")
        body.appendString("Content-Type: \(contentType(for: fileURL))\r\n\r\n")
        body.append(data)
        body.appendString("\r\n--\(boundary)--\r\n")

        var request = URLRequest(url: baseURL.appendingPathComponent("api/legal/documents/analyse"))
        request.httpMethod = "POST"
        request.httpBody = body
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        return try await execute(request, as: DocumentAnalysisResponse.self)
    }

    func logout(token: String) async {
        var request = URLRequest(url: baseURL.appendingPathComponent("api/auth/logout"))
        request.httpMethod = "POST"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        _ = try? await session.data(for: request)
    }

    private func get<T: Decodable>(_ path: String, token: String?) async throws -> T {
        var request = URLRequest(url: url(for: path))
        if let token { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }
        return try await execute(request, as: T.self)
    }

    private func send<B: Encodable, T: Decodable>(_ path: String, method: String, body: B, token: String?) async throws -> T {
        var request = URLRequest(url: url(for: path))
        request.httpMethod = method
        request.httpBody = try encoder.encode(body)
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let token { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }
        return try await execute(request, as: T.self)
    }

    private func execute<T: Decodable>(_ request: URLRequest, as type: T.Type) async throws -> T {
        let (data, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw APIError.invalidResponse }
        guard (200..<300).contains(http.statusCode) else {
            let envelope = try? decoder.decode(APIErrorEnvelope.self, from: data)
            let message = envelope?.error ?? envelope?.detail ?? envelope?.title ?? HTTPURLResponse.localizedString(forStatusCode: http.statusCode)
            throw APIError.server(statusCode: http.statusCode, message: message)
        }
        do {
            return try decoder.decode(T.self, from: data)
        } catch {
            throw APIError.decoding(error.localizedDescription)
        }
    }

    private func url(for path: String) -> URL {
        let trimmed = path.hasPrefix("/") ? String(path.dropFirst()) : path
        return baseURL.appendingPathComponent(trimmed)
    }

    private func contentType(for url: URL) -> String {
        switch url.pathExtension.lowercased() {
        case "pdf": return "application/pdf"
        case "docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        case "txt": return "text/plain"
        default: return "application/octet-stream"
        }
    }
}

enum APIError: LocalizedError {
    case invalidURL
    case invalidResponse
    case server(statusCode: Int, message: String)
    case decoding(String)

    var errorDescription: String? {
        switch self {
        case .invalidURL: return "The FijiLaw service URL is invalid."
        case .invalidResponse: return "FijiLaw returned an invalid network response."
        case let .server(_, message): return message
        case let .decoding(message): return "The FijiLaw response could not be read: \(message)"
        }
    }
}

private extension Data {
    mutating func appendString(_ string: String) {
        if let data = string.data(using: .utf8) { append(data) }
    }
}
