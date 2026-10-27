#!/usr/bin/env python3
"""Build or apply a Cineclay album catalog using the YouTube Data API v3."""

from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
import pathlib
import time
import urllib.error
import urllib.parse
import urllib.request


API_URL = "https://www.googleapis.com/youtube/v3/search"
LANGUAGES = {"Odia": "or", "Hindi": "hi", "English": "en"}


def request_json(url: str, headers: dict[str, str] | None = None) -> dict:
    request = urllib.request.Request(url, headers=headers or {})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def search(api_key: str, language: str, year: int, limit: int) -> list[dict]:
    query = urllib.parse.urlencode(
        {
            "part": "snippet",
            "type": "video",
            "videoCategoryId": "10",
            "videoEmbeddable": "true",
            "safeSearch": "moderate",
            "regionCode": "IN" if language != "English" else "US",
            "relevanceLanguage": LANGUAGES[language],
            "q": f"{language} song {year} official music video",
            "publishedAfter": f"{year}-01-01T00:00:00Z",
            "publishedBefore": f"{year + 1}-01-01T00:00:00Z",
            "maxResults": limit,
            "key": api_key,
        }
    )
    return request_json(f"{API_URL}?{query}").get("items", [])


def make_album(language: str, year: int, results: list[dict]) -> dict | None:
    songs = []
    for result in results:
        video_id = result.get("id", {}).get("videoId", "")
        snippet = result.get("snippet", {})
        if len(video_id) != 11:
            continue
        songs.append(
            {
                "title": html.unescape(snippet.get("title", "")).strip(),
                "artist": html.unescape(snippet.get("channelTitle", "")).strip(),
                "youTubeVideoId": video_id,
                "thumbnail": snippet.get("thumbnails", {}).get("high", {}).get(
                    "url", f"https://i.ytimg.com/vi/{video_id}/hqdefault.jpg"
                ),
                "publishedAt": snippet.get("publishedAt"),
            }
        )
    if not songs:
        return None

    language_number = list(LANGUAGES).index(language) + 1
    return {
        "id": 800_000_000_000 + language_number * 1_000_000 + year,
        "title": f"{language} Songs of {year}",
        "language": language,
        "year": year,
        "artist": "Various artists",
        "cover": songs[0]["thumbnail"],
        "sourceUrl": f"https://www.youtube.com/watch?v={songs[0]['youTubeVideoId']}",
        "songs": songs,
    }


def apply_album(api_url: str, admin_key: str, album: dict) -> None:
    request = urllib.request.Request(
        f"{api_url.rstrip('/')}/api/admin/albums/{album['id']}",
        data=json.dumps(album).encode(),
        method="PUT",
        headers={"Content-Type": "application/json", "X-Admin-Key": admin_key},
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        if response.status >= 300:
            raise RuntimeError(f"Album import failed with HTTP {response.status}")


def parse_args() -> argparse.Namespace:
    current_year = dt.datetime.now(dt.timezone.utc).year
    parser = argparse.ArgumentParser(
        description="Import Odia, Hindi and English YouTube song metadata."
    )
    parser.add_argument("--start-year", type=int, default=2000)
    parser.add_argument("--end-year", type=int, default=current_year)
    parser.add_argument("--languages", nargs="+", choices=LANGUAGES, default=list(LANGUAGES))
    parser.add_argument("--results-per-year", type=int, default=3, choices=range(1, 11))
    parser.add_argument("--output", type=pathlib.Path, default=pathlib.Path("albums.youtube.json"))
    parser.add_argument("--apply-url", help="Optional Cineclay API base URL")
    parser.add_argument("--delay", type=float, default=0.1, help="Delay between API requests")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    current_year = dt.datetime.now(dt.timezone.utc).year
    if args.start_year < 2000 or args.end_year > current_year or args.start_year > args.end_year:
        raise SystemExit(f"Years must be between 2000 and the current year ({current_year}).")

    api_key = os.environ.get("YOUTUBE_API_KEY", "")
    if not api_key:
        raise SystemExit("Set YOUTUBE_API_KEY before running this importer.")
    admin_key = os.environ.get("ADMIN_API_KEY", "")
    if args.apply_url and len(admin_key) < 32:
        raise SystemExit("Set ADMIN_API_KEY (at least 32 characters) when using --apply-url.")

    albums = []
    for language in args.languages:
        for year in range(args.start_year, args.end_year + 1):
            try:
                album = make_album(language, year, search(api_key, language, year, args.results_per_year))
            except urllib.error.HTTPError as error:
                detail = error.read().decode(errors="replace")
                raise SystemExit(f"YouTube API failed for {language} {year}: {detail}") from error
            if album:
                albums.append(album)
                if args.apply_url:
                    apply_album(args.apply_url, admin_key, album)
            time.sleep(args.delay)

    args.output.write_text(json.dumps(albums, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {len(albums)} albums and {sum(len(a['songs']) for a in albums)} songs to {args.output}")
    if args.apply_url:
        print(f"Applied {len(albums)} albums to {args.apply_url}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
