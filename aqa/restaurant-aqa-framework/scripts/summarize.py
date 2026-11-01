"""Read actual TRX outcomes. Never infer Passed from process completion or an empty result set."""
import argparse
import html
import json
from pathlib import Path
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
parser.add_argument('--require-results', action='store_true')
parser.add_argument('--require-fresh-e2e', action='store_true')
args = parser.parse_args()
tests = []
files = sorted(args.directory.glob('*.trx'))
for path in files:
    root = ET.parse(path).getroot()
    ns = {'t': root.tag.split('}')[0].strip('{')}
    categories = {test.get('id'): {item.get('TestCategory') for item in test.findall('.//t:TestCategoryItem', ns)}
                  for test in root.findall('.//t:TestDefinitions/t:UnitTest', ns)}
    for test in root.findall('.//t:UnitTestResult', ns):
        message = test.find('.//t:Message', ns)
        if message is None:
            message = test.find('.//t:DebugTrace', ns)
        tags = categories.get(test.get('testId'), set())
        diagnostic = bool(tags.intersection({'diagnostic', 'nonacceptance', 'readonly', 'continuation'}))
        tests.append({'name': test.get('testName'), 'outcome': test.get('outcome'), 'tags': sorted(tags),
                      'classification': 'diagnostic-nonacceptance' if diagnostic else ('acceptance' if 'hw8' in tags else 'local-or-other'),
                      'duration': test.get('duration'), 'reason': message.text if message is not None else ''})
summary = {outcome: sum(t['outcome'] == outcome for t in tests) for outcome in ['Passed', 'Failed', 'NotExecuted']}
summary['Total'] = len(tests)
acceptance = [t for t in tests if t['classification'] == 'acceptance']
fresh_e2e_passed = any(t['outcome'] == 'Passed' and 'e2e' in t['tags'] for t in acceptance)
payload = {'counts': summary, 'acceptance_counts': {s: sum(t['outcome'] == s for t in acceptance) for s in ['Passed', 'Failed', 'NotExecuted']},
           'diagnostic_count': sum(t['classification'] == 'diagnostic-nonacceptance' for t in tests),
           'fresh_acceptance_e2e_passed': fresh_e2e_passed, 'tests': tests}
args.directory.mkdir(parents=True, exist_ok=True)
(args.directory / 'summary.json').write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding='utf-8', newline='\n')
rows = ''.join('<tr>' + ''.join(f'<td>{html.escape(str(t[k] or ""))}</td>' for k in ['name', 'classification', 'outcome', 'duration', 'reason']) + '</tr>' for t in tests)
evidence = {'acceptance': payload['acceptance_counts'], 'diagnostics': payload['diagnostic_count'], 'fresh_acceptance_e2e_passed': fresh_e2e_passed}
(args.directory / 'summary.html').write_text('<!doctype html><meta charset="utf-8"><title>Фактичні результати AQA</title><style>body{font:16px system-ui;margin:2rem}td,th{padding:.6rem;border:1px solid #bbb;vertical-align:top}table{border-collapse:collapse}td:last-child{white-space:pre-wrap}</style><h1>Фактичні результати AQA</h1><p>Діагностика не підтверджує свіжий E2E.</p><p>' + html.escape(str(evidence)) + '</p><p>' + html.escape(str(summary)) + '</p><table><tr><th>Сценарій</th><th>Класифікація</th><th>Результат</th><th>Тривалість</th><th>Причина</th></tr>' + rows + '</table>', encoding='utf-8')
print(f"AQA: Total={len(tests)}, Passed={summary['Passed']}, Failed={summary['Failed']}, Skipped={summary['NotExecuted']}")
print(f"Acceptance counts: {payload['acceptance_counts']}; diagnostics: {payload['diagnostic_count']}; fresh acceptance E2E passed: {fresh_e2e_passed}")
for test in tests:
    if test['outcome'] != 'Passed':
        reason = (test['reason'] or 'No reason recorded').splitlines()[0]
        print(f"- {test['outcome']}: {test['name']} — {reason}")
if args.require_results and not tests:
    raise SystemExit('FAILED: no test results produced')
if args.require_fresh_e2e and not fresh_e2e_passed:
    raise SystemExit('BLOCKED: no passing fresh acceptance E2E; continuation/readonly cannot satisfy this gate')
