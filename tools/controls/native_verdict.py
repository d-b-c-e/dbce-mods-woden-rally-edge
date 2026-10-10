"""Offline Woden input verdict. No engine, device, force or configuration writes.

Reads the original-profile raw workload, correlated commands and native-car
observations. Requested car-input/wheelInput values are never proof. This checks
only the submitted scalar samples; menus and unsubmitted bindings stay unknown.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def scalar_case(action, expected, start, native, duration=.5):
    keys = {"steer": ("controls.steer", "game.steer"),
            "throttle": ("controls.throttle", "game.throttle"),
            "brake": ("controls.brake", "game.brake"),
            "handbrake": ("game.handbrake",)}
    result = dict(action=action, expected=expected, at=start, result="unknown")
    if action not in keys:
        result["reason"] = "No scalar verdict for this action; inspect separate menu/camera evidence."
        return result
    # Leave two physics ticks for acquisition; require a sustained plateau,
    # not one matching value somewhere in a multi-second window.
    rows = [row for t, row in native if start+.08 <= t <= start+duration-.02]
    result["rows"] = len(rows)
    if len(rows) < 5:
        result["reason"] = "Insufficient independent native-car observations."
        return result
    required = (*keys[action], "game.status", "game.paused", "game.locked",
                "game.respawning", "game.replay")
    if any(any(k in row.get("Missing", []) or k not in row["Channels"] or
                   not math.isfinite(row["Channels"][k]) for k in required) for row in rows):
        result["reason"] = "Missing or non-finite native channel."
        return result
    if len({row["Car"] for row in rows}) != 1 or any(
            row["State"] != "driving" or row["Channels"]["game.status"] != 1 or
            any(row["Channels"][k] != 0 for k in required[-4:]) for row in rows):
        result["reason"] = "Not a stable, unlocked driving window."
        return result
    require(math.isfinite(expected), "Non-finite expected value.")
    # Native Woden exposes handbrake as a button, not a proportional value.
    target = (1 if expected > 0 else 0) if action == "handbrake" else expected
    result["nativeTarget"] = target
    result["channels"] = {k: dict(min=min(r["Channels"][k] for r in rows),
                                  max=max(r["Channels"][k] for r in rows)) for k in keys[action]}
    result["result"] = "observed" if all(abs(r["Channels"][k]-target) <= .001
                                        for r in rows for k in keys[action]) else "mismatch"
    # Release back to physical input is observed separately. No assumed neutral
    # value: an owner wheel can be slightly off centre while at rest.
    after = [r for t, r in native if start+duration+.08 <= t <= start+duration+.5]
    result["postExpiry"] = {k: [min(r["Channels"][k] for r in after),
                               max(r["Channels"][k] for r in after)]
                           for k in keys[action] if after and all(
                               k in r["Channels"] and math.isfinite(r["Channels"][k]) for r in after)}
    return result


def analyze(root):
    identity = load(root / "trace/identity.json")
    workload = load(root / "raw-workload.json")
    applied = load(root / "apply/apply.json")
    require(workload["Schema"] == 1 and workload["ProfileHash"] == applied["ProfileHash"],
            "Original workload/profile identity differs.")
    require(applied["Game"] == "super-woden-rally-edge" and applied["Status"] == "restored", "Wrong Apply receipt.")
    require(identity["physicalOutput"] is False, "No no-force evidence.")
    require(load(root / "runtime-verification.json")["status"] == "passed", "Runtime config verification failed.")
    require((root / "restored.txt").is_file(), "Owner restoration is not complete.")
    result = load(root / "trace/result.json")
    require(result["reason"] == "stopped by command" and result["physicalOutput"] is False,
            "Probe did not stop cleanly without force.")
    submitted = []
    for path in sorted((root / "trace").glob("command-*.json"), key=lambda p: int(p.stem.split('-')[-1])):
        command = load(path)
        if command["operation"] != "raw":
            continue
        seq = command["sequence"]
        require(command == load(root / f"sent-{seq}.json"), "Sent/archive command differs.")
        reply = load(root / f"reply-{seq}.json")
        require(reply["sequence"] == seq and command["nonce"] == reply["nonce"] == identity["request"]["nonce"]
                and reply["ok"] is True and reply["physicalOutput"] is False, "Command reply differs.")
        submitted.append(command["raw"])
    accepted, native = [], []
    lines = (root / "trace/observations.tsv").read_text(encoding="utf-8-sig").splitlines()
    require(lines[0] == "time_s\tframe\tkind\tdata", "Unknown trace format.")
    for line in lines[1:]:
        t, frame, kind, data = line.split('\t', 3)
        t = float(t)
        require(math.isfinite(t), "Non-finite trace time.")
        if kind == "request-accepted":
            accepted.append((t, data.split('; ', 1)[0]))
        elif kind == "native-car":
            native.append((t, json.loads(data)))
        elif kind == "request-refused":
            raise ValueError("A raw command was refused.")
    require([raw for t, raw in accepted] == submitted and len(submitted) == result["commands"],
            "Accepted command sequence differs.")
    cases = []
    for t, raw in accepted:
        matches = {(a["Action"], s["Expected"]) for a in workload["Actions"] for s in a["Samples"]
                   if s["Command"] == raw}
        require(len(matches) == 1, "Raw command has no unique original-profile expectation.")
        action, expected = matches.pop()
        cases.append(scalar_case(action, expected, t, native))
    files = ["trace/observations.tsv", "trace/identity.json", "raw-workload.json", "apply/apply.json",
             "runtime-verification.json", "restored.txt", "trace/result.json", "apply/profile.json"]
    return dict(schema=1, scope="Submitted native scalar samples only; not full profile qualification or owner acceptance",
                sourceHashes={p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in files},
                observed=sum(c["result"] == "observed" for c in cases),
                mismatches=sum(c["result"] == "mismatch" for c in cases),
                unknown=sum(c["result"] == "unknown" for c in cases), nativeRows=len(native), cases=cases)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("result", type=Path)
    args = parser.parse_args()
    report = analyze(args.result)
    print(json.dumps(report, indent=2))
    raise SystemExit(1 if report["mismatches"] or not report["observed"] else 0)
