"""The gang-up web of one run: who hated whom, and why (human-hate records; docs/HUMAN-AI.md §9).

usage: python tools/hate.py <run dir>
Prints, per cause bucket, the matrix of hate gained (row = the faction that hates, column = its target) summed over
the run; then the final hate, the months at war, and who was the strongest faction over time.
"""
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

SHORT = {'ResistCouncil': 'Resist', 'DestroyCouncil': 'HF', 'EscapeCouncil': 'Exodus', 'ExploitCouncil': 'Init',
         'CooperateCouncil': 'Academy', 'AppeaseCouncil': 'Protect', 'SubmitCouncil': 'Servants'}


def short(f):
    return SHORT.get(f, f or '-')


def main(run):
    rows = [json.loads(l) for l in open(Path(run) / 'journal.jsonl', encoding='utf8') if '"human-hate"' in l]
    if not rows:
        print('no human-hate records'); return
    names = sorted({r['f'] for r in rows}, key=lambda f: list(SHORT).index(f) if f in SHORT else 99)
    player = next((r['f'] for r in rows if r.get('player')), None)
    gained = defaultdict(lambda: defaultdict(float))   # bucket -> (from, to) -> sum
    war = Counter()
    last = {}
    for r in rows:
        last[r['f']] = r
        for to, v in r['toward'].items():
            for b, x in (v.get('gained') or {}).items():
                gained[b][(r['f'], to)] += x
            if v.get('war'):
                war[(r['f'], to)] += 1

    def matrix(title, cell):
        print(f'\n{title}  (row hates column; player = {short(player)})')
        print('          ' + ''.join(short(n).rjust(9) for n in names))
        for a in names:
            print(short(a).ljust(10) + ''.join(('' if a == b else cell(a, b)).rjust(9) for b in names))

    for b in sorted(gained, key=lambda k: -sum(abs(x) for x in gained[k].values())):
        matrix(f'gained: {b}', lambda a, c, b=b: f"{gained[b].get((a, c), 0):.0f}")
    matrix('final hate', lambda a, c: f"{last[a]['toward'].get(c, {}).get('hate', 0):.0f}")
    matrix('months at war', lambda a, c: str(war.get((a, c), 0)))
    print('\nstrongest by month:', ' '.join(f"{r['t'][:7]}:{short(r['strongest'])}" for r in rows if r['f'] == names[0]))
    print('self-assessment (last):', {short(f): last[f]['self'] for f in names})
    print('strength (last):', {short(f): last[f]['strength'] for f in names})


if __name__ == '__main__':
    main(sys.argv[1])
