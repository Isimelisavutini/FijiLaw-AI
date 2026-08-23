import Foundation

struct LoginRequest: Codable {
    let email: String
    let password: String
}

struct RegisterRequest: Codable {
    let email: String
    let password: String
    let displayName: String?
    let requestedPlanCode: String?
}

struct AuthSessionResult: Codable {
    let accessToken: String
    let expiresAt: String
    let userId: UUID
    let email: String
    let displayName: String?
}

struct AuthenticatedMember: Codable {
    let userId: UUID
    let email: String
    let displayName: String?
    let emailVerified: Bool
    let roles: [String]
    let permissions: [String]
    let planCode: String
    let subscriptionStatus: String
    let currentPeriodEnd: String?
}

struct CreditWalletSnapshot: Codable {
    let userId: UUID
    let balance: Int
    let lifetimePurchased: Int64
    let lifetimeGranted: Int64
    let lifetimeUsed: Int64
    let lastAllowanceKey: String?
}

struct CreditWalletEnvelope: Codable {
    let wallet: CreditWalletSnapshot
    let planCode: String
}

struct CreditPackage: Codable, Identifiable {
    var id: String { code }
    let code: String
    let name: String
    let credits: Int
    let priceFjd: Decimal
}

struct CreditService: Codable, Identifiable {
    var id: String { serviceCode }
    let serviceCode: String
    let name: String
    let credits: Int
    let implemented: Bool
}

struct CreditCatalogResponse: Codable {
    let currency: String
    let terminology: String
    let packages: [CreditPackage]
    let services: [CreditService]
    let paymentProvider: String?
    let paymentCheckoutReady: Bool
    let includedByPlan: [String: Int]
    let note: String
}

struct CreditCheckoutRequest: Codable {
    let packageCode: String
}

struct CreditCheckoutResponse: Codable {
    let simulated: Bool
    let charged: Bool
    let provider: String?
    let orderId: UUID?
    let checkoutUrl: String?
    let package: CreditPackage
    let message: String
}

struct LegalTriageRequest: Codable {
    let situation: String
    let location: String?
    let language: String
}

struct LegalAuthority: Codable, Identifiable {
    var id: String { "\(title)|\(provision ?? "")" }
    let title: String
    let provision: String?
    let sourceUrl: String?
    let verified: Bool
}

struct LegalTriageResult: Codable {
    let issue: String
    let facts: [String]
    let missingInformation: [String]
    let authorities: [LegalAuthority]
    let guidance: String
    let nextSteps: [String]
    let humanReviewRequired: Bool
    let disclaimer: String
    let correlationId: String
    let legalDomains: [String]?
}

struct LegalServicesResponse: Codable {
    let items: [LegalServiceLocation]
    let cities: [String]
}

struct LegalServiceLocation: Codable, Identifiable {
    let id: String
    let name: String
    let type: String
    let city: String
    let address: String
    let phone: String?
    let website: String?
    let practiceAreas: [String]
    let verified: Bool
    let verificationNote: String
}

struct DocumentAnalysisResponse: Codable {
    let fileName: String
    let contentType: String
    let characterCount: Int
    let preview: String
    let assessment: LegalTriageResult
    let creditsUsed: Int
    let note: String
}

struct APIErrorEnvelope: Codable {
    let error: String?
    let title: String?
    let detail: String?
}
