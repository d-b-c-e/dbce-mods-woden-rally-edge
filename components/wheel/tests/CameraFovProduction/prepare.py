"""Freeze real old/current FOV editing method bodies and source snapshots; no build/test/Unity."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
WHEEL = 'components/wheel'
PLUGIN = WHEEL + '/src/WodenRallyEdge.Plugin'
ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument('--output', type=Path, required=True)
ap.add_argument('--baseline', default='9f961f38f80737c7c92e56cd2578c18321c2af7a')
args = ap.parse_args()
out = args.output.resolve()
if out.exists():
    raise SystemExit('Preserve existing snapshots; choose a fresh output.')

def method(source, signature):
    start = source.index(signature)
    params = source.index(')', start) + 1  # selected signatures have no nested parameter expressions
    tail = source[params:].lstrip()
    if tail.startswith('=>'):
        return source[start:source.index(';', params) + 1]
    brace = source.index('{', params)
    depth = 1
    for end in range(brace + 1, len(source)):
        depth += (source[end] == '{') - (source[end] == '}')
        if not depth:
            return source[start:end + 1]
    raise ValueError('Unbalanced source method')

records = {}
for label, revision in [('baseline', args.baseline), ('current', 'HEAD')]:
    dest = out / label
    names = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', revision, '--', WHEEL + '/src', WHEEL + '/Directory.Build.props'], cwd=ROOT, text=True).splitlines()
    for name in names:
        if not (name.endswith(('.cs', '.csproj')) or name.endswith('Directory.Build.props')):
            continue
        target = dest / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(subprocess.check_output(['git', 'show', f'{revision}:{name}'], cwd=ROOT))
    source = (dest / PLUGIN / 'TripleView.cs').read_text(encoding='utf-8-sig')
    signatures = ['internal static bool Realistic(', 'private static float LegacyFovOffset(',
                  'private static float FovOffset(', 'internal static float FramedFov(',
                  'internal static bool TryGetPanelFov(', 'internal static void SetFovFromPanel(',
                  'internal static void AdjustFov(', 'private static void SaveFov(']
    bodies = [method(source, signature) for signature in signatures]
    generated = dest / 'generated'
    generated.mkdir()
    (generated / 'TripleView.Fov.cs').write_text('using System; using WodenRallyEdge.Core; namespace WodenRallyEdge; internal static partial class TripleView {\n' + '\n'.join(bodies) + '\n}\n', encoding='utf-8')
    records[label] = {'revision': subprocess.check_output(['git', 'rev-parse', revision], cwd=ROOT, text=True).strip(),
                      'methods': {sig: hashlib.sha256(body.encode()).hexdigest() for sig, body in zip(signatures, bodies)},
                      'files': {str(p.relative_to(dest)): hashlib.sha256(p.read_bytes()).hexdigest() for p in dest.rglob('*') if p.is_file()}}
(out / 'source-pins.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
print(f'Staged exact source bodies at {out}; no compilation or test execution.')
