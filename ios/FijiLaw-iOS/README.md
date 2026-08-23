# FijiLaw iOS

Native SwiftUI client for the FijiLaw platform.

## Location

This folder is intentionally isolated from the .NET API and web frontend:

```text
ios/
└── FijiLaw-iOS/
    ├── project.yml
    ├── README.md
    └── FijiLaw/
        ├── App/
        ├── Core/
        ├── Features/
        ├── Models/
        └── Info.plist
```

## Current MVP features

- Native SwiftUI iPhone/iPad application
- Production Railway API configuration
- Registration and sign-in
- Bearer session stored in iOS Keychain
- Member/profile view and sign-out
- FijiLaw Credits balance and catalogue
- Advanced Legal Triage (10 FijiLaw Credits)
- PDF, DOCX and TXT document analysis (15 FijiLaw Credits)
- Fiji legal-services directory and search
- Legal Aid and listed private-firm contact links
- Clear AI/legal-information disclaimer presentation

## Build

The project file is generated with XcodeGen so the Xcode configuration stays reviewable in source control.

```bash
cd ios/FijiLaw-iOS
brew install xcodegen
xcodegen generate
open FijiLaw.xcodeproj
```

Use Xcode 15.4+ / 16+ and iOS 17 or later.

The production API is currently:

```text
https://fijilaw-api-production-production.up.railway.app
```

No backend secrets are stored in this app. OpenAI, database, Resend and Windcave credentials remain server-side.

## iOS payment policy

FijiLaw Credits are digital usage units consumed inside the iOS app. Before an App Store release, paid credit top-ups should be implemented with StoreKit 2 / Apple In-App Purchase and validated server-side. The web Windcave checkout remains appropriate for FijiLaw's web experience, but it should not be treated as the final App Store purchase mechanism for in-app digital credits.

The current iOS scaffold exposes the catalogue and backend checkout capability for controlled development. App Store release work should replace the purchase action with StoreKit products and backend transaction verification.

## Before TestFlight/App Store

1. Set the Apple Developer Team in Xcode.
2. Create the production App ID / bundle identifier (`fj.com.fijilaw.app` or the approved replacement).
3. Add production app icons and launch branding.
4. Add StoreKit 2 products for FijiLaw Credit packages and a backend App Store transaction-verification endpoint.
5. Configure Associated Domains if universal links are added.
6. Complete App Privacy disclosures for account information, legal questions and uploaded documents.
7. Review legal disclaimers and Terms/Privacy text for the mobile experience.
8. Test accessibility, Dynamic Type and VoiceOver.
9. Run the iOS GitHub Actions build and archive in Xcode before TestFlight upload.
