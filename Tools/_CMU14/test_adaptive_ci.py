"""CMU14: history must improve ordering without weakening the required evidence."""

import json
import contextlib
import io
import os
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from adaptive_ci import (artifact_fingerprint, balance, baseline_changes, collect, dependency_priority,
                         execute_group, fingerprint, history_inputs, priority_fixtures, reusable, save_json)


class AdaptiveCiTests(unittest.TestCase):
    def test_baseline_includes_intermediate_failed_revision_and_deletions(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def git(*args):
                return subprocess.check_output(["git", "-c", "user.name=CI", "-c", "user.email=ci@example.invalid",
                                                *args], cwd=root, stderr=subprocess.DEVNULL).decode().strip()
            git("init")
            (root / "old.cs").write_text("green")
            git("add", "."); git("commit", "-m", "green")
            baseline = git("rev-parse", "HEAD")
            (root / "old.cs").unlink()
            (root / "new.cs").write_text("failed revision")
            git("add", "."); git("commit", "-m", "failed revision")
            (root / "README.md").write_text("latest commit only changes documentation")
            git("add", "."); git("commit", "-m", "latest")
            self.assertEqual(set(baseline_changes(baseline, root)), {"old.cs", "new.cs", "README.md"})

    def test_dependency_closure_follows_types_events_and_prototype_parents_through_cycles(self):
        files = {
            "Content.Shared/Event.cs": "class WoundEvent {}",
            "Content.Shared/Helper.cs": "class MedicalHelper { WoundEvent ev; MedicalSystem owner; }",
            "Content.Shared/System.cs": "class MedicalSystem { MedicalHelper helper; }",
            "Content.IntegrationTests/MedicalTest.cs": "namespace Tests; class MedicalTest { MedicalSystem system; }",
            "Resources/Prototypes/base.yml": "- type: entity\n  id: ParentItem\n",
            "Resources/Prototypes/child.yml": "- type: entity\n  id: ChildItem\n  parent: ParentItem\n",
            "Content.Tests/PrototypeTest.cs": 'namespace Tests; class PrototypeTest { string id = "ChildItem"; }',
            "Content.Tests/UnrelatedTest.cs": "namespace Tests; class UnrelatedTest {}",
        }
        self.assertEqual(dependency_priority(files, ["Content.Shared/Event.cs"]), ["Tests.MedicalTest"])
        self.assertEqual(dependency_priority(files, ["Resources/Prototypes/base.yml"]), ["Tests.PrototypeTest"])

    def test_balancing_keeps_every_group_once_and_prioritizes_previous_failures(self):
        shards = [dict(name=f"g{i}", seconds=seconds, rank=0 if i == 3 else 2)
                  for i, seconds in enumerate((100, 90, 80, 5, 20, 15))]
        lanes = balance(shards, 2)
        assigned = [name for lane in lanes for name in lane["shards"]]
        self.assertCountEqual(assigned, [s["name"] for s in shards])
        self.assertEqual(lanes[0]["shards"][0], "g3")
        self.assertLessEqual(max(lane["seconds"] for lane in lanes), 180)
        self.assertEqual(sum(lane["seconds"] for lane in lanes), 310)

    def test_early_pass_requires_failures_or_measured_small_affected_fixtures(self):
        self.assertEqual(priority_fixtures({}, ["Tests.UnknownCost"]), [])
        record = dict(seconds=100, cases=[dict(fixture="Tests.Failed", outcome="Failed", seconds=60),
                      dict(fixture="Tests.Small", outcome="Passed", seconds=10),
                      dict(fixture="Tests.Expensive", outcome="Passed", seconds=70)])
        self.assertEqual(priority_fixtures(record, ["Tests.Small", "Tests.Expensive"]), ["Tests.Failed", "Tests.Small"])

    def test_pr_history_cannot_authorize_reuse_and_mismatched_metadata_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            state = Path(directory)
            meta = dict(id=42, head_sha="a" * 40, event="pull_request", conclusion="success",
                        head_branch="master", head_repository="org/repo")
            data = dict(schema=1, run_id="42", head_sha="a" * 40, source_sha="a" * 40, records={}, complete=True)
            save_json(state / "imports/trusted/history.json", data)
            provenance = dict(repository="org/repo", default_branch="master", trusted=meta)
            save_json(state / "provenance.json", provenance)
            self.assertNotIn("trusted", history_inputs(state))
            meta["event"] = "push"
            save_json(state / "provenance.json", provenance)
            self.assertIn("trusted", history_inputs(state))
            data["source_sha"] = "b" * 40
            save_json(state / "imports/trusted/history.json", data)
            self.assertNotIn("trusted", history_inputs(state))
            (state / "imports/trusted/history.json").write_text("broken download")
            self.assertEqual(history_inputs(state), {})

    def test_reuse_invalidates_on_source_toolchain_and_execution_settings(self):
        plan, settings, environment = dict(source_tree="a" * 40), dict(workers=2, filter="all"), dict(sdk="10", image="v1")
        key = fingerprint(plan, settings, environment)
        record = dict(valid=True, status=0, counters=dict(executed=2), fingerprint=key)
        self.assertTrue(reusable(record, fingerprint(plan, settings, environment)))
        variants = [(dict(source_tree="b" * 40), settings, environment),
                    (plan, dict(workers=1, filter="all"), environment),
                    (plan, dict(workers=2, filter="different"), environment),
                    (plan, settings, dict(sdk="11", image="v1")),
                    (plan, settings, dict(sdk="10", image="v2"))]
        for inputs in variants:
            self.assertFalse(reusable(record, fingerprint(*inputs)))
        self.assertFalse(reusable(dict(record, status=1), key))
        self.assertFalse(reusable(dict(record, counters=dict(executed=0)), key))

    def test_changed_restored_binary_invalidates_reuse_even_when_source_is_identical(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Dependency.dll").write_bytes(b"original dependency")
            previous = artifact_fingerprint([root])
            (root / "Dependency.dll").write_bytes(b"different dependency")
            self.assertNotEqual(artifact_fingerprint([root]), previous)

    def run_sequence(self, responses, priority=None, previous=None):
        calls = []
        with tempfile.TemporaryDirectory() as directory:
            def runner(args):
                calls.append(args)
                response = responses[len(calls) - 1]
                save_json(Path(args.log).with_suffix(".json"), response)
                return response["status"]
            settings = dict(name="unit", assembly="unit.dll", filter="FullyQualifiedName~Tests.", timeout=720)
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(Path(directory) / "summary.md")}), \
                    contextlib.redirect_stdout(io.StringIO()):
                status = execute_group(settings, priority or ["Tests.PreviousFailure"], previous or {}, "exact-key",
                                       directory, runner)
            observed = json.loads((Path(directory) / "unit.json").read_text())
            return status, calls, observed

    def result(self, status=0, executed=1):
        return dict(status=status, valid=status == 0 and executed > 0, counters=dict(executed=executed, passed=executed),
                    seconds=10, cases=[])

    def test_successful_priority_is_excluded_from_remainder_and_shares_one_time_budget(self):
        status, calls, observed = self.run_sequence([self.result(), self.result()])
        self.assertEqual(status, 0)
        self.assertEqual(len(calls), 2)
        self.assertIn("FullyQualifiedName~Tests.PreviousFailure.", calls[0].command[calls[0].command.index("--filter") + 1])
        self.assertIn("FullyQualifiedName!~Tests.PreviousFailure.", calls[1].command[calls[1].command.index("--filter") + 1])
        self.assertEqual(calls[1].timeout, 710)
        self.assertEqual(observed["counters"]["executed"], 2)

    def test_failed_priority_stays_failed_without_retrying_until_green(self):
        status, calls, observed = self.run_sequence([self.result(status=1)])
        self.assertEqual(status, 1)
        self.assertEqual(len(calls), 1)
        self.assertFalse(observed["valid"])

    def test_ignored_priority_filter_cannot_create_duplicate_success_evidence(self):
        first = dict(self.result(), cases=[dict(id="same-test", outcome="Passed")])
        status, calls, observed = self.run_sequence([first, first])
        self.assertNotEqual(status, 0)
        self.assertFalse(observed["valid"])

    def test_stale_priority_falls_back_to_full_selection_and_both_empty_is_failure(self):
        status, calls, observed = self.run_sequence([self.result(executed=0), self.result()])
        self.assertEqual(status, 0)
        self.assertEqual(calls[1].command[calls[1].command.index("--filter") + 1], "FullyQualifiedName~Tests.")
        self.assertFalse(calls[1].allow_empty)
        status, _, observed = self.run_sequence([self.result(executed=0), self.result(executed=0)])
        self.assertNotEqual(status, 0)
        self.assertFalse(observed["valid"])

    def test_exact_trusted_success_reuses_evidence_without_starting_a_process(self):
        previous = dict(self.result(), fingerprint="exact-key")
        status, calls, observed = self.run_sequence([], previous=previous)
        self.assertEqual(status, 0)
        self.assertEqual(calls, [])
        self.assertTrue(observed["reused"])
        self.assertEqual(observed["counters"]["executed"], 1)

    def test_aggregate_rejects_missing_groups_and_saves_failures_for_next_run(self):
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()):
            state = Path(directory)
            self.assertEqual(collect(SimpleNamespace(state=directory)), 1)
            save_json(state / "plan.json", dict(schema=1, selected=dict(build=True, unit=True, gameplay=True),
                                                shards=[dict(name="gameplay")]))
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(state / "summary.md")}):
                save_json(state / "observations/unit.json", self.result())
                self.assertEqual(collect(SimpleNamespace(state=directory)), 1)
                self.assertIn("gameplay | Missing evidence", (state / "summary.md").read_text(encoding="utf-8"))
                save_json(state / "observations/gameplay.json", self.result(status=1))
                self.assertEqual(collect(SimpleNamespace(state=directory)), 1)
                history = json.loads((state / "history/history.json").read_text())
                self.assertEqual(history["records"]["gameplay"]["status"], 1)
                save_json(state / "observations/gameplay.json", self.result())
                self.assertEqual(collect(SimpleNamespace(state=directory)), 0)


if __name__ == "__main__":
    unittest.main()
