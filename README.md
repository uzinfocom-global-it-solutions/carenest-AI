# ParentAI

AI family assistant — parents create a family, add children with sensitivities and routines, and get child-specific recommendations driven by weather, schedule, and chat.

## Repo layout

```
ParentAI/
├── backend/          # .NET 10 API (Clean Architecture, Aspire, EF Core + Postgres, Redis cache)
├── app/              # Flutter app (iOS, Android)
├── ParentAI-design/  # Design exploration (HTML/JSX mockups; not built)
└── README.md         # You are here
```

- **Backend**: ASP.NET Core minimal-API endpoints under `/auth`, `/families`, `/children`, `/chats`, `/calendar`, `/weather`, `/recommendations`, `/users`. JWT auth, MediatR for CQRS, FluentValidation, Swagger UI at `/docs`.
- **App**: Flutter app using provider for state, go_router for navigation. Talks to the backend over HTTP with a JWT bearer token.

## Prerequisites

- Docker Desktop (for Postgres + Redis)
- .NET SDK **10.0** (`dotnet --version` should print `10.x`)
- Flutter SDK **3.11+** with platform tooling for the target you care about (Android SDK / Xcode)

## Quickstart

```bash
# 1. Start data services
docker compose -f backend/docker/docker-compose.yml up -d postgres redis

# 2. Start the backend (listens on http://0.0.0.0:5204, Swagger at http://localhost:5204/docs)
cd backend
dotnet run --project src/Web --launch-profile http

# 3. In a separate shell, start the app
cd app
flutter pub get
flutter run --dart-define-from-file=env.json
```

See [`backend/README.md`](backend/README.md) and [`app/README.md`](app/README.md) for details, configuration, and troubleshooting.

## Demo path

1. Register → router sends you to onboarding.
2. Create your family → add 1+ children (name + age) → toggle sensitivities → finish.
3. Land on the home dashboard: weather card, child quick-switcher, recommendations.
4. Bottom nav: **Home**, **Plan** (calendar, read-only), **Chat** (AI conversation), **Family** (children list), **Settings** (profile + sign out).

## Known gaps (not regressions)

- No in-app routine / calendar event create UI (backend endpoints exist).
- No post-onboarding "Add child" UI.
- Push notifications (30-min-before reminders) not implemented — backend has the data model but no FCM/APNS transport yet.
- Voice modal is a placeholder; no STT/TTS.
- Family location currently hardcoded in the app bootstrap; not pulled from family record.
