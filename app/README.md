# app

Flutter app for CareNest. Targets iOS, Android, and (scaffolded) Windows. Talks to the CareNest backend over HTTP with JWT bearer auth.

## Prerequisites

- Flutter SDK **3.11+** (`flutter --version`)
- The backend running and reachable from your device. See [`../backend/README.md`](../backend/README.md).

For physical devices, the API must be on a LAN-reachable URL (not `localhost`). The backend already binds to `0.0.0.0:5204`, so use your machine's LAN IP (`ipconfig` on Windows, `ifconfig` / `ip addr` elsewhere).

## Configure the backend URL

The API base URL is injected at build time via `--dart-define-from-file=env.json`.

1. Copy the template and edit it for your machine:

   ```bash
   cp env.example.json env.json
   ```

2. `env.json` (gitignored):

   ```json
   {
     "API_BASE_URL": "http://192.168.1.57:5204"
   }
   ```

   - **Android emulator** → `http://10.0.2.2:5204`
   - **iOS simulator on macOS** → `http://localhost:5204`
   - **Physical device on the same Wi-Fi** → your machine's LAN IP (the `ipconfig` IPv4 address)

The code reads it via `String.fromEnvironment('API_BASE_URL')` in `lib/core/constants/api_constants.dart`. There's intentionally **no default** — if `env.json` is missing or the key is unset, requests fail loudly instead of silently hitting the wrong host.

## Run

```bash
flutter pub get
flutter run --dart-define-from-file=env.json
```

Use the same `--dart-define-from-file=env.json` flag for `flutter build` (release and profile builds also need the URL baked in).

## Layout

```
app/lib/
├── core/                       # cross-cutting constants
├── shared/                     # api client, theme, widgets, navigation
│   ├── api/api_client.dart
│   ├── navigation/app_router.dart
│   ├── theme/{app_colors,app_text_styles,app_spacing,app_theme}.dart
│   └── widgets/                # AppButton, AppAvatar, SectionLabel, …
├── features/
│   ├── auth/                   # register, login, token storage
│   ├── onboarding/             # welcome → family → children → sensitivities
│   ├── home/                   # dashboard (greeting, child switcher, weather, recs)
│   ├── chats/                  # AI chat
│   ├── plan/                   # weekly calendar (read-only)
│   ├── family/                 # children list
│   ├── child_profile/          # per-child detail
│   ├── recommendations/        # rec detail + Helpful / Not relevant feedback
│   ├── settings/               # profile + sign out
│   ├── calendar/               # controller + service
│   ├── children/               # controller + service
│   └── weather/                # controller + service
└── main.dart
```

Every internal import uses `package:app/...` (no relative `../../` paths).

## Architecture in 60 seconds

- **Controllers** (`ChangeNotifier`) own state and delegate IO to **services**.
- **Services** wrap the `ApiClient` and parse JSON into domain models.
- `AuthController` tracks `isAuthenticated` and `hasFamily`; `OnboardingController.createFamily` flips `hasFamily` to true after a successful POST.
- `app_router.dart` redirects based on those two flags:
  - not logged in → `/login`
  - logged in, no family → `/onboarding`
  - logged in, has family → `/home`
- `main.dart` listens to auth changes and bootstraps children + weather + recommendations + chat once a family exists.

## Demo path

1. Register → `/onboarding` welcome.
2. Set family name → add 1+ kids → toggle sensitivities → finish.
3. Home dashboard appears with weather, child switcher, recommendation cards.
4. Pull-to-refresh re-fetches weather and recommendations.
5. Tap a recommendation card → detail screen → submit feedback.
6. Bottom nav: Home / Plan / Chat / Family / Settings.

## Common issues

- **`Building with plugins requires symlink support.`** on Windows → enable Developer Mode: `start ms-settings:developers`.
- **App opens but every request fails** → check `env.json`. Likely you're using `localhost` from a physical device. Use the LAN IP.
- **Stuck on "Connecting to chat..." after register** → fixed: the bootstrap now fires on auth change. If it reappears, hot **restart** (not hot-reload).
- **Onboarding "Continue" doesn't advance** → the screen calls the backend. Check the backend log for the failing request.

## Test

```bash
flutter analyze
```

(Widget tests were dropped during the rewrite — only static analysis runs in CI right now.)
