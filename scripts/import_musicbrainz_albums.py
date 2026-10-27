#!/usr/bin/env python3
"""Import 5,000+ real album records and link each to a YouTube search."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import pathlib
import time
import urllib.error
import urllib.parse
import urllib.request


API_URL = "https://musicbrainz.org/ws/2/release/"
USER_AGENT = "Cineclay/1.0 (https://github.com/hello-abhishekpanda/cineclay)"
LANGUAGES = {
    "Odia": "ori",
    "Hindi": "hin",
    "English": "eng",
    "Telugu": "tel",
    "Malayalam": "mal",
    "Tamil": "tam",
    "Kannada": "kan",
    "Bengali": "ben",
    "Marathi": "mar",
    "Punjabi": "pan",
    "Japanese": "jpn",
    "Korean": "kor",
    "Spanish": "spa",
    "French": "fra",
    "German": "deu",
}


def stable_id(mbid: str) -> int:
    value = int.from_bytes(hashlib.sha256(mbid.encode()).digest()[:8], "big")
    return value & 0x7FFF_FFFF_FFFF_FFFF or 1


def fetch(language_code: str, start_year: int, end_year: int, offset: int) -> dict:
    query = (
        f"lang:{language_code} AND primarytype:album AND status:official "
        f"AND date:[{start_year}-01-01 TO {end_year}-12-31]"
    )
    url = f"{API_URL}?{urllib.parse.urlencode({'query': query, 'fmt': 'json', 'limit': 100, 'offset': offset})}"
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if error.code not in (429, 503) or attempt == 3:
                raise
            time.sleep(2 + attempt * 2)
    return {}


def artist_name(release: dict) -> str:
    names = [credit.get("name", "").strip() for credit in release.get("artist-credit", [])]
    return ", ".join(name for name in names if name) or "Unknown artist"


def make_album(language: str, release: dict) -> dict | None:
    title = release.get("title", "").strip()
    date = release.get("date", "")
    mbid = release.get("id", "")
    artist = artist_name(release)
    release_group_id = release.get("release-group", {}).get("id", "")
    if not title or len(date) < 4 or not date[:4].isdigit() or not mbid or not release_group_id:
        return None

    year = int(date[:4])
    search = urllib.parse.urlencode({"search_query": f"{artist} {title} full album"})
    return {
        "id": stable_id(mbid),
        "externalId": mbid,
        "title": title,
        "language": language,
        "year": year,
        "artist": artist,
        "cover": f"https://coverartarchive.org/release-group/{release_group_id}/front-500",
        "sourceUrl": f"https://www.youtube.com/results?{search}",
        "songs": [],
    }


def apply_albums(api_url: str, admin_key: str, albums: list[dict]) -> None:
    for start in range(0, len(albums), 1000):
        batch = albums[start : start + 1000]
        request = urllib.request.Request(
            f"{api_url.rstrip('/')}/api/admin/albums/import",
            data=json.dumps(batch).encode(),
            method="POST",
            headers={"Content-Type": "application/json", "X-Admin-Key": admin_key},
        )
        with urllib.request.urlopen(request, timeout=120) as response:
            if response.status >= 300:
                raise RuntimeError(f"Album import failed with HTTP {response.status}")
        print(f"Applied {min(start + len(batch), len(albums)):,}/{len(albums):,} albums")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", type=int, default=5001)
    parser.add_argument("--start-year", type=int, default=2000)
    parser.add_argument("--end-year", type=int, default=dt.datetime.now(dt.timezone.utc).year)
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        default=pathlib.Path("src/Cineclay.Api/albums.seed.json"),
    )
    parser.add_argument("--delay", type=float, default=1.05)
    parser.add_argument(
        "--refresh-covers",
        action="store_true",
        help="Revisit MusicBrainz results and attach Cover Art Archive URLs to existing records.",
    )
    parser.add_argument("--apply-url", help="Optional Cineclay API base URL")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.target < 5001:
        raise SystemExit("The catalog target must be at least 5,001.")
    current_year = dt.datetime.now(dt.timezone.utc).year
    if args.start_year < 2000 or args.end_year > current_year or args.start_year > args.end_year:
        raise SystemExit(f"Years must be between 2000 and {current_year}.")
    admin_key = os.environ.get("ADMIN_API_KEY", "")
    if args.apply_url and len(admin_key) < 32:
        raise SystemExit("Set ADMIN_API_KEY (at least 32 characters) when using --apply-url.")

    existing = json.loads(args.output.read_text(encoding="utf-8")) if args.output.exists() else []
    albums = list(existing)
    albums_by_id = {album["id"]: album for album in albums}
    seen_ids = {album["id"] for album in albums}
    seen_editions = {
        (album["language"], album["title"].casefold(), album["artist"].casefold(), album["year"])
        for album in albums
    }

    for language, code in LANGUAGES.items():
        for offset in range(0, 500, 100):
            if len(albums) >= args.target and not args.refresh_covers:
                break
            data = fetch(code, args.start_year, args.end_year, offset)
            for release in data.get("releases", []):
                album = make_album(language, release)
                if album is None:
                    continue
                if album["id"] in albums_by_id:
                    if args.refresh_covers:
                        albums_by_id[album["id"]]["externalId"] = album["externalId"]
                        albums_by_id[album["id"]]["cover"] = album["cover"]
                    continue
                if len(albums) >= args.target:
                    continue
                edition = (
                    album["language"],
                    album["title"].casefold(),
                    album["artist"].casefold(),
                    album["year"],
                )
                if album["id"] in seen_ids or edition in seen_editions:
                    continue
                albums.append(album)
                albums_by_id[album["id"]] = album
                seen_ids.add(album["id"])
                seen_editions.add(edition)
                if len(albums) >= args.target and not args.refresh_covers:
                    break
            print(f"{language} offset {offset}: {len(albums):,}/{args.target:,}")
            time.sleep(args.delay)
        if len(albums) >= args.target and not args.refresh_covers:
            break

    albums.sort(key=lambda album: (album["language"] != "Odia", -album["year"], album["title"]))
    args.output.write_text(json.dumps(albums, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {len(albums):,} albums to {args.output}")
    if len(albums) < args.target:
        raise SystemExit("MusicBrainz returned fewer unique albums than requested.")
    if args.apply_url:
        apply_albums(args.apply_url, admin_key, albums)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
