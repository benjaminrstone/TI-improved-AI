"""Compare two harness runs (typically vanilla vs mod from the same fixture) month by month.

usage: python -I tools/compare.py lab/results/<runA> lab/results/<runB> [--faction ResistCouncil]

Prints, per metrics row: alien SCV / ships / bases, the chosen human faction's SCV / bases / stations / hate,
plus per-run totals of habs lost by faction, alien battles won/lost, attack launches, pooling and raids.
"""
import collections
import json
import os
import sys


def load(run, name):
    p = os.path.join(run, name)
    if not os.path.exists(p):
        return []
    with open(p, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def fac(row, name):
    return next((x for x in row.get("factions", []) if x.get("f") == name), {})


def k(x):
    return "-" if x is None else (f"{x/1000:.1f}k" if abs(x) >= 1000 else f"{x:.0f}")


def totals(j, human):
    lost = collections.Counter(r["faction"] for r in j if r["kind"] == "hab-")
    battles = [r for r in j if r["kind"] == "combat" and r.get("occurred") and any(s["f"] == "AlienCouncil" for s in r["sides"])]
    won = sum(1 for r in battles if r.get("winner") == "AlienCouncil")
    alien_lost = sum(s["destroyed"] for r in battles for s in r["sides"] if s["f"] == "AlienCouncil")
    human_lost = sum(s["destroyed"] for r in battles for s in r["sides"] if s["f"] != "AlienCouncil")
    launches = [r for r in j if r["kind"] == "transfer" and r.get("goal") == "AttackWithFleet"]
    return {
        "habs lost (all)": sum(lost.values()),
        f"habs lost ({human})": lost.get(human, 0),
        "alien battles": len(battles),
        "alien won": won,
        "alien ships lost": alien_lost,
        "human ships lost vs aliens": human_lost,
        "alien attack launches": len(launches),
        "launches at player": sum(1 for r in launches if r.get("targetFaction") == human),
        "pool-ready": sum(1 for r in j if r["kind"] == "pool-ready"),
        "raid picks": sum(len(r["picks"]) for r in j if r["kind"] == "raid"),
        "exceptions": None,
    }


def main(a, b, human="ResistCouncil"):
    runs = [(a, load(a, "metrics.jsonl"), load(a, "journal.jsonl")), (b, load(b, "metrics.jsonl"), load(b, "journal.jsonl"))]
    print(f"A = {os.path.basename(a)}\nB = {os.path.basename(b)}\n")
    hdr = f"{'date':<11}| {'alienSCV':>8} {'ships':>5} {'bases':>5} | {human[:6]+' SCV':>10} {'bases':>5} {'stns':>4} {'hate':>5}"
    print(f"{'':<11}{'A':^40}|{'B':^40}")
    print(hdr.replace("| ", "") + " || " + hdr[11:].replace("| ", ""))
    rows = max(len(runs[0][1]), len(runs[1][1]))
    for i in range(rows):
        cells = []
        date = None
        for _, m, _ in runs:
            if i < len(m):
                r = m[i]
                date = date or r.get("t")
                al, hu = fac(r, "AlienCouncil"), fac(r, human)
                cells.append(f"{k(al.get('scv')):>8} {al.get('ships', '-'):>5} {al.get('bases', '-'):>5}  {k(hu.get('scv')):>10} {hu.get('bases', '-'):>5} {hu.get('stations', '-'):>4} {k(hu.get('hate')):>5}")
            else:
                cells.append(" " * 40)
        print(f"{date or '':<11}" + " || ".join(cells))
    print()
    ta, tb = totals(runs[0][2], human), totals(runs[1][2], human)
    for key in ta:
        if key == "exceptions":
            continue
        print(f"  {key:<28} A {ta[key]!s:>6}   B {tb[key]!s:>6}")
    for run, _, _ in runs:
        rp = os.path.join(run, "results.json")
        if os.path.exists(rp):
            s = (json.load(open(rp, encoding="utf-8-sig")).get("status") or {})
            print(f"  {os.path.basename(run)}: {s.get('gameDays')} days, {s.get('gameDaysPerRealMinute')} d/min, exceptions {s.get('exceptions')}, stalls {s.get('stalls')}, unsticks {s.get('unsticks')}")


if __name__ == "__main__":
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    human = sys.argv[sys.argv.index("--faction") + 1] if "--faction" in sys.argv else "ResistCouncil"
    if "--faction" in sys.argv:
        args = [x for x in args if x != human]
    main(args[0], args[1], human)
