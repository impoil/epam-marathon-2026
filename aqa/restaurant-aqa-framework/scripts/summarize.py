"""Read actual TRX outcomes. Never infer Passed from process completion or an empty result set."""
import argparse
import html
import json
from pathlib import Path
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
parser.add_argument('--require-results', action='store_true')
args = parser.parse_args()
tests = []
files = sorted(args.directory.glob('*.trx'))
for path in files:
    root = ET.parse(path).getroot()
    ns = {'t': root.tag.split('}')[0].strip('{')}
    for test in root.findall('.//t:UnitTestResult', ns):
        message = test.find('.//t:Message', ns)
        if message is None:
            message = test.find('.//t:DebugTrace', ns)
        tests.append({'name': test.get('testName'), 'outcome': test.get('outcome'),
                      'duration': test.get('duration'), 'reason': message.text if message is not None else ''})
summary = {outcome: sum(t['outcome'] == outcome for t in tests) for outcome in ['Passed', 'Failed', 'NotExecuted']}
summary['Total'] = len(tests)
payload = {'counts': summary, 'tests': tests}
args.directory.mkdir(parents=True, exist_ok=True)
(args.directory / 'summary.json').write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding='utf-8')
rows = ''.join('<tr>' + ''.join(f'<td>{html.escape(str(t[k] or ""))}</td>' for k in ['name', 'outcome', 'duration', 'reason']) + '</tr>' for t in tests)
(args.directory / 'summary.html').write_text('<!doctype html><meta charset="utf-8"><title>AQA actual results</title><style>body{font:16px system-ui;margin:2rem}td,th{padding:.6rem;border:1px solid #bbb;vertical-align:top}table{border-collapse:collapse}td:last-child{white-space:pre-wrap}</style><h1>AQA actual results</h1><p>' + html.escape(str(summary)) + '</p><table><tr><th>Scenario</th><th>Outcome</th><th>Duration</th><th>Reason</th></tr>' + rows + '</table>', encoding='utf-8')
print(f"AQA: Total={len(tests)}, Passed={summary['Passed']}, Failed={summary['Failed']}, Skipped={summary['NotExecuted']}")
for test in tests:
    if test['outcome'] != 'Passed':
        reason = (test['reason'] or 'No reason recorded').splitlines()[0]
        print(f"- {test['outcome']}: {test['name']} — {reason}")
if args.require_results and not tests:
    raise SystemExit('FAILED: no test results produced')
