#!/usr/bin/env python3
"""Copy the validated API album seed into the dependency-free static preview."""

from __future__ import annotations

import json
import pathlib


ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/Cineclay.Api/albums.seed.json"
TARGET = ROOT / "dist/albums.json"
WEB_ROOT = ROOT / "src/Cineclay.Web/wwwroot"


def main() -> int:
    albums = json.loads(SOURCE.read_text(encoding="utf-8"))
    if len(albums) < 5001:
        raise SystemExit("The API album seed must contain at least 5,001 releases.")
    if not any(album.get("songs") for album in albums):
        raise SystemExit("The album catalog must retain at least one playable preview.")

    TARGET.write_text(
        json.dumps(albums, ensure_ascii=False, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )
    (ROOT / "dist/splash.js").write_text(
        (WEB_ROOT / "splash.js").read_text(encoding="utf-8"),
        encoding="utf-8",
    )
    styles = (WEB_ROOT / "style.css").read_text(encoding="utf-8")
    marker = "/* Opening industry showcase */"
    if marker not in styles:
        raise SystemExit("The shared splash stylesheet marker is missing.")
    (ROOT / "dist/splash.css").write_text(styles[styles.index(marker) :], encoding="utf-8")
    print(f"Wrote {len(albums):,} albums to {TARGET.relative_to(ROOT)}")
    print("Synchronized the static opening showcase assets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
