"""Freeze exact old/current camera field UI and consumer bodies, without running a compiler."""
from pathlib import Path
import argparse,hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];WHEEL='components/wheel';PLUGIN=WHEEL+'/src/WodenRallyEdge.Plugin'
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--baseline',default='b84cd999d6a3b7b64a2c6986cd80ff6c0e4b59c6');a=p.parse_args()
out=a.output.resolve()
if out.exists():raise SystemExit('Preserve snapshots; use a fresh output')
def block(s,signature,method=True):
 start=s.index(signature)
 at=s.index(')',start)+1 if method else start
 tail=s[at:].lstrip()
 if method and tail.startswith('=>'):return s[start:s.index(';',at)+1]
 brace=s.index('{',at);depth=1
 for i in range(brace+1,len(s)):
  depth+=(s[i]=='{')-(s[i]=='}')
  if depth==0:return s[start:i+1]
 raise ValueError('Unbalanced source')
records={}
for label,rev in [('baseline',a.baseline),('current','HEAD')]:
 dest=out/label
 names=subprocess.check_output(['git','ls-tree','-r','--name-only',rev,'--',WHEEL+'/src',WHEEL+'/Directory.Build.props'],cwd=ROOT,text=True).splitlines()
 for name in names:
  if not(name.endswith(('.cs','.csproj')) or name.endswith('Directory.Build.props')):continue
  f=dest/name;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(subprocess.check_output(['git','show',f'{rev}:{name}'],cwd=ROOT))
 source=(dest/PLUGIN/'TripleView.cs').read_text(encoding='utf-8-sig')
 signatures=['internal static bool Realistic(','private static float LegacyFovOffset(','private static float FovOffset(','internal static float FramedFov(','internal static bool TryGetPanelFov(','internal static void SetFovFromPanel(','internal static void AdjustFov(','private static void SaveFov(','internal static CameraOffset PositionOffset(','internal static void SetPositionFromPanel(','internal static void ResetCurrent(']
 optional=['internal static bool TryGetPanelFovRequest(','internal static void DefaultFovFromPanel(','internal static void DefaultPositionFromPanel(']
 signatures += [sig for sig in optional if sig in source]
 bodies=[block(source,sig) for sig in signatures]
 generated=dest/'generated';generated.mkdir()
 (generated/'TripleView.Fields.cs').write_bytes(('using System;using WodenRallyEdge.Core;namespace WodenRallyEdge;internal static partial class TripleView {\n'+'\n'.join(bodies)+'\n}\n').encode())
 panel=(dest/PLUGIN/'Panel.cs').read_text(encoding='utf-8-sig')
 slider=block(panel,'private static float Slider(')
 if 'private static float CameraSlider(' in panel:slider+='\n'+block(panel,'private static float CameraSlider(')
 fov=block(panel,'if(TripleView.TryGetPanelFov(out float cameraFov))',False)
 positions=block(panel,'if(TripleView.CameraEditAllowed)\n',False)
 text='using System;using System.Globalization;using WodenRallyEdge.Core;using Dbce.Wheel.Ui;using UnityEngine;namespace WodenRallyEdge;internal static partial class Panel {\n'+slider+'\ninternal static float DrawFov(float y){'+fov+'return y;}\ninternal static float DrawPositions(float y){'+positions+'return y;}\n}\n'
 (generated/'Panel.Fields.cs').write_bytes(text.encode())
 h=lambda s:hashlib.sha256(s.encode()).hexdigest().upper()
 records[label]=dict(revision=subprocess.check_output(['git','rev-parse',rev],cwd=ROOT,text=True).strip(),methods={sig:h(body) for sig,body in zip(signatures,bodies)},slider=h(slider),fovBlock=h(fov),positionBlock=h(positions))
(out/'source-pins.json').write_bytes(json.dumps(records,indent=2).encode())
print(out)
