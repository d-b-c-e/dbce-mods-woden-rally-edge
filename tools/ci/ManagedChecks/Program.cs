using WodenRallyEdge;
using WodenRallyEdge.Core;

if (args.Length > 0 && args[0] == "--ffb-summary") return FfbSummaryCommand.Run(args, Console.Error);
try {
    // These existing fake context fields serve excluded timer/game fixtures.
    Runtime.Local = null; Runtime.Settings.CountdownAssistEnabled = false; Runtime.Devices!.Candidates = null;
    int checks = 0;
    void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
    TelemetrySample Contact(double time) {
        var sample = new TelemetrySample { SessionId = "ci-synthetic", State = "driving", SimulationSeconds = time, ElapsedSeconds = time, CarInstanceId = 1 };
        sample.Add("motion.speed", 20); sample.Add("motion.velocity.local.z", 20); sample.Add("wheelInput.steer", 0);
        foreach (string corner in new[] { "fl", "fr" }) {
            sample.Add("wheel." + corner + ".grounded", 1); sample.Add("wheel." + corner + ".contactForce", 3000); sample.Add("wheel." + corner + ".sidewaysSlip", .3);
        }
        return sample;
    }
    Directory.CreateDirectory(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/tests")));
    ForceControllerChecks.Recovery(Check, Contact); ForceControllerChecks.Failures(Check, Contact);
    LifecycleChecks.Run(Check, Contact); CommandCaptureChecks.Run(Check, Contact);
    FfbRegressionChecks.Run(Check); FfbRegressionChecks.ConcurrentCreation(Check); FfbSummaryCommandChecks.Run(Check);
    Console.WriteLine($"PASS asset-free production-linked managed subset: {checks} assertions; no devices or private references.");
    return 0;
} catch (Exception ex) {
    Console.Error.WriteLine("FAIL asset-free-managed: " + ex.GetType().Name + ": " + ex.Message);
    return 1;
}
