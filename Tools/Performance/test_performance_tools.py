import unittest
from copy import deepcopy
from compare_pvs_logs import summarize, compare
from terrain_packing_audit import select, pack, unpack

def window(area="Send_States", calls=10, total=20, seconds=2, status="observed", mode="True"):
    return f"[CMU-PERF] pvs-stage-window area={area} calls={calls} totalMs={total} avgMs=unused status={status} windowSeconds={seconds} pvsAsync={mode} processWide=true\n"

class PvsTests(unittest.TestCase):
    def test_missing_is_unavailable(self):
        self.assertEqual(summarize(["ordinary log"])["status"], "missing-stage-windows")
        row = summarize([window(calls=0, total="unavailable", status="no-observations")])["stages"][0]
        self.assertIsNone(row["avg_ms"])
        self.assertIsNone(row["total_ms"])

    def test_weights_by_calls_and_separates_async_mode(self):
        rows = summarize([window(), window(calls=30, total=120), window(mode="False")])["stages"]
        self.assertEqual(len(rows), 2)
        row = next(r for r in rows if r["pvs_async"] == "True")
        self.assertEqual(row["avg_ms"], 3.5)
        self.assertEqual(row["observed_ms_per_second"], 35)
        self.assertEqual(compare(summarize([window()]), summarize([window(total=10)]))[0]["average_change_percent"], -50)

    def test_invalid_and_reset_counters_do_not_become_costs(self):
        data = summarize([window(total="nan"), window(status="counter-reset", total="unavailable", calls=0)])
        self.assertEqual(data["malformed_windows"], 1)
        self.assertIsNone(data["stages"][0]["avg_ms"])
        self.assertIsNone(compare(summarize([]), summarize([window()]))[0]["average_change_percent"])

def patch():
    records = []
    for x in range(-2, 3):
        for y in range(-2, 3):
            records.append({"uid": str(100 + (x+2)*5 + y+2),
                            "components": [{"type": "Transform", "parent": "1", "pos": f"{x+.5:g},{y+.5:g}", "rot": "1.5rad"}]})
    return {"entities": [{"proto": "CMWallRock", "entities": records}]}

class TerrainTests(unittest.TestCase):
    def test_negative_positions_round_trip_and_boundaries_remain_entities(self):
        selected, count, rejected = select(patch(), "CMWallRock")
        self.assertEqual(count, 25)
        self.assertEqual(len(selected), 9)
        self.assertEqual(rejected["boundary"], 16)
        self.assertEqual(unpack(pack(selected, "CMWallRock")), selected)
        self.assertGreater(len(pack(selected, "CMWallRock")["chunks"]), 1)

    def test_references_custom_components_and_fractional_occupants_exclude_cells(self):
        data = patch()
        center = data["entities"][0]["entities"][12]
        data["entities"].append({"proto": "Other", "entities": [{"uid": "500", "components": [
            {"type": "Transform", "parent": "1", "pos": "1.1,1.2"},
            {"type": "Container", "entity": center["uid"]}]}]})
        data["entities"][0]["entities"][6]["components"].append({"type": "Damageable", "damage": "7"})
        selected, _, rejected = select(data, "CMWallRock")
        self.assertNotIn(("1", 0, 0), selected)
        self.assertNotIn(("1", 1, 1), selected)
        self.assertNotIn(("1", -1, -1), selected)
        self.assertEqual(rejected["referenced"], 1)
        self.assertEqual(rejected["occupied"], 1)
        self.assertEqual(rejected["customized"], 1)

    def test_unanchored_duplicate_and_unknown_transforms_are_not_packed(self):
        for field in ("anchored", "unknown"):
            data = patch()
            data["entities"][0]["entities"][12]["components"][0][field] = "false"
            selected, _, _ = select(data, "CMWallRock")
            self.assertNotIn(("1", 0, 0), selected)
        data = patch()
        duplicate = deepcopy(data["entities"][0]["entities"][12])
        duplicate["uid"] = "700"
        data["entities"][0]["entities"].append(duplicate)
        selected, _, _ = select(data, "CMWallRock")
        self.assertNotIn(("1", 0, 0), selected)

if __name__ == "__main__":
    unittest.main()
