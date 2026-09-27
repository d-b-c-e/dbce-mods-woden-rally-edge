using System.Text;
using Dbce.TripleScreen;
using WodenTripleScreenProbe.Core;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
void Reject(string json, string message)
{
    assertions++;
    try { LayoutV1.Parse(Encoding.UTF8.GetBytes(json)); }
    catch (InvalidDataException) { return; }
    throw new Exception(message);
}

// Exact v1 values in the saved optimizer output; physical dimensions are diagonal-derived
// because a visible active-area width has not been measured.
string owner = """
{
  "schemaVersion": 1,
  "panel": {"count":3,"nativeWidthPx":2560,"nativeHeightPx":1440,
    "physicalWidthMm":708.4165965748336,"physicalHeightMm":398.4843355733439,
    "curveRadiusMm":1500,"bezelWidthMm":8},
  "geometry":{"eyeDistanceMm":660,"eyeHeightAbovePanelCenterMm":0,
    "leftYawDegrees":70,"rightYawDegrees":70},
  "output":{"mode":"nvidia-surround","combinedWidthPx":7680,"combinedHeightPx":1440}
}
""";

var layout = LayoutV1.Parse(Encoding.UTF8.GetBytes(owner));
layout.RequireCurrentOwnerRig();
Check(layout.LeftYawDegrees == 70 && layout.RightYawDegrees == 70, "owner yaw");
Check(layout.Sha256.Length == 64, "raw layout hash");
var views = layout.Projections(.1, 1000);
Check(views.Count == 3, "three views");
Check(Math.Abs(views[1].Left + views[1].Right) < 1e-9, "center symmetry");
Check(Math.Abs(views[0].Left + views[0].Right) > .001, "left asymmetry");
Check(Math.Abs(views[2].Left + views[2].Right) > .001, "right asymmetry");
Check(Vector3d.Dot(views[0].CameraForward, views[1].CameraForward) < .5,
    "left view rotated with physical panel");
Check(Vector3d.Dot(views[2].CameraForward, views[1].CameraForward) < .5,
    "right view rotated with physical panel");
Check(views.All(v => v.Near == .1 && v.Far == 1000), "clip range");
Check(layout.BezelMm == 8, "gap retained as input, not corrected by toolkit");
var eye = new Vector3d(0, 0, 0);
var leftHinge = EyeRayCalculator.ThroughPanelUv(views[0].Surface, eye, 1, .5);
var centerLeft = EyeRayCalculator.ThroughPanelUv(views[1].Surface, eye, 0, .5);
var centerRight = EyeRayCalculator.ThroughPanelUv(views[1].Surface, eye, 1, .5);
var rightHinge = EyeRayCalculator.ThroughPanelUv(views[2].Surface, eye, 0, .5);
Check((leftHinge.Direction - centerLeft.Direction).Length < 1e-9, "left 70-degree modeled hinge ray");
Check((rightHinge.Direction - centerRight.Direction).Length < 1e-9, "right 70-degree modeled hinge ray");
var plans = views.Select(UnityPanelPlan.From).ToArray();
Check(Math.Abs(plans[1].LocalForward.Z - 1) < 1e-9, "Unity center looks local +Z");
Check(plans[0].LocalForward.X < -.5 && plans[2].LocalForward.X > .5,
    "Unity side views face opposite outward directions");
Check(plans.All(p => Math.Abs(p.LocalForward.Length - 1) < 1e-9), "unit view directions");

var stale = LayoutV1.Parse(Encoding.UTF8.GetBytes(owner.Replace("\"leftYawDegrees\":70", "\"leftYawDegrees\":60")));
assertions++;
try { stale.RequireCurrentOwnerRig(); throw new Exception("stale 60-degree profile accepted"); }
catch (InvalidDataException) { }
Reject(owner.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), "unknown schema accepted");
Reject(owner.Replace("\"count\":3", "\"count\":2"), "two panels accepted");
Reject(owner.Replace("\"bezelWidthMm\":8", "\"bezelWidthMm\":-1"), "negative bezel accepted");
Reject(owner.Replace("\"nativeWidthPx\":2560", "\"nativeWidthPx\":0"), "zero pixels accepted");
Reject(owner.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\"surprise\":0,"), "unknown property accepted");
Reject(owner.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\"schemaVersion\":1,"), "duplicate property accepted");

Console.WriteLine($"Passed {assertions} offline layout/geometry assertions.");
