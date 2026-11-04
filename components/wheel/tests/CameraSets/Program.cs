using System.Text.Json;
using WodenRallyEdge;
using WodenRallyEdge.Core;

var results = new List<object>();
int failures = 0;
void Check(string assertion, bool pass)
{
    results.Add(new { assertion, pass });
    if (!pass) failures++;
}

// The old production helper was called by startup with the persisted CompareView.
// A player setting from the retired comparison cycle must no longer disable the
// normal corrected renderer. Shift+F7 still has a separate in-memory cycle.
Check("retired Original pick cannot disable next-start correction", TripleGeometry.FromSaved(1) == null);
Check("retired Angle pick cannot suppress next-start curve", TripleGeometry.FromSaved(2) == null);
Check("retired Angle+curve pick is not session developer state", TripleGeometry.FromSaved(3) == null);
Check("normal flat output starts corrected", TripleGeometry.CurrentStep(null, false) == TripleGeometry.AngleStep);
Check("normal curved output starts fully corrected", TripleGeometry.CurrentStep(null, true) == TripleGeometry.AngleCurveStep);
Check("developer comparison retains original as a session step", TripleGeometry.NextStep(null, true) == TripleGeometry.OriginalStep);

Check("stock chase keeps native framing despite old global switches", TripleGeometry.ShowsGameFov(null, false, false, MountedView.Stock));
double chaseEye = TripleGeometry.EyeDistanceForView(398.5, 660, false, false, MountedView.Stock, 70, null);
Check("stock chase projection follows native lens", Math.Abs(chaseEye - TripleGeometry.EyeDistanceForVerticalFov(398.5, 70)) < 1e-9);

var physical = CurvedRig.Compute(708.4, 398.5, 660, 70, 8, 1500);
Check("measured eye distance stays at the panel-edge plane", Math.Abs(physical.Warps![1].Panel.LowerLeft.Z + 660) < 1e-8);
var keys = CameraTuning.DefaultKeys();
Check("numpad plus narrows the field of view", keys["Camera narrower"] == "NumpadPlus");
Check("numpad minus widens the field of view", keys["Camera wider"] == "NumpadMinus");
Check("narrowing actually zooms in", CameraTuning.Adjust(CameraPose.Bonnet, "Camera narrower").Fov < CameraPose.Bonnet.Fov);

Console.WriteLine(JsonSerializer.Serialize(new
{
    schema = "woden.camera-sets-regression@1",
    pass = failures == 0,
    checks = results.Count,
    failures,
    productionFunctions = true,
    engineOrDeviceLoaded = false,
    assertions = results
}, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 ? 0 : 1;
