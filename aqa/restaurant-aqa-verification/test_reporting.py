"""Offline report contract tests: synthetic TRX only, never rewrite historical results."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'restaurant-aqa-framework/scripts/summarize.py'
TEMP_ROOT = Path(__file__).resolve().parent / 'TestResults/report-contract'
TEMP_ROOT.mkdir(parents=True, exist_ok=True)


class ReportingTests(unittest.TestCase):
    def run_report(self, tags, outcome='Passed'):
        with tempfile.TemporaryDirectory(dir=TEMP_ROOT) as directory:
            root = Path(directory)
            categories = ''.join(f'<TestCategoryItem TestCategory="{tag}" />' for tag in tags)
            (root / 'synthetic.trx').write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
            <TestDefinitions><UnitTest id="one"><TestCategory>{categories}</TestCategory></UnitTest></TestDefinitions>
            <Results><UnitTestResult testId="one" testName="synthetic" outcome="{outcome}" duration="00:00:01" /></Results></TestRun>''', encoding='utf-8')
            result = subprocess.run([sys.executable, str(SCRIPT), directory, '--require-results', '--require-fresh-e2e'], capture_output=True, text=True)
            return result.returncode, json.loads((root / 'summary.json').read_text(encoding='utf-8'))

    def test_continuation_cannot_satisfy_fresh_gate_even_with_e2e_tag(self):
        code, report = self.run_report(['hw8', 'e2e', 'continuation'])
        self.assertNotEqual(code, 0)
        self.assertEqual(report['counts']['Passed'], 1)
        self.assertEqual(report['acceptance_counts']['Passed'], 0)
        self.assertFalse(report['fresh_acceptance_e2e_passed'])

    def test_readonly_is_diagnostic(self):
        code, report = self.run_report(['readonly', 'ui'])
        self.assertNotEqual(code, 0)
        self.assertEqual(report['diagnostic_count'], 1)

    def test_failed_fresh_e2e_does_not_pass_gate(self):
        code, report = self.run_report(['hw8', 'l3', 'e2e'], 'Failed')
        self.assertNotEqual(code, 0)
        self.assertEqual(report['acceptance_counts']['Failed'], 1)

    def test_passing_fresh_e2e_is_identified(self):
        code, report = self.run_report(['hw8', 'l3', 'e2e'])
        self.assertEqual(code, 0)
        self.assertTrue(report['fresh_acceptance_e2e_passed'])

    def test_empty_directory_is_not_success(self):
        with tempfile.TemporaryDirectory(dir=TEMP_ROOT) as directory:
            result = subprocess.run([sys.executable, str(SCRIPT), directory, '--require-results'], capture_output=True)
            self.assertNotEqual(result.returncode, 0)

    def test_manual_workflow_stays_suspended_without_secret_or_test_steps(self):
        workflow = Path(__file__).resolve().parents[2] / '.github/workflows/aqa-e2e.yml'
        text = '\n'.join(line.split('#', 1)[0] for line in workflow.read_text(encoding='utf-8').splitlines())
        for forbidden in ('secrets.', 'environment:', 'max_reservations', 'dotnet ', 'playwright', 'uses:', 'schedule:', 'pull_request'):
            self.assertNotIn(forbidden, text)
        self.assertIn('permissions: {}', text)
        self.assertIn('exit 1', text)


if __name__ == '__main__':
    unittest.main()
