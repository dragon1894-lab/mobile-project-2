import sys
from collections import Counter
from pathlib import Path

ROOT = Path(sys.argv[1])
for rel in sys.argv[2:]:
    d = ROOT / rel
    stats = Counter()
    ratios = []
    odd = []
    for f in sorted(d.rglob('*.txt')):
        if f.name == 'classes.txt':
            continue
        rows = [list(map(float, l.split())) for l in f.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
        stats[f'n={len(rows)}'] += 1
        if len(rows) == 2:
            a, b = sorted(rows, key=lambda r: -r[3] * r[4])
            inside = (b[1] - b[3] / 2 >= a[1] - a[3] / 2 - 0.02 and b[1] + b[3] / 2 <= a[1] + a[3] / 2 + 0.02 and
                      b[2] - b[4] / 2 >= a[2] - a[4] / 2 - 0.02 and b[2] + b[4] / 2 <= a[2] + a[4] / 2 + 0.02)
            r = (b[3] * b[4]) / (a[3] * a[4])
            ratios.append(r)
            stats['small_inside_big' if inside else 'NOT_inside'] += 1
            if not inside or r > 0.5:
                odd.append((f.name, round(r, 2), inside))
        elif len(rows) == 1:
            stats['single_area>0.05' if rows[0][3] * rows[0][4] > 0.05 else 'single_small(<5%)'] += 1
            if rows[0][3] * rows[0][4] <= 0.05:
                odd.append((f.name, 'single small box', round(rows[0][3] * rows[0][4], 3)))
    print('==', rel, dict(stats))
    if ratios:
        ratios.sort()
        print('   inner/outer area ratio min/median/max', round(ratios[0], 3), round(ratios[len(ratios) // 2], 3), round(ratios[-1], 3))
    print('   odd', odd[:10], len(odd))
