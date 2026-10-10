import copy
import unittest
from native_verdict import scalar_case


class NativeVerdictTests(unittest.TestCase):
    def setUp(self):
        self.rows = [(10+.1+i*.02, dict(Car=11, State="driving", Missing=[], Channels={
            "controls.throttle": .5, "game.throttle": .5, "game.status": 1,
            "game.paused": 0, "game.locked": 0, "game.respawning": 0, "game.replay": 0,
        })) for i in range(15)]

    def result(self, rows=None):
        return scalar_case("throttle", .5, 10, self.rows if rows is None else rows)["result"]

    def test_native_plateau(self):
        self.assertEqual("observed", self.result())

    def test_car_overrides_requested_half_throttle(self):
        # Controls saw .5 but MainCar discarded it: the old lease observer
        # would report success. The independent verdict must fail.
        for t, row in self.rows:
            row["Channels"]["game.throttle"] = 0
            row["Channels"]["wheelInput.throttle"] = .5
        self.assertEqual("mismatch", self.result())

    def test_only_one_good_value_is_not_a_plateau(self):
        self.rows[6][1]["Channels"]["controls.throttle"] = 0
        self.assertEqual("mismatch", self.result())

    def test_absence_not_zero_or_success(self):
        self.assertEqual("unknown", self.result([]))
        del self.rows[6][1]["Channels"]["game.throttle"]
        self.assertEqual("unknown", self.result())

    def test_nonfinite_not_success(self):
        self.rows[6][1]["Channels"]["game.throttle"] = float("nan")
        self.assertEqual("unknown", self.result())

    def test_explicit_missing_wins_over_stale_value(self):
        self.rows[6][1]["Missing"].append("game.throttle")
        self.assertEqual("unknown", self.result())

    def test_inactive_or_locked_not_success(self):
        for channel in ("game.status", "game.paused", "game.locked", "game.respawning", "game.replay"):
            rows = copy.deepcopy(self.rows)
            rows[6][1]["Channels"][channel] = 0 if channel == "game.status" else 1
            self.assertEqual("unknown", self.result(rows), channel)

    def test_other_car_or_outside_window(self):
        self.rows[6][1]["Car"] = 12
        self.assertEqual("unknown", self.result())
        self.assertEqual("unknown", self.result([(t+20, r) for t, r in self.rows]))

    def test_unobserved_menu_not_passed(self):
        self.assertEqual("unknown", scalar_case("confirm", 1, 10, self.rows)["result"])

    def test_digital_handbrake_is_labelled(self):
        for t, row in self.rows:
            row["Channels"]["game.handbrake"] = 1
        r = scalar_case("handbrake", .5, 10, self.rows)
        self.assertEqual("observed", r["result"])
        self.assertEqual(1, r["nativeTarget"])


if __name__ == "__main__":
    unittest.main()
