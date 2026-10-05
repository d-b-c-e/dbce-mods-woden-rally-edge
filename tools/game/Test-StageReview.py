"""Exercise the device-free validator against a retained real recording and isolated corrupt copies."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def hashes(root):
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in root.iterdir() if p.is_file()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    inspector = root / "components/wheel/tools/TelemetryInspector/bin/Release/net10.0/TelemetryInspector.dll"
    args.output.mkdir(parents=True, exist_ok=False)
    original = hashes(args.recording)

    def run(path, expected, reason=""):
        result = subprocess.run(["dotnet", str(inspector), "stage-review", str(path)], capture_output=True, text=True)
        assert result.returncode == expected, result.stdout + result.stderr
        if reason:
            assert reason in result.stderr, result.stderr
        return result

    result = run(args.recording, 0)
    (args.output / "original-review.json").write_text(result.stdout, encoding="utf-8")
    # Reseal only intentionally altered fixture copies, to reach semantic checks
    # behind integrity verification. The actual recording is never rewritten.
    for name, field, replacement, reason in [
        ("alignment", "capture.trajectoryIndex", 99, "row alignment"),
        ("delivery", "ffb.deliveryAttempts", 1, "physically muted"),
        ("model", "analysis.force.preview", .9, "Model preview differs"),
        ("reset", "analysis.force.reset", 99999, "validity/reset differs"),
        ("missing", "analysis.force.preview", None, "Missing/nonfinite"),
    ]:
        fixture = args.output / name
        shutil.copytree(args.recording, fixture)
        path = fixture / "source.jsonl"
        lines = path.read_text(encoding="utf-8-sig").splitlines()
        for i, line in enumerate(lines):
            record = json.loads(line)
            if record["kind"] == "sample":
                if replacement is None:
                    del record["sample"]["channels"][field]
                else:
                    record["sample"]["channels"][field] = replacement
                lines[i] = json.dumps(record, separators=(",", ":"))
                break
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        seal = (fixture / "complete.tsv").read_text().splitlines()
        (fixture / "complete.tsv").write_text("\n".join([
            seal[0], *["\t".join([entry.split("\t")[0],
                str((fixture / entry.split("\t")[0]).stat().st_size),
                hashlib.sha256((fixture / entry.split("\t")[0]).read_bytes()).hexdigest()]) for entry in seal[1:]]
        ]) + "\n", encoding="utf-8")
        result = run(fixture, 2, reason)
        (args.output / (name + ".txt")).write_text(result.stderr, encoding="utf-8")
    fixture = args.output / "unsealed-change"
    shutil.copytree(args.recording, fixture)
    with (fixture / "trajectory.tsv").open("ab") as stream:
        stream.write(b"invalid\n")
    run(fixture, 2, "changed or truncated")
    assert hashes(args.recording) == original, "Original evidence changed"
    print("PASS: original source reprocess, six corruption refusals, exact original preservation")


if __name__ == "__main__":
    main()
