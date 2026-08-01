"""Summarize Solarium infinite-world evaluation CSV files.

Usage:
    python summarize_infinite_evaluation.py infinite-episodes.csv
"""

from __future__ import annotations

import csv
import statistics
import sys
from pathlib import Path


def number(row: dict[str, str], key: str) -> float:
    return float(row.get(key, "0") or 0)


def summarize(path: Path) -> None:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))[-10:]
    if not rows:
        print(f"{path}: no episodes")
        return

    survival = [number(row, "survival_time") for row in rows]
    chunks = [number(row, "chunks_discovered") for row in rows]
    revisited = [number(row, "chunks_revisited") for row in rows]
    deposited = sum(number(row, "rations_deposited") for row in rows)
    consumed = sum(number(row, "rations_consumed") for row in rows)
    eligible_ratio = consumed / deposited if deposited else 0.0
    print(
        f"{path.stem}: n={len(rows)} "
        f"survival_median={statistics.median(survival):.2f}s "
        f"chunks_median={statistics.median(chunks):.1f} "
        f"runs_with_revisit={sum(value > 0 for value in revisited) / len(rows):.0%} "
        f"reserve_use={eligible_ratio:.0%} ({consumed:.0f}/{deposited:.0f})"
    )


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit("Pass at least one infinite-episodes.csv file.")
    for argument in sys.argv[1:]:
        summarize(Path(argument))


if __name__ == "__main__":
    main()
