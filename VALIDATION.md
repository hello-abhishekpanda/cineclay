# Validation performed

- .NET SDK 10.0.403: API and Blazor Web builds succeeded with zero warnings and zero errors.
- 12 executable model checks: accepted valid/future dated records and bundled posters; rejected pre-1990 years, unsafe poster URLs, unsafe trailer IDs, invalid country codes, credentials in URLs and blank cast members.
- Blazor runtime HTTP checks: login page renders; missing CSRF token is rejected; wrong password fails; correct password sets the admin cookie and reaches authorized Studio. API-down state is handled.
- Hosted-preview browser checks: real poster/backdrop loading, cast search, combined year/genre filters, film details, watchlist addition and retrieval, manual film creation and removal.
- JavaScript syntax validation passed.
- Both .NET projects include source; this is not a JavaScript implementation presented as Blazor. The browser preview is separately labeled.

## Limits

PostgreSQL and Docker are not installed in the authoring environment. PostgreSQL installation was blocked by container user/permission restrictions. The real PostgreSQL schema, API CRUD and container integration have **not** been executed here. A local Docker Compose configuration and an API smoke script are included for that verification.

No TMDB credential was provided, so live import and current provider metadata have **not** been exercised. The preview uses a 19-film seed; it is not an exhaustive global catalog. No Supabase project is connected or claimed to be connected. Migration instructions are included.

The source has not had a production security audit, load test or accessibility audit. Container builds are supplied but were not run in this environment.

## Expansion validation

- All 74 public American/Hindi year-list fetches/parses completed without source failures; snapshot spans 1990–2026. The final catalog has 12,243 unique IDs with 12,228 movie records and 15 TV series.
- 42 enriched records and 12,201 index-only records. No invented completeness, provider availability or global ranking is claimed.
- API and Blazor compile with zero errors/warnings. 15 executable model checks now include media-type validation and the distinction between global official title pages and regional subscription offers.
- Browser: TV filter returns 15 titles; pagination moves from 1–12 to 13–15 and disables Next on the last page. Public source links, real brand icons, top-ten selection and trailer popups are checked separately.
- PostgreSQL execution remains unverified in this environment, as stated above. The real public scrape and TVmaze fetches did run; the corresponding local JSON files are included.

## clayDot expansion checks

- API and Blazor build with zero warnings/errors after language metadata, native clayDot, the Top 20 timer and platform-directory changes.
- Public TVmaze import expanded the selected series catalog to 106; the snapshot has 17 indexed language values. Source fetch failures are reported rather than silently fabricated.
- Browser: the assistant proceeds through Question 1, 2 and 3 and produces three English series matching an escapist mood. Recommendations contain real source titles and genre explanations. Slideshow progress was observed changing automatically; it pauses while clayDot is open.
- Availability responses distinguish direct official title pages from a verified regional offer. Legal free links are service-catalog links. No generative model or live PostgreSQL connection is claimed.
