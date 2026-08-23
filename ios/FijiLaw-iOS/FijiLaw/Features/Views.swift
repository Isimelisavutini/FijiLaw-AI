import SwiftUI
import UniformTypeIdentifiers

struct AuthView: View {
    @EnvironmentObject private var auth: AuthStore
    @State private var mode = 0
    @State private var displayName = ""
    @State private var email = ""
    @State private var password = ""

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 24) {
                    VStack(spacing: 8) {
                        Image(systemName: "scale.3d")
                            .font(.system(size: 54, weight: .semibold))
                            .foregroundStyle(.blue)
                        Text("FijiLaw")
                            .font(.largeTitle.bold())
                        Text("Legal information, FijiLaw AI and verified legal-service connections in one place.")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                    }

                    Picker("Account", selection: $mode) {
                        Text("Sign In").tag(0)
                        Text("Create Account").tag(1)
                    }
                    .pickerStyle(.segmented)

                    VStack(spacing: 14) {
                        if mode == 1 {
                            TextField("Full name", text: $displayName)
                                .textContentType(.name)
                                .fieldStyle()
                        }
                        TextField("Email", text: $email)
                            .textContentType(.emailAddress)
                            .keyboardType(.emailAddress)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .fieldStyle()
                        SecureField("Password", text: $password)
                            .textContentType(mode == 0 ? .password : .newPassword)
                            .fieldStyle()
                    }

                    if let error = auth.errorMessage {
                        Text(error)
                            .font(.footnote)
                            .foregroundStyle(.red)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }

                    Button {
                        Task {
                            if mode == 0 {
                                await auth.login(email: email, password: password)
                            } else {
                                await auth.register(email: email, password: password, displayName: displayName.nilIfBlank)
                            }
                        }
                    } label: {
                        HStack {
                            if auth.isLoading { ProgressView().tint(.white) }
                            Text(mode == 0 ? "Sign In" : "Create Free Account")
                                .fontWeight(.semibold)
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 13)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(auth.isLoading || email.nilIfBlank == nil || password.isEmpty)

                    Text("FijiLaw provides legal information and guided triage, not a substitute for advice from a qualified lawyer.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                }
                .padding(24)
            }
            .navigationBarHidden(true)
        }
    }
}

struct MainTabView: View {
    var body: some View {
        TabView {
            NavigationStack { HomeView() }
                .tabItem { Label("Home", systemImage: "house.fill") }
            NavigationStack { TriageView() }
                .tabItem { Label("AI Triage", systemImage: "sparkles") }
            NavigationStack { ServicesView() }
                .tabItem { Label("Legal Help", systemImage: "building.columns.fill") }
            NavigationStack { CreditsView() }
                .tabItem { Label("Credits", systemImage: "creditcard.fill") }
            NavigationStack { ProfileView() }
                .tabItem { Label("Profile", systemImage: "person.crop.circle.fill") }
        }
    }
}

struct HomeView: View {
    @EnvironmentObject private var auth: AuthStore
    @State private var wallet: CreditWalletSnapshot?
    @State private var errorMessage: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Bula\(auth.member?.displayName.map { ", \($0)" } ?? "")")
                        .font(.largeTitle.bold())
                    Text("Access Fiji-focused legal guidance and services.")
                        .foregroundStyle(.secondary)
                }

                HStack(spacing: 12) {
                    MetricCard(title: "FijiLaw Credits", value: wallet.map { "\($0.balance)" } ?? "—", icon: "bolt.fill")
                    MetricCard(title: "Plan", value: auth.member?.planCode.displayPlan ?? "—", icon: "person.text.rectangle")
                }

                VStack(alignment: .leading, spacing: 12) {
                    Text("Quick actions").font(.headline)
                    NavigationLink {
                        TriageView()
                    } label: {
                        ActionCard(title: "Tell Me My Rights", subtitle: "Run an Advanced Legal Triage Report", icon: "sparkles")
                    }
                    .buttonStyle(.plain)

                    NavigationLink {
                        DocumentAnalysisView()
                    } label: {
                        ActionCard(title: "Analyse a Legal Document", subtitle: "Upload PDF, DOCX or TXT", icon: "doc.text.magnifyingglass")
                    }
                    .buttonStyle(.plain)

                    NavigationLink {
                        ServicesView()
                    } label: {
                        ActionCard(title: "Find Legal Help", subtitle: "Legal Aid and listed law firms across Fiji", icon: "mappin.and.ellipse")
                    }
                    .buttonStyle(.plain)
                }

                if let errorMessage {
                    Text(errorMessage).font(.footnote).foregroundStyle(.red)
                }
            }
            .padding()
        }
        .navigationTitle("FijiLaw")
        .task { await loadWallet() }
        .refreshable { await loadWallet() }
    }

    private func loadWallet() async {
        guard let token = auth.accessToken else { return }
        do { wallet = try await APIClient.shared.wallet(token: token).wallet }
        catch { errorMessage = error.localizedDescription }
    }
}

struct TriageView: View {
    @EnvironmentObject private var auth: AuthStore
    @State private var situation = ""
    @State private var location = ""
    @State private var language = "en"
    @State private var result: LegalTriageResult?
    @State private var isLoading = false
    @State private var errorMessage: String?

    var body: some View {
        Form {
            Section("Your situation") {
                TextEditor(text: $situation)
                    .frame(minHeight: 150)
                TextField("Location (optional)", text: $location)
                Picker("Language", selection: $language) {
                    Text("English").tag("en")
                    Text("iTaukei").tag("fj")
                    Text("Fiji Hindi").tag("hi")
                }
            }

            Section {
                Button {
                    Task { await runTriage() }
                } label: {
                    HStack {
                        if isLoading { ProgressView() }
                        Text("Run Advanced Legal Triage")
                    }
                }
                .disabled(isLoading || situation.nilIfBlank == nil)
            } footer: {
                Text("Uses 10 FijiLaw Credits when the workflow completes successfully.")
            }

            if let errorMessage {
                Section { Text(errorMessage).foregroundStyle(.red) }
            }

            if let result {
                Section("Issue") { Text(result.issue) }
                if let domains = result.legalDomains, !domains.isEmpty {
                    Section("Legal areas") {
                        Text(domains.joined(separator: ", "))
                    }
                }
                Section("Guidance") { Text(result.guidance) }
                if !result.authorities.isEmpty {
                    Section("Authorities") {
                        ForEach(result.authorities) { authority in
                            VStack(alignment: .leading, spacing: 4) {
                                Text(authority.title).fontWeight(.semibold)
                                if let provision = authority.provision { Text(provision).font(.caption) }
                                Label(authority.verified ? "Verified" : "Needs verification", systemImage: authority.verified ? "checkmark.seal.fill" : "exclamationmark.triangle")
                                    .font(.caption)
                                    .foregroundStyle(authority.verified ? .green : .orange)
                            }
                        }
                    }
                }
                if !result.missingInformation.isEmpty {
                    Section("Information still needed") {
                        ForEach(result.missingInformation, id: \.self) { Text("• \($0)") }
                    }
                }
                Section("Next steps") {
                    ForEach(Array(result.nextSteps.enumerated()), id: \.offset) { index, step in
                        Text("\(index + 1). \(step)")
                    }
                }
                Section("Important") {
                    Text(result.disclaimer).font(.footnote).foregroundStyle(.secondary)
                }
            }
        }
        .navigationTitle("AI Legal Triage")
    }

    private func runTriage() async {
        guard let token = auth.accessToken else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            result = try await APIClient.shared.triage(
                LegalTriageRequest(situation: situation, location: location.nilIfBlank, language: language),
                token: token
            )
        } catch {
            errorMessage = error.localizedDescription
        }
    }
}

struct ServicesView: View {
    @State private var response: LegalServicesResponse?
    @State private var query = ""
    @State private var city = ""
    @State private var isLoading = false
    @State private var errorMessage: String?

    var body: some View {
        List {
            if let cities = response?.cities, !cities.isEmpty {
                Section {
                    Picker("City", selection: $city) {
                        Text("All Fiji").tag("")
                        ForEach(cities, id: \.self) { Text($0).tag($0) }
                    }
                    .onChange(of: city) { _, _ in Task { await load() } }
                }
            }

            if isLoading { ProgressView("Searching…") }
            if let errorMessage { Text(errorMessage).foregroundStyle(.red) }

            ForEach(response?.items ?? []) { service in
                VStack(alignment: .leading, spacing: 8) {
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(service.name).font(.headline)
                            Text("\(service.type) • \(service.city)").font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        if service.verified {
                            Image(systemName: "checkmark.seal.fill").foregroundStyle(.green)
                        }
                    }
                    Text(service.address).font(.subheadline)
                    Text(service.practiceAreas.joined(separator: " • ")).font(.caption).foregroundStyle(.secondary)
                    HStack {
                        if let phone = service.phone, let url = URL(string: "tel:\(phone.telephoneSafe)") {
                            Link("Call", destination: url)
                        }
                        if let website = service.website, let url = URL(string: website) {
                            Link("Website", destination: url)
                        }
                    }
                    .font(.subheadline.bold())
                }
                .padding(.vertical, 5)
            }
        }
        .navigationTitle("Find Legal Help")
        .searchable(text: $query, prompt: "Firm, city or legal area")
        .onSubmit(of: .search) { Task { await load() } }
        .task { if response == nil { await load() } }
        .refreshable { await load() }
    }

    private func load() async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do { response = try await APIClient.shared.legalServices(query: query, city: city) }
        catch { errorMessage = error.localizedDescription }
    }
}

struct CreditsView: View {
    @EnvironmentObject private var auth: AuthStore
    @Environment(\.openURL) private var openURL
    @State private var wallet: CreditWalletSnapshot?
    @State private var catalog: CreditCatalogResponse?
    @State private var purchasingCode: String?
    @State private var errorMessage: String?

    var body: some View {
        List {
            Section("Balance") {
                HStack {
                    VStack(alignment: .leading) {
                        Text("Available FijiLaw Credits").font(.subheadline).foregroundStyle(.secondary)
                        Text(wallet.map { "\($0.balance)" } ?? "—").font(.system(size: 38, weight: .bold, design: .rounded))
                    }
                    Spacer()
                    Image(systemName: "bolt.circle.fill").font(.system(size: 42)).foregroundStyle(.blue)
                }
            }

            if let catalog {
                Section("Credit packages") {
                    ForEach(catalog.packages) { package in
                        HStack {
                            VStack(alignment: .leading, spacing: 3) {
                                Text(package.name).font(.headline)
                                Text("\(package.credits) credits • \(package.priceLabel)")
                                    .font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Button(purchasingCode == package.code ? "Starting…" : "Buy") {
                                Task { await purchase(package) }
                            }
                            .buttonStyle(.borderedProminent)
                            .disabled(purchasingCode != nil || !catalog.paymentCheckoutReady)
                        }
                    }
                } footer: {
                    if catalog.paymentCheckoutReady {
                        Text("Payments open on the secure hosted payment page. FijiLaw never handles raw card details in the app.")
                    } else {
                        Text("Online purchases are temporarily unavailable until the Fiji merchant payment account is activated.")
                    }
                }

                Section("AI service costs") {
                    ForEach(catalog.services.filter(\.implemented)) { service in
                        HStack {
                            Text(service.name)
                            Spacer()
                            Text("\(service.credits)").foregroundStyle(.secondary)
                        }
                    }
                }
            }

            if let errorMessage { Section { Text(errorMessage).foregroundStyle(.red) } }
        }
        .navigationTitle("FijiLaw Credits")
        .task { await load() }
        .refreshable { await load() }
    }

    private func load() async {
        guard let token = auth.accessToken else { return }
        do {
            async let walletRequest = APIClient.shared.wallet(token: token)
            async let catalogRequest = APIClient.shared.catalog()
            let (walletEnvelope, catalogResponse) = try await (walletRequest, catalogRequest)
            wallet = walletEnvelope.wallet
            catalog = catalogResponse
        } catch { errorMessage = error.localizedDescription }
    }

    private func purchase(_ package: CreditPackage) async {
        guard let token = auth.accessToken else { return }
        purchasingCode = package.code
        errorMessage = nil
        defer { purchasingCode = nil }
        do {
            let checkout = try await APIClient.shared.checkout(packageCode: package.code, token: token)
            if let urlString = checkout.checkoutUrl, let url = URL(string: urlString) {
                openURL(url)
            } else {
                errorMessage = checkout.message
            }
        } catch { errorMessage = error.localizedDescription }
    }
}

struct DocumentAnalysisView: View {
    @EnvironmentObject private var auth: AuthStore
    @State private var selectedFile: URL?
    @State private var showingImporter = false
    @State private var result: DocumentAnalysisResponse?
    @State private var isLoading = false
    @State private var errorMessage: String?

    private var allowedTypes: [UTType] {
        [.pdf, .plainText, UTType(filenameExtension: "docx") ?? .data]
    }

    var body: some View {
        Form {
            Section("Document") {
                Button("Choose PDF, DOCX or TXT") { showingImporter = true }
                if let selectedFile { Text(selectedFile.lastPathComponent).font(.subheadline) }
                Button {
                    Task { await analyse() }
                } label: {
                    HStack {
                        if isLoading { ProgressView() }
                        Text("Analyse Document")
                    }
                }
                .disabled(selectedFile == nil || isLoading)
            } footer: {
                Text("Document analysis uses 15 FijiLaw Credits after a successful workflow.")
            }

            if let errorMessage { Section { Text(errorMessage).foregroundStyle(.red) } }
            if let result {
                Section("Assessment") {
                    Text(result.assessment.issue).font(.headline)
                    Text(result.assessment.guidance)
                }
                Section("Next steps") {
                    ForEach(result.assessment.nextSteps, id: \.self) { Text("• \($0)") }
                }
                Section("Processing note") {
                    Text(result.note).font(.footnote).foregroundStyle(.secondary)
                    Text("Credits used: \(result.creditsUsed)").font(.footnote.bold())
                }
            }
        }
        .navigationTitle("Document Analysis")
        .fileImporter(isPresented: $showingImporter, allowedContentTypes: allowedTypes, allowsMultipleSelection: false) { selection in
            switch selection {
            case let .success(urls): selectedFile = urls.first
            case let .failure(error): errorMessage = error.localizedDescription
            }
        }
    }

    private func analyse() async {
        guard let token = auth.accessToken, let selectedFile else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do { result = try await APIClient.shared.analyseDocument(fileURL: selectedFile, token: token) }
        catch { errorMessage = error.localizedDescription }
    }
}

struct ProfileView: View {
    @EnvironmentObject private var auth: AuthStore

    var body: some View {
        List {
            if let member = auth.member {
                Section("Account") {
                    LabeledContent("Name", value: member.displayName ?? "Not set")
                    LabeledContent("Email", value: member.email)
                    LabeledContent("Plan", value: member.planCode.displayPlan)
                    LabeledContent("Status", value: member.subscriptionStatus.capitalized)
                    LabeledContent("Email verified", value: member.emailVerified ? "Yes" : "No")
                }
                Section("Access") {
                    Text(member.roles.map(\.displayPlan).joined(separator: ", "))
                        .foregroundStyle(.secondary)
                }
            }
            Section {
                Button("Sign Out", role: .destructive) { Task { await auth.logout() } }
            }
            Section("About") {
                Text("FijiLaw connects people in Fiji with legal information, guided AI triage and legal-service directories. AI output should be verified before relying on it for legal decisions.")
                    .font(.footnote)
            }
        }
        .navigationTitle("Profile")
    }
}

private struct MetricCard: View {
    let title: String
    let value: String
    let icon: String

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Image(systemName: icon).foregroundStyle(.blue)
            Text(value).font(.title2.bold()).lineLimit(1).minimumScaleFactor(0.7)
            Text(title).font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 18))
    }
}

private struct ActionCard: View {
    let title: String
    let subtitle: String
    let icon: String

    var body: some View {
        HStack(spacing: 14) {
            Image(systemName: icon)
                .font(.title2)
                .frame(width: 42, height: 42)
                .background(Color.blue.opacity(0.12), in: RoundedRectangle(cornerRadius: 12))
                .foregroundStyle(.blue)
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.headline)
                Text(subtitle).font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            Image(systemName: "chevron.right").foregroundStyle(.tertiary)
        }
        .padding()
        .background(Color(uiColor: .secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 18))
    }
}

private extension View {
    func fieldStyle() -> some View {
        self
            .padding(12)
            .background(Color(uiColor: .secondarySystemBackground), in: RoundedRectangle(cornerRadius: 12))
    }
}

private extension String {
    var nilIfBlank: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    var displayPlan: String {
        replacingOccurrences(of: "_", with: " ")
            .split(separator: " ")
            .map { $0.capitalized }
            .joined(separator: " ")
    }

    var telephoneSafe: String {
        filter { $0.isNumber || $0 == "+" }
    }
}

private extension CreditPackage {
    var priceLabel: String {
        String(format: "FJD %.2f", NSDecimalNumber(decimal: priceFjd).doubleValue)
    }
}
