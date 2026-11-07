using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;

namespace WodenRallyEdge;

// Player installs ship no Dbce.Wheel.Recording.dll or Dbce.Wheel.Playback.dll (STD-050). The real Core and toolkit
// telemetry DLLs are loaded into an isolated context from a folder without them, and TelemetryOutput must still deliver
// dashboard UDP. Any attempt to load a missing DBCE assembly is refused and reported.
internal static class NoRecorderChecks
{
    static readonly string[] PlayerCore = { "WodenRallyEdge.Core.dll", "Dbce.Wheel.Telemetry.dll", "Dbce.Wheel.Ffb.dll" };

    sealed class PlayerFolder : AssemblyLoadContext
    {
        readonly string _folder;
        internal readonly List<string> Refused = new();
        internal PlayerFolder(string folder) : base("woden-player-folder", isCollectible: true) => _folder = folder;
        protected override Assembly? Load(AssemblyName name)
        {
            string path = Path.Combine(_folder, name.Name + ".dll");
            if (File.Exists(path)) return LoadFromAssemblyPath(path);
            if (name.Name!.StartsWith("Dbce.", StringComparison.Ordinal) || name.Name.StartsWith("WodenRallyEdge", StringComparison.Ordinal))
            { Refused.Add(name.Name); throw new FileNotFoundException("Not in the player folder: " + name.Name); }
            return null; // framework assemblies come from the shared runtime
        }
    }

    internal static void UdpWithoutRecorder(Action<bool, string> check)
    {
        string source = Path.GetDirectoryName(typeof(Core.TelemetryOutput).Assembly.Location)!;
        string folder = Path.Combine(Path.GetTempPath(), "woden-player-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var context = new PlayerFolder(folder);
        try
        {
            foreach (string name in PlayerCore) File.Copy(Path.Combine(source, name), Path.Combine(folder, name));
            var core = context.LoadFromAssemblyPath(Path.Combine(folder, "WodenRallyEdge.Core.dll"));
            Type optionsType = core.GetType("WodenRallyEdge.Core.OutputOptions", true)!;
            Type outputType = core.GetType("WodenRallyEdge.Core.TelemetryOutput", true)!;
            Type sampleType = core.GetType("WodenRallyEdge.Core.TelemetrySample", true)!;

            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); receiver.Client.ReceiveTimeout = 2000;
            int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
            object options = Activator.CreateInstance(optionsType, port, 0, 20, null, true)!;
            // The constructor the plugin's player path calls: no recording path, empty recording source.
            var ctor = outputType.GetConstructor(new[] { optionsType, typeof(string), typeof(string), typeof(string), typeof(IReadOnlyDictionary<string, string>) })!;
            var output = (IDisposable)ctor.Invoke(new object?[] { options, "player-folder", "", "fixture", null });
            try
            {
                object sample = Activator.CreateInstance(sampleType)!;
                sampleType.GetProperty("SessionId")?.SetValue(sample, "player-folder");
                sampleType.GetProperty("ElapsedSeconds")?.SetValue(sample, .02);
                sampleType.GetProperty("SimulationSeconds")?.SetValue(sample, .02);
                sampleType.GetProperty("State")?.SetValue(sample, "driving");
                outputType.GetMethod("Publish")!.Invoke(output, new[] { sample });
                IPEndPoint endpoint = new(IPAddress.Any, 0);
                byte[] bytes = receiver.Receive(ref endpoint);
                check(bytes.Length == 324, "Forza packet delivered with no recorder DLL: " + bytes.Length + " bytes");
                check((string)outputType.GetProperty("RecordingStatus")!.GetValue(output)! == "Disabled", "no recorder present");
                outputType.GetMethod("StopRecording")!.Invoke(output, null);
                check((long)outputType.GetProperty("RecordingDrops")!.GetValue(output)! == 0 &&
                    (double)outputType.GetProperty("RecordingElapsedSeconds")!.GetValue(output)! == 0, "recording members answer without a recorder");
            }
            finally { output.Dispose(); }
            check(context.Refused.Count == 0, "no missing DBCE assembly was requested: " + string.Join(", ", context.Refused));
            check(!context.Assemblies.Any(a => a.GetName().Name is "Dbce.Wheel.Recording" or "Dbce.Wheel.Playback"), "recorder and playback stay unloaded");
        }
        finally
        {
            context.Unload();
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
