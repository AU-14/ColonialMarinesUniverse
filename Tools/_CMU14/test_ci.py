"""CMU14: protect coverage decisions and test-process failure handling."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from ci import select_tests


RUNNER = Path(__file__).with_name("ci.py")
TRX = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Results><UnitTestResult testName="Example.Gameplay" outcome="Passed" duration="00:00:01.250" /></Results>
<ResultSummary outcome="{outcome}"><Counters total="{count}" executed="{count}" passed="{count}"
failed="0" notExecuted="0" /></ResultSummary></TestRun>'''


class CiTests(unittest.TestCase):
    def test_planner_keeps_shared_and_uncertain_changes_in_both_suites(self):
        for paths in (None, [], ["README.md", "Content.Shared/Movement.cs"],
                      ["Content.CMU/Tests/PairHelper.cs"], ["Resources/Guidebook/new.md"],
                      ["unknown/input.dat"], ["Content.CMU/IntegrationTests/Shared.props"]):
            with self.subTest(paths=paths):
                selected = select_tests("pull_request", paths)
                self.assertTrue(selected["unit"] and selected["gameplay"])

    def test_planner_narrows_only_proven_independent_inputs(self):
        docs = select_tests("pull_request", ["README.md", ".github/CI.md"])
        self.assertFalse(docs["build"] or docs["unit"] or docs["gameplay"])
        integration = select_tests("pull_request", ["Content.CMU/IntegrationTests/Yautja/Bow.cs", "README.md"])
        self.assertFalse(integration["unit"])
        self.assertTrue(integration["build"] and integration["gameplay"])
        for event in ("push", "merge_group"):
            self.assertFalse(select_tests(event, ["README.md"])["gameplay"])
            self.assertTrue(select_tests(event, None)["gameplay"])

    def test_plan_consumes_old_rename_path_and_falls_back_on_bad_diff(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            changes = root / "changes.json"
            output = root / "outputs"
            env = {**os.environ, "GITHUB_OUTPUT": str(output), "GITHUB_STEP_SUMMARY": str(root / "summary")}
            # The API supplies both names of a rename; the old code path prevents a docs-only skip.
            for content in (json.dumps(["README.md", "Content.Shared/Old.cs"]), "invalid JSON"):
                changes.write_text(content)
                output.unlink(missing_ok=True)
                process = subprocess.run([sys.executable, str(RUNNER), "plan", "--event", "pull_request",
                                          "--changes", str(changes)], cwd=root, env=env, capture_output=True, text=True)
                self.assertEqual(process.returncode, 0, process.stderr)
                values = dict(line.split("=", 1) for line in output.read_text().splitlines())
                self.assertEqual(values["gameplay"], "true")
                self.assertGreater(len(json.loads(values["matrix"])["lane"]), 0)

    def execute(self, code, *, timeout=5, old_result=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            trx = root / "results.trx"
            if old_result:
                trx.write_text(TRX.format(outcome="Completed", count=1))
            env = {**os.environ, "GITHUB_STEP_SUMMARY": str(root / "summary.md")}
            result = subprocess.run([
                sys.executable, str(RUNNER), "run", "--name", "Regression", "--log", str(root / "test.log"),
                "--trx", str(trx), "--timeout", str(timeout), "--heartbeat", "0.1", "--",
                sys.executable, "-u", "-c", code,
            ], cwd=root, env=env, capture_output=True, text=True, timeout=15)
            summary = root / "summary.md"
            return result, summary.read_text() if summary.exists() else "", (root / "test.log").read_text()

    def writer(self, outcome="Completed", count=1):
        xml = TRX.format(outcome=outcome, count=count)
        return "from pathlib import Path; Path('results.trx').write_text(" + repr(xml) + "); "

    def test_runner_retains_log_and_reports_progress_and_timings(self):
        process, summary, log = self.execute(self.writer() +
            "import time; print('engine noise'); print('  Passed Example.Gameplay [1 s]'); time.sleep(0.3)")
        self.assertEqual(process.returncode, 0, process.stdout + process.stderr)
        self.assertIn("1 results", process.stdout)
        self.assertNotIn("engine noise", process.stdout)
        self.assertIn("engine noise", log)
        self.assertEqual(summary, "")
        self.assertIn("Passed: 1, Failed: 0, Skipped: 0", process.stdout)

    def test_runner_rejects_false_green_evidence(self):
        scenarios = (
            ("print('no tests found')", True),  # stale prior success must be removed
            (self.writer(count=0), False),
            (self.writer(outcome="Aborted"), False),  # passed cases do not make an aborted run green
            (self.writer() + "raise SystemExit(7)", False),
        )
        for code, old in scenarios:
            with self.subTest(code=code):
                process, summary, _ = self.execute(code, old_result=old)
                self.assertNotEqual(process.returncode, 0)
                self.assertIn("Result: failed", summary)

    def test_runner_surfaces_failure_without_executing_log_commands(self):
        failure = TRX.format(outcome="Failed", count=1).replace('outcome="Passed"', 'outcome="Failed"')
        failure = failure.replace('failed="0"', 'failed="1"').replace('passed="1"', 'passed="0"')
        failure = failure.replace('duration="00:00:01.250" />',
            'duration="00:00:01.250"><Output><ErrorInfo><Message>Expected &lt;alive&gt;</Message>'
            '</ErrorInfo></Output></UnitTestResult>')
        process, summary, log = self.execute("from pathlib import Path; Path('results.trx').write_text(" +
            repr(failure) + "); print('  Failed Example.Gameplay [1 s]'); print('::error::untrusted'); raise SystemExit(1)")
        self.assertNotEqual(process.returncode, 0)
        self.assertIn("  |   Failed Example.Gameplay", process.stdout)
        self.assertIn("  | ::error::untrusted", process.stdout)
        self.assertIn("::error::untrusted", log)
        self.assertIn("Failed: 1", summary)
        self.assertIn("Expected &lt;alive&gt;", summary)
        self.assertNotIn("Expected <alive>", summary)

    def test_runner_bounds_a_hung_process_even_with_partial_success(self):
        process, summary, _ = self.execute(self.writer() + "import time; time.sleep(30)", timeout=0.5)
        self.assertEqual(process.returncode, 124)
        self.assertIn("wall-clock timeout", process.stdout)
        self.assertIn("Result: failed", summary)

    @unittest.skipUnless(os.name == "posix", "Linux process-group behavior runs in CI")
    def test_timeout_kills_descendants_that_hold_the_log_pipe_open(self):
        child = "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(30)"
        code = ("import subprocess,sys,time; subprocess.Popen([sys.executable, '-c', " + repr(child) +
                "]); time.sleep(30)")
        process, summary, _ = self.execute(code, timeout=0.5)
        self.assertEqual(process.returncode, 124)
        self.assertIn("Result: failed", summary)


if __name__ == "__main__":
    unittest.main()
