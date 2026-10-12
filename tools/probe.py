"""Evaluate a probe's asserts against one harness run's output.

usage: python -I tools/probe.py lab/probes/<probe>.json lab/results/<run-dir>

Asserts are Python expressions evaluated with these names in scope:
  journal   list of journal records (dicts; see DecisionJournal.cs for kinds)
  metrics   list of monthly metrics rows
  results   the run's results.json
  status    results["status"] (date, gameDays, realSeconds, gameDaysPerRealMinute, exceptions, stalls, ...)
  count(kind, **match)      number of journal records of that kind whose fields match (callables allowed as values)
  events(kind, **match)     those records
  faction(row, name)        the faction entry in a metrics row (name = template name, e.g. "AlienCouncil")
  first, last               first and last metrics rows
  median, mean              from statistics; plus len/sum/min/max/any/all/abs/round/sorted/set
Exit code 0 if every assert passes, 2 otherwise.
"""
import json
import os
import statistics
import sys


SAFE_BUILTINS = {"len": len, "sum": sum, "min": min, "max": max, "any": any, "all": all, "abs": abs, "round": round,
                 "sorted": sorted, "set": set, "median": statistics.median, "mean": statistics.mean}


def load_jsonl(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def main(probe_path, run_dir):
    with open(probe_path, encoding="utf-8") as f:
        probe = json.load(f)
    journal = load_jsonl(os.path.join(run_dir, "journal.jsonl"))
    metrics = load_jsonl(os.path.join(run_dir, "metrics.jsonl"))
    with open(os.path.join(run_dir, "results.json"), encoding="utf-8-sig") as f:
        results = json.load(f)

    def matches(rec, match):
        for k, v in match.items():
            val = rec.get(k)
            if callable(v):
                if not v(val):
                    return False
            elif val != v:
                return False
        return True

    def events(kind, **match):
        return [r for r in journal if r.get("kind") == kind and matches(r, match)]

    def count(kind, **match):
        return len(events(kind, **match))

    def faction(row, name):
        return next((x for x in row.get("factions", []) if x.get("f") == name), {})

    scope = dict(journal=journal, metrics=metrics, results=results, status=results.get("status") or {},
                 count=count, events=events, faction=faction,
                 first=metrics[0] if metrics else {}, last=metrics[-1] if metrics else {})

    outcomes = []
    for expr in probe.get("asserts", []):
        try:
            ok = bool(eval(expr, dict(scope, __builtins__=SAFE_BUILTINS)))  # one namespace, so comprehensions see it
            err = None
        except Exception as e:  # an assert that can't evaluate is a failure, with the reason shown
            ok, err = False, f"{type(e).__name__}: {e}"
        outcomes.append({"expr": expr, "pass": ok, "error": err})

    info = []
    for expr in probe.get("info", []):  # tracked numbers: printed and saved, never fail the probe
        try:
            info.append({"expr": expr, "value": eval(expr, dict(scope, __builtins__=SAFE_BUILTINS))})
        except Exception as e:
            info.append({"expr": expr, "value": f"{type(e).__name__}: {e}"})

    print(f"\n--- probe {probe.get('name')} ({len(journal)} journal records, {len(metrics)} metrics rows) ---")
    if probe.get("expect"):
        print(f"expect: {probe['expect']}")
    for o in outcomes:
        print(f"{'PASS' if o['pass'] else 'FAIL'}  {o['expr']}" + (f"   [{o['error']}]" if o["error"] else ""))
    for i in info:
        print(f"INFO  {i['expr']} = {i['value']}")
    with open(os.path.join(run_dir, "asserts.json"), "w", encoding="utf-8") as f:
        json.dump(outcomes + [dict(i, info=True) for i in info], f, indent=2, default=str)
    return 0 if all(o["pass"] for o in outcomes) else 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
