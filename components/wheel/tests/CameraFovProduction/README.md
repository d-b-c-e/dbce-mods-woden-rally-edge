# Actual cockpit/framing FOV edit and persistence witness

prepare.py copies committed old/current source and extracts eight unchanged
TripleView method bodies, including AdjustFov and SaveFov. Generated code calls
the real Core policy and real CameraFramingStore/ConfigLine/BepInEx writer.
Host.cs substitutes only camera availability/native measurements and notices;
it contains no replacement FOV editing, storage or projection implementation.

Fixture.csproj takes ProductionRoot, HarnessRoot, GeneratedRoot and LibDir.
The actual Core project needs its existing qualified toolkit/recording references
under the snapshot's components/wheel/lib. No game/Unity assembly is needed by
this fixture. Keep source snapshots, dependencies and compiler pinned in a fresh
ignored CPU packet; 

Run Fixture.dll <fresh-evidence-directory> [lens|store|projection|consumer].
Each named witness must compile on the old source and then exit1 with its
REGRESSION prefix. Current source must pass all. Store tests use private real
files, preserve failure outputs, and do not edit owner settings. Existing
CameraSets and full plugin compilation remain separate required checks.

This does not prove Unity callback timing, real keyboard/F6 events, installed
behavior, renderer geometry/seams, physical hardware or owner acceptance.
