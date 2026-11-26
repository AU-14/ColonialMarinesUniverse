"""CMU14: history must improve ordering without weakening the required evidence."""

import json
import contextlib
import io
import os
from pathlib import Path
import re
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
from ci_selection import may_select, select_assets, select_shards

from adaptive_ci import (LANE_BUDGET, MAX_LANES, POLICY, artifact_fingerprint, balance, baseline_changes, collect, dependency_priority, plan_run,
                         execute_group, fingerprint, group_seconds, history_inputs, priority_fixtures, reusable, save_json,
                         split_heavy, unlisted_namespaces)


class AdaptiveCiTests(unittest.TestCase):
    def test_asset_selection_skips_code_and_broadens_for_validator_changes_or_missing_diff(self):
        self.assertEqual(select_assets(["Content.Shared/Example.cs"]),
                         dict(rga=False, maps=False, rsi=False, any=False))
        self.assertEqual(select_assets(["Content.CMU/Resources/Maps/CMU14/example.yml"]),
                         dict(rga=False, maps=True, rsi=False, any=True))
        for paths in (None, [".github/Schemas/mapfile_requirements.txt"], ["RobustToolbox"]):
            self.assertTrue(all(select_assets(paths).values()))

    def test_plan_schedules_only_selected_groups_and_unit_fixture_changes_skip_gameplay(self):
        feature = "Content.CMU/Shared/Yautja/WeaponSystem.cs"
        unit = "Content.Tests/Shared/WeaponTest.cs"
        files = {feature: "class WeaponSystem {}",
                 unit: "namespace Content.Tests.Shared; class WeaponTest { [Test] void Example() {} }"}
        for event in ("pull_request", "push", "merge_group"):
            for path, expected in ((feature, ["yautja"]), (unit, [])):
                with self.subTest(event=event, path=path), tempfile.TemporaryDirectory() as directory, \
                        patch.dict(os.environ, {"CMU_CI_STATE": directory}), \
                        patch("adaptive_ci.history_inputs", return_value={}), \
                        patch("adaptive_ci.git", return_value="a" * 40), \
                        patch("adaptive_ci.source_graph_inputs", return_value=(files, False)):
                    result = plan_run(dict(build=True, unit=True, gameplay=True, reason=""), [path], event,
                                      [dict(name="yautja", filter="FullyQualifiedName~CMU14.Yautja."),
                                       dict(name="medical", filter="FullyQualifiedName~CMU14.Medical.")])
                    plan = json.loads((Path(directory) / "plan.json").read_text())
                    self.assertEqual([s["name"] for s in plan["shards"]], expected)
                    self.assertEqual([g for lane in plan["lanes"] for g in lane["shards"]], expected)
                    self.assertEqual(result["gameplay"], bool(expected))
                    self.assertEqual(plan["selected"]["gameplay"], bool(expected))
                    self.assertEqual(result["unit"], path == unit)
                    self.assertEqual(len(result["matrix"]["lane"]), len(expected))

    def test_feature_selection_honors_exclusions_and_includes_shared_consumers(self):
        shards = [dict(name="yautja", filter="FullyQualifiedName~CMU14.Yautja."),
                  dict(name="medical", filter="FullyQualifiedName~CMU14.Medical."),
                  dict(name="other", filter="FullyQualifiedName~CMU14. & FullyQualifiedName!~CMU14.Yautja. & FullyQualifiedName!~CMU14.Medical.")]
        path = "Content.CMU/Shared/Yautja/SharedItemSystem.cs"
        files = {path: "class SharedItemSystem {}"}
        self.assertEqual(select_shards([path], shards, files, [], True), {"yautja"})
        self.assertEqual(select_shards([path], shards, files,
                         ["Content.IntegrationTests.CMU14.Medical.TreatmentTest"], True), {"yautja", "medical"})
        for paths, source, complete in (([path], files, False), ([path], {}, True),
                                      (["Content.Shared/EntitySystem.cs"], {}, True),
                                      (["Resources/Prototypes/items.yml"], {}, True)):
            self.assertEqual(select_shards(paths, shards, source, [], complete), {"yautja", "medical", "other"})

    def test_affected_nightly_fixtures_run_without_policy_exclusion_or_unrelated_sweeps(self):
        from ci import select_tests
        shared = "Content.Shared/Storage/SharedRules.cs"
        unrelated = "Content.Shared/Storage/OtherRules.cs"
        unit = "Content.Tests/Shared/IndependentTest.cs"
        fixtures = {
            "Content.IntegrationTests/Tests/EntityTest.cs": "Content.IntegrationTests.Tests.EntityTest",
            "Content.IntegrationTests/Tests/Localization/LocalizedDatasetPrototypeTest.cs":
                "Content.IntegrationTests.Tests.Localization.LocalizedDatasetPrototypeTest",
            "Content.IntegrationTests/Tests/GameRules/AntagGhostRoleTest.cs":
                "Content.IntegrationTests.Tests.GameRules.AntagGhostRoleTest",
        }
        entity_fixture = fixtures["Content.IntegrationTests/Tests/EntityTest.cs"]
        sweeps = {
            "nightly_affected_entity_maps": "SpawnAndDeleteAllEntitiesOnDifferentMaps",
            "nightly_affected_entity_dirty": "SpawnAndDirtyAllEntities",
            "nightly_affected_entity_count": "SpawnAndDeleteEntityCountTest",
        }
        files = {shared: "namespace Shared; class SharedRules {}",
                 unrelated: "namespace Shared; class OtherRules {}",
                 unit: "namespace Content.Tests.Shared; class IndependentTest { [Test] void Example() {} }"}
        for path, fixture in fixtures.items():
            namespace, name = fixture.rsplit(".", 1)
            files[path] = (f"using Shared; namespace {namespace}; class {name} "
                           "{ [Test] void Example() { SharedRules rules; } }")
        scenarios = [(path, False, [fixture], {"nightly_affected"}) for path, fixture in fixtures.items()]
        scenarios += [(shared, False, sorted(fixtures.values()), {"storage", "nightly_affected"}),
                      (unrelated, False, [], {"storage"}), (unit, False, [], set()),
                      ("README.md", False, [], set()),
                      (next(iter(fixtures)), True, [next(iter(fixtures.values()))],
                       {"storage", "round", "nightly_affected"})]
        for path, partial, expected, groups in scenarios:
            groups = groups | (sweeps.keys() if entity_fixture in expected else set())
            with self.subTest(path=path, partial=partial), tempfile.TemporaryDirectory() as directory, \
                    patch.dict(os.environ, {"CMU_CI_STATE": directory}), \
                    patch("adaptive_ci.history_inputs", return_value={}), \
                    patch("adaptive_ci.git", return_value="a" * 40), \
                    patch("adaptive_ci.source_graph_inputs", return_value=(files, partial)):
                result = plan_run(select_tests("pull_request", [path]), [path], "pull_request",
                                  [dict(name="storage", filter="FullyQualifiedName~Content.IntegrationTests.Tests.Storage."),
                                   dict(name="round", filter="FullyQualifiedName~Content.IntegrationTests.Tests.GameRules.")])
                plan = json.loads((Path(directory) / "plan.json").read_text())
                self.assertEqual({s["name"] for s in plan["shards"]}, groups)
                self.assertEqual({g for lane in plan["lanes"] for g in lane["shards"]}, groups)
                self.assertEqual(result["gameplay"], bool(groups))
                if expected:
                    nightly = next(s for s in plan["shards"] if s["name"] == "nightly_affected")
                    selection = " | ".join("FullyQualifiedName~" + fixture + "." for fixture in expected)
                    if entity_fixture in expected:
                        selection = "(" + selection + ")" + "".join(
                            " & FullyQualifiedName!=" + entity_fixture + "." + method for method in sweeps.values())
                        for name, method in sweeps.items():
                            sweep = next(s for s in plan["shards"] if s["name"] == name)
                            self.assertEqual(sweep["filter"],
                                             f"(FullyQualifiedName~{entity_fixture}. & Name={method})")
                    self.assertEqual(nightly["filter"], "(" + selection + ")")
                    for shard in plan["shards"]:
                        if shard["name"].startswith("nightly_affected"):
                            self.assertEqual((shard["workers"], shard["hang"]), (1, "20m"))

    def test_integration_fixture_selection_retains_name_split_shards_and_shared_helpers_run_all(self):
        shards = [dict(name="a", filter="FullyQualifiedName~Tests.FeatureTest. & Name~One"),
                  dict(name="b", filter="FullyQualifiedName~Tests.FeatureTest. & Name!~One"),
                  dict(name="other", filter="FullyQualifiedName~Tests.OtherTest.")]
        path = "Content.IntegrationTests/Tests/FeatureTest.cs"
        files = {path: ('// namespace Wrong; class OtherTest {}\n'
                        'namespace Tests; class FeatureTest { [Test] public void One() { '
                        'var prose = "class OtherTest {}"; } /* class OtherTest {} */ }')}
        self.assertEqual(select_shards([path], shards, files, [], True), {"a", "b"})
        self.assertEqual(select_shards([path], shards, files, [], False), {"a", "b", "other"})
        self.assertEqual(select_shards(["Content.IntegrationTests/PoolManager.cs"], shards, {}, [], True),
                         {"a", "b", "other"})

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
            "Content.Shared/Helper.cs": "using Helpers; class MedicalHelper { WoundEvent ev; MedicalSystem owner; void Run() { ev.IsUsable(); } }",
            "Content.Shared/Extensions.cs": "namespace Helpers; static class Rules { public static bool IsUsable<T>(this T value) => true; }",
            "Content.Shared/System.cs": "class MedicalSystem { MedicalHelper helper; }",
            "Content.IntegrationTests/MedicalTest.cs": "namespace Tests; class MedicalTest { MedicalSystem system; }",
            "Resources/Prototypes/base.yml": "- type: entity\n  id: ParentItem\n",
            "Resources/Prototypes/child.yml": "- type: entity\n  id: ChildItem\n  parent: ParentItem\n",
            "Content.Tests/PrototypeTest.cs": 'namespace Tests; class PrototypeTest { string id = "ChildItem"; }',
            "Content.Tests/UnrelatedTest.cs": "namespace Tests; class UnrelatedTest { void Run() { value.IsUsable(); } }",
        }
        self.assertEqual(dependency_priority(files, ["Content.Shared/Event.cs"]), ["Tests.MedicalTest"])
        self.assertEqual(dependency_priority(files, ["Content.Shared/Extensions.cs"]), ["Tests.MedicalTest"])
        self.assertEqual(dependency_priority(files, ["Resources/Prototypes/base.yml"]), ["Tests.PrototypeTest"])

    def test_system_change_reaches_tests_using_spawned_components_without_naming_the_system(self):
        path = "Content.CMU/Shared/Fob/CrateSystem.cs"
        files = {path: "class CrateSystem { SharedRules rules; SubscribeLocalEvent<CrateComponent, OpenEvent>(OnOpen); }",
                 "Content.Shared/Storage/SharedRules.cs": "class SharedRules {}",
                 "Content.CMU/Shared/Fob/CrateComponent.cs": "class CrateComponent {}",
                 "Resources/Prototypes/crate.yml": "- type: entity\n  id: MyCrate\n  components:\n  - type: Crate\n",
                 "Content.IntegrationTests/CrateTest.cs": 'namespace Tests; class CrateTest { Spawn("MyCrate"); }',
                 "Content.IntegrationTests/UnrelatedTest.cs": "namespace Tests; class UnrelatedTest {}"}
        self.assertEqual(dependency_priority(files, [path]), ["Tests.CrateTest"])
        self.assertEqual(dependency_priority(files, ["Content.Shared/Storage/SharedRules.cs"]), ["Tests.CrateTest"])

    def test_dependency_graph_does_not_confuse_comments_nested_ids_or_other_namespaces_with_types(self):
        path = "Content.CMU/Shared/Fob/CrateSystem.cs"
        files = {
            path: 'namespace Feature; class CrateSystem { SubscribeLocalEvent<CrateComponent, OpenEvent>(OnOpen); }',
            "Content.CMU/Shared/Fob/CrateComponent.cs": 'namespace Feature; class CrateComponent {}',
            "Resources/Prototypes/crates.yml": '- type: entity\n  id: Crate\n  components:\n  - type: Crate\n    items:\n    - id: Welder\n- type: entity\n  id: OtherCrate\n',
            "Content.Server.Database/Model.cs": 'namespace Database; class Server { string role = "Crate"; } // record the incident',
            "Content.IntegrationTests/CrateTest.cs": 'namespace Tests; class CrateTest { [Test] void Example() { Spawn("Crate"); } }',
            "Content.IntegrationTests/OtherTest.cs": 'namespace Tests; class OtherTest { [Test] void Example() { Server.Run(); Spawn("Welder"); Spawn("OtherCrate"); } }',
        }
        self.assertEqual(dependency_priority(files, [path]), ["Tests.CrateTest"])

    def test_balancing_keeps_every_group_once_and_prioritizes_previous_failures(self):
        shards = [dict(name=f"g{i}", seconds=seconds, rank=0 if i == 3 else 2)
                  for i, seconds in enumerate((100, 90, 80, 5, 20, 15))]
        lanes = balance(shards, 2)
        assigned = [name for lane in lanes for name in lane["shards"]]
        self.assertCountEqual(assigned, [s["name"] for s in shards])
        self.assertEqual(lanes[0]["shards"][0], "g3")
        self.assertLessEqual(max(lane["seconds"] for lane in lanes), 180)
        self.assertEqual(sum(lane["seconds"] for lane in lanes), 310)

    def test_balancing_widens_by_estimated_time_and_caps_the_lane_count(self):
        heavy = [dict(name=f"g{i}", seconds=1200, rank=2) for i in range(30)]
        lanes = balance(heavy)
        self.assertEqual(len(lanes), MAX_LANES)
        self.assertEqual(max(lane["seconds"] for lane in lanes), 3600)
        self.assertCountEqual([g for lane in lanes for g in lane["shards"]], [s["name"] for s in heavy])
        self.assertEqual(len(balance(heavy[:8])), 8)
        self.assertEqual(len(balance(heavy[:3])), 3)
        self.assertEqual(len(balance([dict(name=f"g{i}", seconds=100, rank=2) for i in range(8)])), 6)

    def test_long_groups_split_by_recorded_time_and_keep_unrecorded_tests(self):
        cases = [dict(fixture=f"Feature.{name}", seconds=seconds) for name, seconds in zip("ABCD", (900, 300, 300, 900))]
        heavy = dict(name="heavy", filter="FullyQualifiedName~Feature.", workers=1, hang="20m", rank=1, seconds=1800,
                     priority=["Feature.A", "Feature.Unseen"])
        small = dict(name="small", filter="FullyQualifiedName~Feature.", workers=2, hang="5m", rank=2, seconds=300, priority=[])
        pieces = split_heavy([heavy, small], {"heavy": dict(seconds=2400, cases=cases)})
        self.assertEqual([p["name"] for p in pieces], ["heavy_1", "heavy", "small"])
        self.assertIs(pieces[2], small)
        self.assertEqual([[may_select(p["filter"], f"Feature.{name}.") for name in "ABCD"] for p in pieces[:2]],
                         [[True, True, False, False], [False, False, True, True]])
        self.assertEqual([p["name"] for p in pieces[:2] if may_select(p["filter"], "Feature.New.")], ["heavy"])
        self.assertEqual([p["priority"] for p in pieces[:2]], [["Feature.A"], ["Feature.Unseen"]])
        self.assertEqual([p["seconds"] for p in pieces[:2]], [1200, 1200])
        self.assertEqual((pieces[0]["workers"], pieces[0]["hang"], pieces[0]["rank"]), (1, "20m", 1))
        history = {p["name"]: dict(seconds=p["seconds"], cases=[c for c in cases if may_select(p["filter"], c["fixture"] + ".")])
                   for p in pieces[:2]}
        self.assertEqual(group_seconds(dict(history, heavier=dict(seconds=99), heavy_1x=dict(seconds=1)), "heavy"), 2400)
        self.assertEqual([(p["name"], p["filter"]) for p in split_heavy([dict(heavy, seconds=600)], history)],
                         [(p["name"], p["filter"]) for p in pieces[:2]])
        self.assertEqual(split_heavy([heavy], {"heavy": dict(seconds=2400, cases=[])}), [heavy])
        with self.assertRaises(ValueError):
            plan_run(dict(build=True, unit=True, gameplay=True, reason=""), None, "push",
                     [dict(name="heavy_1", filter="FullyQualifiedName~Feature.")])

    def test_new_test_directories_run_as_unlisted_until_a_named_group_claims_them(self):
        shards = [dict(name="storage", filter="FullyQualifiedName~Content.IntegrationTests.Tests.Storage."),
                  dict(name="root", filter=""), dict(name="tests_unlisted", filter=""),
                  dict(name="nightly_affected", filter="(FullyQualifiedName~Tests.EntityTest. & Name=Dirty)")]
        source = "namespace Content.IntegrationTests.Tests.Fresh; class B { [Test] void Example() {} }"
        path = "Content.IntegrationTests/Tests/Fresh/B.cs"
        with tempfile.TemporaryDirectory() as directory:
            tree = Path(directory) / "Content.IntegrationTests"
            for relative, text in (("Tests/Storage/A.cs", source.replace("Fresh", "Storage")),
                                   ("Tests/Fresh/B.cs", source),
                                   ("Tests/Localization/L.cs", source.replace("Fresh", "Localization")),
                                   ("Tests/Helpers/C.cs", "class C {} // [Test] void NotATest() {}"),
                                   ("Fixtures/GameTest.cs", "[TestFixture] abstract class GameTest {}"),
                                   ("Pool/D.cs", "class D {}")):
                (tree / relative).parent.mkdir(parents=True, exist_ok=True)
                (tree / relative).write_text(text)
            self.assertEqual(unlisted_namespaces(shards, tree), ["Content.IntegrationTests.Tests.Fresh"])
            with contextlib.chdir(directory), patch.dict(os.environ, {"CMU_CI_STATE": directory}), \
                    patch("adaptive_ci.history_inputs", return_value={}), \
                    patch("adaptive_ci.git", return_value="a" * 40), \
                    patch("adaptive_ci.source_graph_inputs", return_value=({path: source}, True)):
                plan_run(dict(build=True, unit=True, gameplay=True, reason=""), [path], "pull_request", shards[:3])
                plan = json.loads((Path(directory) / "plan.json").read_text())
            unlisted = next(s for s in plan["shards"] if s["name"] == "tests_unlisted")
            self.assertEqual(unlisted["filter"], "(FullyQualifiedName~Content.IntegrationTests.Tests.Fresh.)" + POLICY)

    def test_workflow_runs_every_planned_lane_and_outlasts_its_budget(self):
        lanes = Path(".github/workflows/ci.yml").read_text(encoding="utf-8").split("  integration-tests:", 1)[1]
        lanes = lanes.split("  required-checks:", 1)[0]
        self.assertGreaterEqual(int(re.search(r"max-parallel:\s*(\d+)", lanes)[1]), MAX_LANES)
        self.assertGreater(int(re.search(r"timeout-minutes:\s*(\d+)", lanes)[1]) * 60, LANE_BUDGET)

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
                (state / "summary.md").unlink()
                self.assertEqual(collect(SimpleNamespace(state=directory)), 0)
                self.assertFalse((state / "summary.md").exists())


if __name__ == "__main__":
    unittest.main()
