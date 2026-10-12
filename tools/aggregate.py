"""Aggregate repeated soaks: mean / min / max of per-run totals, grouped by variant (vanilla vs mod).

usage: python -I tools/aggregate.py <glob-pattern-under-lab/results>   e.g.  "soak-accel-2031-hot-war-r*"
"""
import glob
import os
import statistics
import sys

sys.path.insert(0, os.path.dirname(__file__))
from compare import load, totals, fac  # noqa: E402


def final_alien(run):
    m = load(run, "metrics.jsonl")
    return fac(m[-1], "AlienCouncil").get("scv") if m else None


def main(pattern, human="ResistCouncil"):
    root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lab", "results")
    runs = sorted(glob.glob(os.path.join(root, "*" + pattern)))
    groups = {"vanilla": [], "mod": []}
    for r in runs:
        if not os.path.exists(os.path.join(r, "results.json")):
            continue
        groups["vanilla" if r.endswith("-vanilla") else "mod"].append(r)
    stats = {}
    for g, rs in groups.items():
        rows = []
        for r in rs:
            t = totals(load(r, "journal.jsonl"), human)
            t["alien SCV at end"] = final_alien(r)
            rows.append(t)
        stats[g] = rows
    keys = [k for k in (stats["mod"] or stats["vanilla"] or [{}])[0].keys() if k != "exceptions"]
    print(f"runs: vanilla {len(stats['vanilla'])}, mod {len(stats['mod'])}")
    print(f"{'metric':<28} {'vanilla mean [min-max]':>26} {'mod mean [min-max]':>26}")
    for k in keys:
        cells = []
        for g in ("vanilla", "mod"):
            vals = [row[k] for row in stats[g] if row.get(k) is not None]
            cells.append(f"{statistics.mean(vals):8.1f} [{min(vals):.0f}-{max(vals):.0f}]" if vals else "-")
        print(f"{k:<28} {cells[0]:>26} {cells[1]:>26}")


if __name__ == "__main__":
    main(sys.argv[1])
