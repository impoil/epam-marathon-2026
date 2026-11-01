"""Run local report/workflow contract checks and persist their actual unittest counts."""
import argparse
import json
from pathlib import Path
import unittest

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parent / 'TestResults/reporting-contract.json')
args = parser.parse_args()
suite = unittest.defaultTestLoader.discover(str(Path(__file__).resolve().parent), pattern='test_*.py')
result = unittest.TextTestRunner(verbosity=2).run(suite)
report = {'scope': 'offline synthetic TRX and frozen workflow contracts', 'counts': {
    'Total': result.testsRun, 'Passed': result.testsRun - len(result.failures) - len(result.errors) - len(result.skipped),
    'Failed': len(result.failures) + len(result.errors), 'Skipped': len(result.skipped)},
    'failures': [{'test': str(test), 'reason': error} for test, error in result.failures + result.errors]}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
raise SystemExit(0 if result.wasSuccessful() else 1)
