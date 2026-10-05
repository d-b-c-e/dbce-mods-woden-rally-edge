import hashlib
import importlib.util
import json
import pathlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("inspection", pathlib.Path(__file__).with_name("Inspect-LegacyTrajectory.py"))
inspection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inspection)


class LegacyTrajectoryTests(unittest.TestCase):
    def fixture(self, change=None):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = pathlib.Path(temporary.name)
        records = [{"kind": "metadata", "metadata": {"game": "Super Woden Rally Edge", "properties": {}}}]
        for index in range(4):
            channels = dict.fromkeys(inspection.POSE, 0)
            channels.update({"motion.orientation.w": 1, "sample.driving": 1,
                             "sample.simulationSeconds": 10 + .02*index, "game.carId": 42})
            records.append({"kind": "sample", "sample": {"sequence": index, "channels": channels}})
        counts = dict.fromkeys(("droppedSamples", "droppedMarkers", "contentionCount", "errorCount", "invalidCount", "limitCount", "queueFullCount"), 0)
        counts.update({"acceptedSamples": 4, "writtenSamples": 4})
        records.append({"kind": "footer", "footer": {"completed": True, "counts": counts}})
        if change:
            change(records)
        source = "".join(json.dumps(item)+"\n" for item in records).encode()
        (root / "source.jsonl").write_bytes(source)
        (root / "case.json").write_text(json.dumps({"schema": "dbce.wheel.replay-case", "version": 1,
            "game": "super-woden-rally-edge", "source": {"path": "source.jsonl", "sha256": hashlib.sha256(source).hexdigest()}}))
        return root

    def test_complete_continuous_source_keeps_unavailable_speed(self):
        result = inspection.inspect(self.fixture())
        self.assertEqual(4, result["continuousDrivingSegments"][0]["samples"])
        self.assertIsNone(result["continuousDrivingSegments"][0]["maxSpeedMs"])
        self.assertEqual({}, result["sceneMetadata"])

    def test_missing_pose_is_reported_not_invented(self):
        result = inspection.inspect(self.fixture(lambda r: r[2]["sample"]["channels"].pop("motion.orientation.x")))
        self.assertEqual(1, result["missingPoseChannels"]["motion.orientation.x"])
        self.assertEqual([1, 2], [s["samples"] for s in result["continuousDrivingSegments"]])

    def test_discontinuity_splits_source(self):
        result = inspection.inspect(self.fixture(lambda r: r[3]["sample"]["channels"].update({"sample.discontinuity": 1})))
        self.assertEqual([2, 2], [s["samples"] for s in result["continuousDrivingSegments"]])

    def test_clock_gap_splits_source(self):
        def change(records):
            for record in records[3:5]:
                record["sample"]["channels"]["sample.simulationSeconds"] += 1
        result = inspection.inspect(self.fixture(change))
        self.assertEqual([2, 2], [s["samples"] for s in result["continuousDrivingSegments"]])

    def test_drops_refused(self):
        root = self.fixture(lambda r: r[-1]["footer"]["counts"].update({"droppedSamples": 1}))
        with self.assertRaisesRegex(ValueError, "dropped"):
            inspection.inspect(root)

    def test_hash_change_refused(self):
        root = self.fixture()
        with (root / "source.jsonl").open("a") as stream:
            stream.write("\n")
        with self.assertRaisesRegex(ValueError, "hash"):
            inspection.inspect(root)

    def test_bad_rotation_refused(self):
        root = self.fixture(lambda r: r[2]["sample"]["channels"].update({"motion.orientation.w": 2}))
        with self.assertRaisesRegex(ValueError, "quaternion"):
            inspection.inspect(root)

    def test_missing_footer_refused(self):
        root = self.fixture(lambda r: r.pop())
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            inspection.inspect(root)


if __name__ == "__main__":
    unittest.main()
