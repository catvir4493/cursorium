"""Protocol correctness tests using synthetic fixtures, never performance evidence."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("summary", Path(__file__).parents[1] / "scripts/summarize-performance.py")
summary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(summary)


class SummaryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.execution = {"schemaVersion": 1, "completed": True, "rounds": 1, "seconds": 5,
                          "warmupSeconds": 1, "started": "2026-01-01T00:00:00Z", "runs": []}

    def tearDown(self):
        self.temporary.cleanup()

    def trial(self, target="test", mode="idle", contaminated=False, passed=True, count=5):
        name = f"{mode}-{target}"
        folder = self.root / name
        folder.mkdir()
        run = {"relativeReport": name, "mode": mode, "target": target, "round": 0,
               "contaminatedByPointerMotion": contaminated, "runtime": "test runtime",
               "applicationSHA256": "A" * 64, "controllerCPUOneCorePercent": 0.1}
        metrics = {"passed": passed, "mode": mode, "runtime": "test runtime", "markers": {},
                   "started": "2026-01-01T00:00:00Z", "samples": [
                       {"phase": "stable", "cpuOneCorePercent": value, "gen0": 0, "gen1": 0, "gen2": 0}
                       for value in range(count)]}
        (folder / "metrics.json").write_text(json.dumps(metrics))
        if mode != "idle":
            (folder / "driver.json").write_text(json.dumps({"passed": True, "events": 100, "observedMoves": 100,
                "observedClicks": 10, "input": "synthetic protocol fixture", "seconds": 6, "cpuOneCorePercent": 1}))
        self.execution["runs"].append(run)
        return run, metrics, folder

    def save(self):
        (self.root / "execution.json").write_text(json.dumps(self.execution))

    def test_interpolated_percentiles(self):
        self.assertEqual(summary.percentile([0, 10, 20], .5), 10)
        self.assertEqual(summary.percentile([0, 10, 20], .95), 19)
        self.assertIsNone(summary.percentile([None], .95))

    def test_missing_paint_remains_null(self):
        self.trial()
        self.save()
        result = summary.summarize(self.root, require_complete=True)
        self.assertIsNone(result["measurements"][0]["observations"]["paintHz"]["median"])
        self.assertEqual(result["measurements"][0]["roundCpuMeans"], [2])

    def test_contaminated_trial_cannot_pass_complete_gate(self):
        self.trial(contaminated=True)
        self.save()
        self.assertEqual(len(summary.summarize(self.root)["excludedRuns"]), 1)
        with self.assertRaises(ValueError):
            summary.summarize(self.root, require_complete=True)

    def test_explicit_cutoff_preserves_exclusion_reason(self):
        self.trial()
        self.save()
        result = summary.summarize(self.root, exclude_before="2026-01-01T00:00:01+00:00")
        self.assertIn("operator", result["excludedRuns"][0]["reason"])

    def test_failed_metrics_rejected(self):
        self.trial(passed=False)
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root)

    def test_missing_samples_rejected(self):
        self.trial(count=2)
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root)

    def test_duplicate_evidence_rejected(self):
        self.trial()
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root, [self.root])

    def test_path_escape_rejected(self):
        run, _, _ = self.trial()
        run["relativeReport"] = "../escape"
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root)

    def test_nan_json_rejected(self):
        path = self.root / "invalid.json"
        path.write_text('{"cpu":NaN}')
        with self.assertRaises(ValueError):
            summary.load(path)

    def test_incomplete_execution_rejected_by_gate(self):
        self.trial()
        self.execution["completed"] = False
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root, require_complete=True)

    def test_overflowing_json_rejected(self):
        path = self.root / "overflow.json"
        path.write_text('{"cpu":1e999}')
        with self.assertRaises(ValueError):
            summary.load(path)

    def test_cross_product_detects_missing_version_mode(self):
        self.trial(target="old", mode="idle")
        self.trial(target="new", mode="comet")
        self.save()
        result = summary.summarize(self.root)
        self.assertEqual(len(result["insufficientGroups"]), 2)
        with self.assertRaises(ValueError):
            summary.summarize(self.root, require_complete=True)

    def test_cancelled_driver_rejected(self):
        _, _, folder = self.trial()
        (folder / "driver.json").write_text('{"passed":false}')
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root)

    def test_attempted_events_do_not_prove_delivery(self):
        _, _, folder = self.trial(mode="comet")
        (folder / "driver.json").write_text('{"passed":true,"events":1000,"observedMoves":0}')
        self.save()
        result = summary.summarize(self.root)
        self.assertEqual(len(result["excludedRuns"]), 1)
        self.assertIn("delivery", result["excludedRuns"][0]["reason"])

    def test_missing_active_driver_rejected(self):
        _, _, folder = self.trial(mode="comet")
        (folder / "driver.json").unlink()
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root)

    def test_unavailable_pointer_cannot_pass_complete_gate(self):
        _, metrics, folder = self.trial()
        metrics["samples"][2]["pointerAvailable"] = False
        (folder / "metrics.json").write_text(json.dumps(metrics))
        self.save()
        with self.assertRaises(ValueError):
            summary.summarize(self.root, require_complete=True)

    def test_summary_includes_supplement_time(self):
        self.trial()
        self.save()
        extra = self.root / "supplement"
        extra.mkdir()
        execution = dict(self.execution, runs=[], started="2026-01-01T01:00:00Z", updated="2026-01-01T02:00:00Z")
        (extra / "execution.json").write_text(json.dumps(execution))
        result = summary.summarize(self.root, [extra])
        self.assertEqual(result["ended"], execution["updated"])


if __name__ == "__main__":
    unittest.main()
