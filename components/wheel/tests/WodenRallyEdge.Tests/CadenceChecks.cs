#if WODEN_CADENCE_CANDIDATE
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;
namespace WodenRallyEdge;

internal static class CadenceChecks
{
    internal static void Run(Action<bool, string> check, Func<double, TelemetrySample> contact)
    {
        double clock = 1, sim = 1;
        var (_, device) = ForceControllerChecks.Create();
        var controller = new ForceController(device, new CadenceForceAdapter(() => clock));
        controller.Prepare();
        void Tick() => controller.Tick(contact(sim += .02));
        Tick(); check(device.Writes.Last() == 0, "first output timestamp primes at zero");
        for (int i = 0; i < 80; i++)
        {
            float previous = controller.Sent; clock += .02; Tick();
            check(Math.Abs(controller.Sent - previous) <= .030001, "actual adapter command obeys output-clock slew");
            check(Math.Abs(controller.Sent) <= .250001, "actual adapter preserves rig cap");
        }
        check(device.Writes.Any(x => Math.Abs(x) > .01), "candidate actual controller reaches structural force");
        foreach (double invalid in new[] { clock, clock - .01, clock + .2, double.NaN, double.PositiveInfinity })
        {
            int writes = device.Writes.Count, zeros = device.Zeros;
            clock = invalid; Tick();
            check(controller.Sent == 0 && device.Zeros == zeros + 1 && device.Writes.Count == writes,
                "invalid output interval immediately stops existing effect without stale write");
            clock = sim + 10; Tick(); clock += .02; Tick();
        }
        int before = device.Zeros;
        var paused = contact(sim += .02); paused.State = "paused"; controller.Tick(paused);
        check(controller.Sent == 0 && device.Zeros == before + 1, "pause immediately stops candidate output");
        clock += .02; Tick(); check(device.Writes.Last() == 0, "resume primes at zero");
        clock += .02; Tick();
        var bad = contact(sim += .02); bad.Channels["wheel.fl.contactForce"] = double.NaN;
        before = device.Zeros; controller.Tick(bad);
        check(controller.Sent == 0 && device.Zeros == before + 1, "nonfinite model signal stops candidate output");
        clock += .02; Tick(); clock += .02; device.AcceptWrite = false; Tick();
        check(controller.Sent == 0 && controller.Failures == 1 && device.Closes == 1, "failed write zeroes and closes");
        controller.Shutdown();

        string? source = Environment.GetEnvironmentVariable("WODEN_CADENCE_SOURCE");
        if (source == null) return;
        check(RecordingArtifacts.Sha256(source) == "785d8d679c9dc654ea860212074b920b59944d0c6315ad40a2e9f26bd83d23b8", "immutable replay identity");
        var (legacy, legacyDevice) = ForceControllerChecks.Create();
        var candidateDevice = new FakeForceDevice();
        var candidate = new ForceController(candidateDevice, new CadenceForceAdapter(() => clock));
        legacy.Prepare(); candidate.Prepare();
        int rows = 0, changed = 0; double maximum = 0;
        foreach (var record in SessionReader.Read(source))
        {
            if (record.Kind != SessionRecordKind.Sample || !record.Sample.Channels.ContainsKey("ffb.modelResetBefore")) continue;
            var row = record.Sample; var c = row.Channels;
            Runtime.Settings.ForceOptions = new((float)c["ffb.tuning.strengthPercent"], (float)c["ffb.tuning.peakPercent"],
                (float)c["ffb.tuning.loadReference"], (float)c["ffb.tuning.slipScale"], (float)c["ffb.tuning.smoothingMs"],
                (float)c["ffb.tuning.damping"], c["ffb.tuning.invert"] != 0);
            // Replay timestamps are an injected software clock scenario, not measured device-call timing.
            clock = row.ElapsedSeconds;
            Panel.Open = c["ffb.gate"] != 0;
            TelemetrySample Copy()
            {
                var s = new TelemetrySample { ElapsedSeconds = row.ElapsedSeconds, SimulationSeconds = c["sample.simulationSeconds"],
                    State = c["sample.driving"] == 1 ? "driving" : "inactive", Discontinuity = c["sample.discontinuity"] == 1 ? "recorded" : null };
                foreach (var pair in c) s.Channels[pair.Key] = pair.Value;
                return s;
            }
            legacy.Tick(Copy()); candidate.Tick(Copy()); rows++;
            check(Math.Abs(candidate.Sent) <= Runtime.Settings.ForceOptions.PeakPercent / 100 + 1e-6, "recorded-input candidate cap");
            check(legacy.Sent == (Panel.Open || !legacy.Last.Valid ? 0 : legacy.Last.Preview), "default controller output unchanged");
            double delta = Math.Abs(candidate.Sent - legacy.Sent); if (delta > 1e-6) changed++; maximum = Math.Max(maximum, delta);
        }
        check(rows == 8251 && changed > 0, "complete recorded model input comparison");
        Console.WriteLine($"CADENCE_REPLAY modelRows={rows} changed={changed} maxDelta={maximum:R} legacyWrites={legacyDevice.Writes.Count} candidateWrites={candidateDevice.Writes.Count} candidateStops={candidateDevice.Zeros}");
        Panel.Open = false; legacy.Shutdown(); candidate.Shutdown();
    }
}
#endif
