#!/usr/bin/env python3
"""Copy the validated API album seed into the dependency-free static preview."""

from __future__ import annotations

import json
import pathlib


ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/Cineclay.Api/albums.seed.json"
TARGET = ROOT / "dist/albums.json"


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
    print(f"Wrote {len(albums):,} albums to {TARGET.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
