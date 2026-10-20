"""AI human fleet output of runs: net SCV change, SCV lost in combat, and gross SCV built (net + lost); with
human-ship-built records (2026-10-19 on), also the ships actually completed (free of combat luck).

Fleet SCV alone hides combat losses (in the 2034 soaks the AI humans lost 3.6-5.1k SCV, mostly to the aliens, against
~3k net growth), so compare gross built. human-fleet records give SCV; combat records give losses.

usage: python tools/hfleet.py [--months N] <run dir> [<run dir> ...]
"""
import json
import sys
from collections import Counter
from pathlib import Path

AI = ['DestroyCouncil', 'CooperateCouncil', 'ExploitCouncil', 'EscapeCouncil', 'SubmitCouncil', 'AppeaseCouncil']


def month_add(t, n):
    y, m = int(t[:4]), int(t[5:7]) - 1 + n
    return f'{y + m // 12:04d}-{m % 12 + 1:02d}'


def one(run, months=None):
    rows = [json.loads(l) for l in open(Path(run) / 'journal.jsonl', encoding='utf8')]
    hf = [r for r in rows if r.get('kind') == 'human-fleet']
    ts = sorted({r['t'] for r in hf})
    end = ts[-1] if months is None else max(t for t in ts if t[:7] <= month_add(ts[0], months))
    a = {r['f']: r for r in hf if r['t'] == ts[0]}
    b = {r['f']: r for r in hf if r['t'] == end}
    lost, vs = Counter(), Counter()
    for r in rows:
        if r.get('kind') != 'combat' or not r.get('occurred') or not (ts[0] <= r['t'] <= end):
            continue
        fs = [s['f'] for s in r['sides']]
        for s in r['sides']:
            if s['f'] in AI:
                x = max(0.0, s['fleetScv0'] - s['assetScvEnd'])
                lost[s['f']] += x
                vs['alien' if any('Alien' in o for o in fs if o != s['f']) else 'human'] += x
    net = {f: b.get(f, {}).get('scv', 0) - a.get(f, {}).get('scv', 0) for f in AI}
    gross = sum(net.values()) + sum(lost.values())
    ships = sum(b.get(f, {}).get('ships', 0) for f in AI)
    built = [r for r in rows if r.get('kind') == 'human-ship-built' and r.get('f') in AI and ts[0] <= r['t'] <= end]
    mc = sum(b.get(f, {}).get('mcIncome', 0) for f in AI)
    gen = sum(b.get(f, {}).get('mcGenericAvailable', 0) for f in AI)
    print(f"{Path(run).name[16:]:55s} {ts[0][:7]}->{end[:7]}  net {sum(net.values()):6.0f}  lost {sum(lost.values()):6.0f}"
          f" (alien {vs['alien']:.0f})  gross {gross:6.0f}  per ship {sum(b.get(f, {}).get('scv', 0) for f in AI) / max(1, ships):4.0f}"
          f"  MC {mc:5.0f} generic {gen:4.0f}" + (f"  BUILT {len(built)} ships, {sum(r.get('scv', 0) for r in built):.0f} SCV" if built else ''))
    return gross


if __name__ == '__main__':
    args = sys.argv[1:]
    months = None
    if args and args[0] == '--months':
        months, args = int(args[1]), args[2:]
    for r in args:
        one(r, months)
