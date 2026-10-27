# Cineclay

Cineclay is a movie, television and music discovery project with a cinema-inspired interface, year, genre and language filters, title search, pagination, watchlists, reviews, trailers, streaming links and the clayDot discovery assistant. Its opening sequence highlights Hollywood, Bollywood and Ollywood; Odia cinema has a dedicated collection. The Albums tab indexes Odia, Hindi and English songs through official YouTube links. Light and dark themes follow your device preference and can be changed from the header.

## Project structure

- `dist/` — the hosted static preview. It loads the checked-in catalog snapshot, and stores reviews, watchlists and preview Studio changes in the current browser. It has no live database or shared accounts. Its sign-out link is provided by the Sites host.
- `src/Cineclay.Web/` — .NET 10 Blazor Server application with authenticated administration.
- `src/Cineclay.Api/` — .NET 10 API, PostgreSQL persistence, seed data and optional TMDB import.
- `src/Cineclay.Shared/` — shared models.
- `scripts/` — catalog utilities, including the source-backed Odia snapshot and its year-list references.
- `deploy/` — non-secret Kubernetes overlays and an Argo CD application scaffold for a future Azure deployment.
- `.github/workflows/` — CI through OCI image creation and a non-publishing CD preparation workflow.
- `tests/` — API tests.

The preview's clayDot is a catalog-based, rule-driven assistant; it is not connected to an AI model. The preview Studio at `/#studio` is a browser-local demo and is not a secure administrative interface. Use the authenticated Blazor Studio for server-side changes. A production deployment needs appropriate identity, role management, HTTPS, secrets and monitoring configured for its environment.

## Run the .NET application locally

Requirements: Docker with Compose, or .NET 10 and PostgreSQL. The Compose configuration binds PostgreSQL and application ports to localhost.

1. Copy `.env.example` to `.env` and replace each placeholder with a different strong secret. The admin password must contain at least 16 characters, and the API key at least 32. Never commit `.env`.
2. Run `docker compose up --build`.
3. Open `http://localhost:5080`. The API runs at `http://localhost:5081`. Visit `/admin` to sign in using the configured administrator credentials.
4. To stop the services, run `docker compose down`. PostgreSQL data remains in the Docker volume.

The `TMDB_TOKEN` setting is optional and is only required for the authenticated TMDB import. No API token is included. Refer to `.env.example` and the Compose file for the complete configuration.

To run the static preview alone, serve `dist/` using an HTTP server, for example `python3 -m http.server 4173 --directory dist`. The Sites-specific sign-out URL works only when the preview is hosted by Sites.

## Catalog and sources

The checked-in preview currently indexes 12,427 movies and 106 TV series, including 199 Odia-language titles from selected year-list references. This is a snapshot, not an exhaustive worldwide catalog. Many archive records have only a title, year and other information available in their sources. Missing summaries, posters, genres and watch links are not inferred. `scripts/odia-references.json` records the source year pages; rerun `python3 scripts/add_odia_snapshot.py` after changing those references.

TV series metadata is sourced from TVmaze; the wider film archive includes Wikipedia year lists. Curated entries include source links where available. Some trailers, artwork and logos are owned by their respective creators and providers; use and redistribution are subject to their terms. The DAMaN trailer linked in the snapshot is the Hindi trailer. The newest-title carousel reflects this catalog, not an independently measured global popularity chart. Any sample scores in the development data are not a public ratings service.

Streaming services and availability vary by location and change over time. An official service logo in the platform directory links to its catalog; a title-specific provider link is shown only when recorded. A provider's title page is not proof of a currently available subscription or free offer. Otherwise Cineclay links to a JustWatch search, not a verified offer. Free-catalog labels describe the service, not every title on it.

The API seeds its initial enriched catalog. For the public archive, use the import utility in `scripts/load_public_catalog.py` and its help output, configuring it for the target local API. Importing is an explicit operation; it is not performed on the static preview.

## Albums and YouTube import

The initial album seed contains a small set of direct YouTube links. To discover Odia, Hindi and English music metadata for every year from 2000 through the actual current year, enable the YouTube Data API v3, set `YOUTUBE_API_KEY`, and run:

```bash
python3 scripts/import_youtube_albums.py
```

The script uses the supported YouTube Data API rather than scraping HTML. Its default 81 searches (three languages across 27 years in 2026) consume about 8,100 quota units, so review your Google Cloud quota before running it. Results are metadata and links only; no audio or video is downloaded. To apply results to a running local API, also set `ADMIN_API_KEY` and add `--apply-url http://localhost:5081`. Search metadata cannot independently prove a song's original release year, so the generated year groups reflect the year-constrained YouTube search and should be editorially reviewed.

## Database setup

`scripts/database.sh` creates and migrates a PostgreSQL database using the standard `PGHOST`, `PGPORT`, `PGUSER`, and `PGPASSWORD` variables:

```bash
CINECLAY_DB_NAME=cineclay scripts/database.sh setup
CINECLAY_DB_NAME=cineclay scripts/database.sh verify
```

The API applies the same idempotent schema at startup and seeds empty movie and album tables. Keep production database credentials in Azure Key Vault or another secret manager, never in source control.

## CI and future Azure delivery

CI restores, formatting-checks, builds and tests the .NET solution, validates Python utilities, and builds API/web OCI archives without pushing them. The manual CD preparation workflow renders Kubernetes manifests and packages them as an artifact; it does not log in to Azure, push to ACR, commit image tags, or deploy. See `deploy/README.md` for the explicit work required before enabling AKS and Argo CD delivery.

## Supabase migration

Supabase uses PostgreSQL. Back up the local database, restore it into a provisioned Supabase PostgreSQL database, and supply its connection string through `ConnectionStrings__Movies`. Configure TLS and network access according to Supabase guidance, use separate least-privilege credentials, and review data-protection key storage, admin identity, backups and migration testing before using the application in production. The project does not automatically provision Supabase or migrate an existing database.

## Validation

With the .NET 10 SDK available, run `dotnet run --project tests/Cineclay.Checks`. For the static preview, run `node --check dist/app.js` and `node --check dist/theme.js`, then verify filters, pagination, trailers, theme, sign-out routing and the assistant in a browser. The Sites preview is not a substitute for end-to-end tests of the PostgreSQL-backed application.
