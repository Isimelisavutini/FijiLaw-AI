import SwiftUI

@main
struct FijiLawApp: App {
    @StateObject private var auth = AuthStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(auth)
                .task { await auth.bootstrap() }
        }
    }
}

struct RootView: View {
    @EnvironmentObject private var auth: AuthStore

    var body: some View {
        Group {
            if auth.isLoading && auth.accessToken != nil && auth.member == nil {
                ProgressView("Connecting to FijiLaw…")
            } else if auth.isAuthenticated {
                MainTabView()
            } else {
                AuthView()
            }
        }
        .tint(.blue)
    }
}
