"""Summarize one or more Solarium episode CSV files.

Usage:
    python summarize_evaluation.py random.csv untrained.csv trained.csv
"""

from __future__ import annotations

import csv
import statistics
import sys
from pathlib import Path


def summarize(path: Path) -> None:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))
    rows = rows[-50:]
    if not rows:
        print(f"{path}: no episodes")
        return
    survival = [float(row["survival_time"]) for row in rows]
    food = [float(row["food_collected"]) for row in rows]
    healing = [float(row["healing_used"]) for row in rows]
    damage = [float(row["damage_taken"]) for row in rows]
    reasons = [row["termination_reason"] for row in rows]
    death_reasons = {"health_depleted", "energy_depleted", "lava"}
    death_rate = sum(reason in death_reasons for reason in reasons) / len(rows)
    lava_rate = sum(reason == "lava" for reason in reasons) / len(rows)
    print(
        f"{path.stem:24} n={len(rows):2d} "
        f"mean={statistics.fmean(survival):7.2f}s "
        f"median={statistics.median(survival):7.2f}s "
        f"std={statistics.pstdev(survival):7.2f}s "
        f"best={max(survival):7.2f}s "
        f"food={statistics.fmean(food):6.2f} "
        f"heals={statistics.fmean(healing):5.2f} "
        f"damage={statistics.fmean(damage):6.2f} "
        f"deaths={death_rate:6.1%} "
        f"lava={lava_rate:6.1%}"
    )


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit("Pass at least one episodes.csv file.")
    for argument in sys.argv[1:]:
        summarize(Path(argument))


if __name__ == "__main__":
    main()
