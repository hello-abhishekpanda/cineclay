#!/usr/bin/env python3
"""Build or apply a large Cineclay music catalog with YouTube Data API v3."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import html
import json
import os
import pathlib
import time
import urllib.error
import urllib.parse
import urllib.request


API_URL = "https://www.googleapis.com/youtube/v3/search"
LANGUAGES = {
    "Odia": "or",
    "Hindi": "hi",
    "English": "en",
    "Telugu": "te",
    "Malayalam": "ml",
    "Tamil": "ta",
    "Kannada": "kn",
    "Bengali": "bn",
    "Marathi": "mr",
    "Punjabi": "pa",
}


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
            "q": f"{language} music {year} official song",
            "publishedAfter": f"{year}-01-01T00:00:00Z",
            "publishedBefore": f"{year + 1}-01-01T00:00:00Z",
            "maxResults": limit,
            "key": api_key,
        }
    )
    return request_json(f"{API_URL}?{query}").get("items", [])


def stable_id(video_id: str) -> int:
    value = int.from_bytes(hashlib.sha256(video_id.encode()).digest()[:8], "big")
    return value & 0x7FFF_FFFF_FFFF_FFFF or 1


def make_album(language: str, year: int, result: dict) -> dict | None:
    video_id = result.get("id", {}).get("videoId", "")
    snippet = result.get("snippet", {})
    title = html.unescape(snippet.get("title", "")).strip()
    artist = html.unescape(snippet.get("channelTitle", "")).strip()
    if len(video_id) != 11 or not title or not artist:
        return None

    thumbnail = snippet.get("thumbnails", {}).get("high", {}).get(
        "url", f"https://i.ytimg.com/vi/{video_id}/hqdefault.jpg"
    )
    song = {
        "title": title,
        "artist": artist,
        "youTubeVideoId": video_id,
        "thumbnail": thumbnail,
        "publishedAt": snippet.get("publishedAt"),
    }
    return {
        "id": stable_id(video_id),
        "title": title,
        "language": language,
        "year": year,
        "artist": artist,
        "cover": thumbnail,
        "sourceUrl": f"https://www.youtube.com/watch?v={video_id}",
        "songs": [song],
    }


def save_json(path: pathlib.Path, value: object) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(path)


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
    current_year = dt.datetime.now(dt.timezone.utc).year
    parser = argparse.ArgumentParser(
        description="Collect a resumable, multilingual YouTube music catalog."
    )
    parser.add_argument("--start-year", type=int, default=2000)
    parser.add_argument("--end-year", type=int, default=current_year)
    parser.add_argument("--languages", nargs="+", choices=LANGUAGES, default=list(LANGUAGES))
    parser.add_argument("--results-per-query", type=int, default=50, choices=range(1, 51))
    parser.add_argument("--target", type=int, default=5001)
    parser.add_argument(
        "--max-requests",
        type=int,
        default=95,
        help="Maximum search requests in this run; the default leaves quota headroom.",
    )
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        default=pathlib.Path("albums.youtube.json"),
    )
    parser.add_argument(
        "--checkpoint",
        type=pathlib.Path,
        default=pathlib.Path(".catalog-cache/youtube-albums.checkpoint.json"),
    )
    parser.add_argument("--apply-url", help="Optional Cineclay API base URL")
    parser.add_argument("--delay", type=float, default=0.15, help="Delay between API requests")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    current_year = dt.datetime.now(dt.timezone.utc).year
    if args.start_year < 2000 or args.end_year > current_year or args.start_year > args.end_year:
        raise SystemExit(f"Years must be between 2000 and the current year ({current_year}).")
    if args.target < 1 or args.max_requests < 1:
        raise SystemExit("--target and --max-requests must be positive.")

    api_key = os.environ.get("YOUTUBE_API_KEY", "").strip()
    if not api_key:
        raise SystemExit("Set YOUTUBE_API_KEY before running this importer.")
    admin_key = os.environ.get("ADMIN_API_KEY", "")
    if args.apply_url and len(admin_key) < 32:
        raise SystemExit("Set ADMIN_API_KEY (at least 32 characters) when using --apply-url.")

    albums = json.loads(args.output.read_text(encoding="utf-8")) if args.output.exists() else []
    by_video = {
        song["youTubeVideoId"]: album
        for album in albums
        for song in album.get("songs", [])
        if song.get("youTubeVideoId")
    }
    checkpoint = (
        json.loads(args.checkpoint.read_text(encoding="utf-8"))
        if args.checkpoint.exists()
        else {"completed": []}
    )
    completed = set(checkpoint.get("completed", []))
    plan = [
        (language, year)
        for language in args.languages
        for year in range(args.end_year, args.start_year - 1, -1)
    ]

    requests = 0
    args.checkpoint.parent.mkdir(parents=True, exist_ok=True)
    for language, year in plan:
        query_id = f"{language}:{year}"
        if query_id in completed or len(by_video) >= args.target or requests >= args.max_requests:
            continue

        try:
            results = search(api_key, language, year, args.results_per_query)
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")
            save_json(args.output, list(by_video.values()))
            save_json(args.checkpoint, {"completed": sorted(completed)})
            raise SystemExit(f"YouTube API failed for {query_id}; progress was saved: {detail}") from error

        requests += 1
        for result in results:
            album = make_album(language, year, result)
            if album:
                video_id = album["songs"][0]["youTubeVideoId"]
                by_video.setdefault(video_id, album)

        completed.add(query_id)
        albums = list(by_video.values())
        save_json(args.output, albums)
        save_json(args.checkpoint, {"completed": sorted(completed)})
        print(f"{query_id}: {len(results)} results; {len(albums):,}/{args.target:,} unique albums")
        time.sleep(args.delay)

    albums = list(by_video.values())[: args.target]
    albums.sort(key=lambda album: (album["language"] != "Odia", -album["year"], album["title"]))
    save_json(args.output, albums)
    print(f"Wrote {len(albums):,} verified albums to {args.output} using {requests} requests")
    if len(albums) < args.target:
        print("Target not reached. Run the same command again after YouTube search quota resets.")
    if args.apply_url:
        apply_albums(args.apply_url, admin_key, albums)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
