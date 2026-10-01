using Dbce.Wheel.Ffb;
using System.Security.Cryptography;
using System.Text.Json;

// Synthetic comparison only: no device, game collision event, or physical Nm.
const float dt = .005f, strength = .5f, cap = .25f;
var rows = new List<object>();
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
ForceShaper Conditioner() => new() { Strength=50, SmoothingMs=35, SoftSaturation=.5f,
    SlewPerSecond=1.5f, FadeStartKmh=3, FadeFullKmh=12, RampSeconds=.5f, PeakLimit=cap };
foreach (var duration in new[] { .04f, .12f, .30f })
foreach (var peak in new[] { .2f, .5f, 1f }) {
    var positive=Conditioner(); var negative=Conditioner();
    var output=new List<float>(); double inputImpulse=0, outputImpulse=0, square=0; int saturated=0;
    float previous=0, maxSlew=0; int end=0;
    for (int i=0; i<600; i++) {
        float time=i*dt;
        // Warm the ramp at zero for one second; half-sine pulse has explicit sign and duration.
        float raw=time>=1 && time<1+duration ? peak*MathF.Sin(MathF.PI*(time-1)/duration) : 0;
        float command=positive.Shape(raw*strength, 72, dt);
        float mirror=negative.Shape(-raw*strength, 72, dt);
        Check(float.IsFinite(command) && MathF.Abs(command)<=cap+1e-6f,"Envelope exceeded cap");
        Check(MathF.Abs(command+mirror)<1e-6f,"Signed envelopes diverged");
        maxSlew=MathF.Max(maxSlew,MathF.Abs(command-previous)/dt); previous=command;
        output.Add(command); inputImpulse+=raw*dt; outputImpulse+=Math.Abs(command)*dt; square+=command*command;
        if (MathF.Abs(command)>=cap-1e-6f) saturated++;
        if (MathF.Abs(command)>.0015f) end=i;
    }
    // Cap clipping and output deadband may affect finite differences; report them without
    // claiming a slew-guard violation or enabling the event bypass.
    rows.Add(new {durationSeconds=duration, modelPeak=peak, strength, cap,
        commandPeak=output.Max(MathF.Abs), nominalPeakUnits=output.Max(MathF.Abs)*10000,
        rms=Math.Sqrt(square/output.Count), inputImpulse, absoluteCommandImpulse=outputImpulse,
        saturationSeconds=saturated*dt, maxObservedSlew=maxSlew,
        unwindSeconds=Math.Max(0,end*dt-(1+duration)), eventBypass=false});
    foreach(var gate in new[]{"pause","focus","stale","respawn"}) {
        positive.Reset();
        Check(positive.Shape(0,72,dt)==0,$"{gate} reset retained command");
    }
}
var dll=typeof(ForceShaper).Assembly.Location;
var evidence=new {kind="synthetic-command-envelope", physicalTorqueMeasured=false,
    rig="MOZA R12 identity from handoff; driver gain and torque calibration unknown",
    sampleSeconds=dt, algorithm="exact pinned shared ForceShaper; current Woden conditioning; no impact bypass",
    ffbArtifactSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant(),
    fixtureSourceSha256=args.Length>1 ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))).ToLowerInvariant() : null,
    checks, rows};
if(args.Length>0) File.WriteAllText(args[0],JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS normalized envelope fixture: {checks} checks, 9 signed pulse pairs; no devices or runtime effect change.");
