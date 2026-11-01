"""Inventory this suite's Gherkin examples without executing BDD hooks or accessing an environment."""
import argparse
from collections import Counter
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path)
args = parser.parse_args()
scenarios = []
for path in sorted((Path(__file__).resolve().parents[1] / 'Features').glob('*.feature')):
    feature_tags, pending, current = [], [], None
    for raw in path.read_text(encoding='utf-8-sig').splitlines() + ['Scenario: END']:
        line = raw.strip()
        if line.startswith('@'):
            pending += [tag[1:] for tag in line.split()]
        elif line.startswith('Feature:'):
            feature_tags, pending = pending, []
        elif line.startswith(('Scenario:', 'Scenario Outline:')):
            if current:
                rows = current.pop('rows')
                outline = current.pop('outline')
                if outline and len(rows) < 2:
                    raise SystemExit('Outline must have examples: ' + current['name'])
                for row in rows[1:] if outline else [None]:
                    item = dict(current)
                    if row is not None:
                        item['name'] += ' (' + ', '.join(row) + ')'
                    scenarios.append(item)
            current = {'file': path.name, 'name': line.split(':', 1)[1].strip(), 'tags': feature_tags + pending,
                       'outline': line.startswith('Scenario Outline:'), 'rows': []}
            pending = []
        elif line.startswith('|') and current:
            current['rows'].append([cell.strip() for cell in line.strip('|').split('|')])
    # END is only a flush sentinel, never added to the inventory.
counts = Counter()
for scenario in scenarios:
    tags = set(scenario['tags'])
    levels = tags.intersection({'l1', 'l2', 'l3'})
    diagnostic = bool(tags.intersection({'diagnostic', 'readonly', 'continuation'}))
    if len(tags.intersection({'ui', 'api'})) != 1:
        raise SystemExit('Exactly one stage required: ' + scenario['name'])
    if diagnostic:
        if 'hw8' in tags or levels or 'e2e' in tags or 'nonacceptance' not in tags:
            raise SystemExit('Diagnostic must not masquerade as level/fresh E2E acceptance: ' + scenario['name'])
        scenario['classification'] = 'diagnostic-nonacceptance'
    else:
        if 'hw8' not in tags or len(levels) != 1:
            raise SystemExit('Business scenario needs hw8 and one level: ' + scenario['name'])
        scenario['classification'] = next(iter(levels))
    counts[scenario['classification']] += 1
expected = {'l1': 10, 'l2': 13, 'l3': 11, 'diagnostic-nonacceptance': 2}
if dict(counts) != expected:
    raise SystemExit(f'Review the inventory baseline: expected {expected}, actual {dict(counts)}')
payload = {'total': len(scenarios), 'counts': dict(counts), 'fresh_e2e_scenarios': sum('e2e' in s['tags'] for s in scenarios),
           'fresh_e2e_verified_by_discovery': False, 'scenarios': scenarios}
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
print(json.dumps({k: v for k, v in payload.items() if k != 'scenarios'}))
