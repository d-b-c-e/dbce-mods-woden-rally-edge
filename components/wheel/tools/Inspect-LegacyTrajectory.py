"""Read-only assessment of a sealed Woden signal case for trajectory reuse.

Does not launch a game, invent setup, rewrite source or issue a playback seal.
Continuous driving ranges remain separate; scene/setup evidence is mandatory
before any later conversion can become a replayable reference.
"""
import argparse
import collections
import hashlib
import json
import math
import pathlib
import statistics

POSE = [f"motion.position.world.{a}" for a in "xyz"]
POSE += [f"motion.orientation.{a}" for a in "xyzw"]
POSE += [f"motion.velocity.world.{a}" for a in "xyz"]
POSE += [f"motion.angularVelocity.local.{a}" for a in "xyz"]
POSE += [f"game.{a}" for a in ("steer", "throttle", "brake", "rpm", "gear")]


def inspect(directory):
    directory = pathlib.Path(directory).resolve()
    case = json.loads((directory / "case.json").read_text(encoding="utf-8-sig"))
    if case.get("schema") != "dbce.wheel.replay-case" or case.get("version") != 1 or case.get("game") != "super-woden-rally-edge":
        raise ValueError("Not a supported Woden source case")
    source = (directory / case["source"]["path"]).resolve()
    if not source.is_relative_to(directory):
        raise ValueError("Source escapes the original case directory")
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != case["source"]["sha256"].lower():
        raise ValueError("Original source hash differs from the case")
    metadata = footer = None
    samples = driving = 0
    markers = collections.Counter()
    marker_examples = {}
    missing = collections.Counter()
    segments, current, steps = [], [], []
    last = None

    def finish():
        if current:
            segments.append({
                "firstSequence": current[0][0], "lastSequence": current[-1][0],
                "samples": len(current), "startPhysicsSeconds": current[0][1],
                "endPhysicsSeconds": current[-1][1], "durationSeconds": current[-1][1]-current[0][1],
                "carId": current[0][2], "maxSpeedMs": max(x[3] for x in current) if all(x[3] is not None for x in current) else None,
                "medianStepSeconds": statistics.median(steps) if steps else None,
                "minimumStepSeconds": min(steps) if steps else None,
                "maximumStepSeconds": max(steps) if steps else None,
            })
        current.clear()
        steps.clear()

    with source.open(encoding="utf-8-sig") as stream:
        for line in stream:
            record = json.loads(line)
            if footer is not None:
                raise ValueError("Records after footer")
            kind = record.get("kind")
            if kind == "metadata":
                if metadata is not None:
                    raise ValueError("Repeated metadata")
                metadata = record["metadata"]
            elif kind == "sample":
                samples += 1
                sample = record["sample"]
                channels = sample["channels"]
                if channels.get("sample.driving") != 1:
                    finish(); last = None
                    continue
                driving += 1
                absent = [name for name in POSE + ["sample.simulationSeconds", "game.carId"] if name not in channels]
                missing.update(absent)
                if absent:
                    finish(); last = None
                    continue
                if any(not math.isfinite(channels[name]) for name in POSE):
                    raise ValueError("Nonfinite original pose channel")
                q = [channels[f"motion.orientation.{a}"] for a in "xyzw"]
                if abs(sum(x*x for x in q)-1) > .01:
                    raise ValueError("Original quaternion is not normalized")
                time = channels["sample.simulationSeconds"]
                if not math.isfinite(time):
                    raise ValueError("Nonfinite physics clock")
                item = (sample["sequence"], time, channels["game.carId"], channels.get("motion.speed"))
                dt = time-last[1] if last else None
                if last and (channels.get("sample.discontinuity") == 1 or item[2] != last[2] or
                             item[0] != last[0]+1 or not .001 <= dt <= .1 or
                             steps and abs(dt-statistics.median(steps[-20:])) > .0001):
                    finish(); last = None
                if last:
                    steps.append(dt)
                current.append(item); last = item
            elif kind == "marker":
                marker = record.get("marker", {})
                name = marker.get("name", marker.get("category", "unknown"))
                markers[name] += 1
                if name not in marker_examples:
                    marker_examples[name] = marker
            elif kind == "footer":
                footer = record
    finish()
    if metadata is None or footer is None:
        raise ValueError("Incomplete original recording")
    if metadata.get("game") != "Super Woden Rally Edge":
        raise ValueError("Source belongs to another game")
    end = footer["footer"]
    counts = end["counts"]
    if not end.get("completed") or counts.get("writtenSamples") != samples or counts.get("acceptedSamples") != samples:
        raise ValueError("Source completion/count mismatch")
    if any(counts.get(name) != 0 for name in ("droppedSamples", "droppedMarkers", "contentionCount", "errorCount", "invalidCount", "limitCount", "queueFullCount")):
        raise ValueError("Source contains dropped, contended, invalid or limited data")
    properties = metadata.get("properties", {})
    return {
        "assessmentOnly": True, "source": str(source), "sourceSha256": digest, "samples": samples,
        "drivingSamples": driving, "missingPoseChannels": dict(missing),
        "gameAssemblySha256": properties.get("gameAssemblySha256"),
        "phase": properties.get("phase"), "originalDisableForces": properties.get("disableForces"),
        "sceneMetadata": {k: v for k, v in properties.items() if "scene" in k.lower() or "track" in k.lower()},
        "markers": dict(markers), "markerExamples": marker_examples,
        "footer": footer, "continuousDrivingSegments": segments,
        "conversionNotes": [
            "Original source is unchanged and matches its case hash.",
            "Angular velocity is local; a converter must rotate it into world space using the recorded quaternion.",
            "Never join separate ranges across resets, non-driving samples or physics-clock gaps.",
            "Scene and launch configuration require separate verified evidence; no scene is guessed.",
            "This inspection does not qualify game hooks or produce a replayable completion seal.",
        ],
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("case_directory")
    parser.add_argument("--output", help="New JSON report; existing reports are refused")
    args = parser.parse_args()
    report = inspect(args.case_directory)
    encoded = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output:
        with pathlib.Path(args.output).open("x", encoding="utf-8") as destination:
            destination.write(encoded)
        print(json.dumps({k: report[k] for k in ("sourceSha256", "samples", "drivingSamples", "missingPoseChannels", "sceneMetadata", "continuousDrivingSegments")}, indent=2))
    else:
        print(encoded, end="")
