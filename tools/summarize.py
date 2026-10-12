"""Human-readable story of one harness run.

usage: python -I tools/summarize.py lab/results/<run-dir> [--all]

Prints status, injections, alien attack launches (SCV vs desired vs expected threat), alien battles with
outcomes, force pooling, raids, the alien's perception of the player's fleets, habs lost, asserts.
--all also prints every goal change.
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


def f0(x):
    return "-" if x is None else f"{x:,.0f}"


def main(run, show_all=False):
    j = load(run, "journal.jsonl")
    m = load(run, "metrics.jsonl")
    res_path = os.path.join(run, "results.json")
    res = json.load(open(res_path, encoding="utf-8-sig")) if os.path.exists(res_path) else {}
    s = res.get("status") or {}
    print(f"== {os.path.basename(run)}")
    print(f"   {s.get('start')} -> {s.get('date')}  {s.get('gameDays')} days  {s.get('gameDaysPerRealMinute')} days/min  "
          f"end={res.get('endReason')}  exceptions={s.get('exceptions')}  stalls={s.get('stalls')}")
    print("   kinds:", dict(collections.Counter(r["kind"] for r in j)))

    for r in j:
        if r["kind"] == "inject":
            print("   INJECT", {k: v for k, v in r.items() if k not in ("kind",)})

    print("\n-- alien attack launches (fleet SCV / desired / expected threat)")
    for r in j:
        if r["kind"] == "transfer" and r.get("goal") == "AttackWithFleet":
            print(f"   {r['t']}  {r['ships']:>3} ships  scv {f0(r['scv']):>7}  desired {f0(r.get('desiredScv')):>7}  "
                  f"threat {f0(r.get('defThreat')):>7}  -> {r.get('goalTarget')} @ {r.get('toSystem')}")

    print("\n-- battles involving the aliens (side: starting fleet SCV, destroyed/assets)")
    for r in j:
        if r["kind"] == "combat" and r.get("occurred") and any(x["f"] == "AlienCouncil" for x in r["sides"]):
            sides = "  ".join(f"{x['f']}: {f0(x['fleetScv0'])} {x['destroyed']}/{x['assets']}" for x in r["sides"])
            print(f"   {r['t']}  {r.get('body')}  attacker={r.get('attacker')}  winner={r.get('winner')}  |  {sides}")

    pools = [r for r in j if r["kind"] in ("pool", "pool-ready")]
    if pools:
        print("\n-- force pooling (every 4th record)")
        for r in pools[::4] + [r for r in pools if r["kind"] == "pool-ready"]:
            d = r.get("donors") or []
            print(f"   {r['t']}  {r['kind']:<10} {r.get('target')} @ {r.get('targetSystem')}  have {f0(r['have'])} / need {f0(r['need'])}"
                  f"  donors {len(d)} ({f0(sum(x['scv'] for x in d))})")

    raids = [r for r in j if r["kind"] == "raid"]
    if raids:
        print("\n-- raid planner")
        for r in raids:
            print(f"   {r['t']}  open {r['open']}: " + "; ".join(f"{p['hab']} @ {p['body']} (v {p['value']:.1f}, threat {p['threat']:.0f})" for p in r["picks"]))

    per = [r for r in j if r["kind"] == "perceive"]
    if per:
        print("\n-- alien perception of the player's fleets (true SCV / perceived), per system")
        for r in per[::2]:
            print(f"   {r['t']}  " + "  ".join(f"{k}: {f0(v['scv'])}/{f0(v['perceived'])}" for k, v in sorted(r["systems"].items()) if v["scv"] or v["perceived"]))

    lost = collections.Counter((r["faction"], r["habType"]) for r in j if r["kind"] == "hab-")
    print("\n-- habs lost:", dict(lost))
    if m:
        def fac(row, name):
            return next((x for x in row.get("factions", []) if x.get("f") == name), {})
        a0, a1 = fac(m[0], "AlienCouncil"), fac(m[-1], "AlienCouncil")
        print(f"-- alien SCV {f0(a0.get('scv'))} -> {f0(a1.get('scv'))}; ships {a0.get('ships')} -> {a1.get('ships')}; "
              f"bases {a0.get('bases')} -> {a1.get('bases')}")
    if show_all:
        print("\n-- goal changes")
        for r in j:
            if r["kind"].startswith("goal"):
                print("  ", {k: v for k, v in r.items() if v is not None})
    ap = os.path.join(run, "asserts.json")
    if os.path.exists(ap):
        print("\n-- asserts")
        for a in json.load(open(ap, encoding="utf-8")):
            print(f"   {'PASS' if a['pass'] else 'FAIL'}  {a['expr']}" + (f"  [{a['error']}]" if a.get("error") else ""))


if __name__ == "__main__":
    main(sys.argv[1], "--all" in sys.argv)
