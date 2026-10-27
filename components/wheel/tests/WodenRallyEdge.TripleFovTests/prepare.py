"""Copy the three actual handler bodies unchanged into a device-free fixture."""
import argparse
import hashlib
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--source', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
raw = a.source.read_bytes()
source = raw.decode('utf-8-sig')
methods = []
for signature in ('internal static void AfterCarCamera(Car_Cam carCam)',
                  'private static void Apply(Camera source, float? ownedMountedFov)',
                  'internal static void Stop(string why)'):
    assert source.count(signature) == 1, signature
    start = source.index(signature)
    opening = source.index('{', start)
    # These specific bodies contain balanced braces, including their interpolation literals.
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    methods.append(source[start:end])
assert not a.output.exists(), 'Preserve previous fixture input'
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text('internal static partial class TripleView {\n' + '\n'.join(methods) + '\n}\n', encoding='utf-8')
print(json.dumps({'source': str(a.source), 'sourceSha256': hashlib.sha256(raw).hexdigest(),
                  'outputSha256': hashlib.sha256(a.output.read_bytes()).hexdigest(),
                  'methods': 3, 'bodySubstitutions': 0, 'unityOrDeviceExecuted': False}))
