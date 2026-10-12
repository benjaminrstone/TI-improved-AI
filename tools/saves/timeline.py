import gzip, json, sys, collections, os
P = 'PavonisInteractive.TerraInvicta.'
def ref(x): return x.get('value') if isinstance(x, dict) else None
def vals(gs, t): return {e['Key']['value']: e['Value'] for e in gs.get(P+t, []) if e['Value'].get('exists', True) and not e['Value'].get('archived')}
out = []
for path in sys.argv[1:]:
    gs = json.load(gzip.open(path, 'rt', encoding='utf-8-sig'))['gamestates']
    date = gs[P+'TIMetadataState'][0]['Value']['gameTimeString'].split()[0]
    facs = vals(gs, 'TIFactionState')
    alien = [k for k,v in facs.items() if 'Alien' in v.get('templateName','')][0]
    pl = [k for k,v in facs.items() if 'Resist' in v.get('templateName','')][0]
    fleets = vals(gs, 'TISpaceFleetState'); ships = vals(gs, 'TISpaceShipState')
    def fships(fid):
        return [ships[ref(s)] for f in fleets.values() if ref(f.get('faction'))==fid for s in f.get('ships',[]) if ref(s) in ships]
    a, p = fships(alien), fships(pl)
    afl = [f for f in fleets.values() if ref(f.get('faction'))==alien and f.get('ships')]
    sizes = sorted((len(f['ships']) for f in afl), reverse=True)
    amass = sum(s.get('currentMass_kg',0) for s in a)/1e9; pmass = sum(s.get('currentMass_kg',0) for s in p)/1e9
    designs = collections.Counter(s['templateName'].replace('AlienCouncilShipTemplate','A') for s in a)
    learned = sum(1 for f in afl if f.get('AI_FailedAttackEnemyStrength'))
    out.append((date, f"aShips={len(a):3d} aMass={amass:6.1f}kt  pShips={len(p):3d} pMass={pmass:6.1f}kt  aFleetSizes={sizes[:10]} learnedFail={learned}"))
def k(d): m,dd,y = map(int, d.split('/')); return (y,m,dd)
for d, s in sorted(out, key=lambda x: k(x[0])): print(d.rjust(10), s)
