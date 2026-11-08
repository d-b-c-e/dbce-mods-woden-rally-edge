using System.Text.Json;
using Dbce.Wheel.Ffb;
using WodenRallyEdge.Core;

var json = new JsonSerializerOptions(TelemetryOutput.Json) { WriteIndented = true };
if (args.Length == 2 && args[0] == "schema")
{
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { schema = TelemetrySchema.Name, version = TelemetrySchema.Version, channels = TelemetrySchema.Channels }, json));
    Console.WriteLine($"Wrote {TelemetrySchema.Channels.Count} channel definitions to {args[1]}"); return 0;
}
if (args.Length == 2 && args[0] == "devices")
{
    if (!WheelFfbNative.Load(Path.GetFullPath(args[1]))) throw new IOException(WheelFfbNative.LastError);
    var devices = WheelFfbNative.ListAllDevices().Select(x => new { x.Name, x.InstanceGuid, x.Axes, x.Buttons, x.ForceFeedback });
    Console.WriteLine(JsonSerializer.Serialize(devices, json));
    WheelFfbNative.ShutdownAll(); return 0;
}
Console.Error.WriteLine("Usage: schema <output.json> | devices <native DLL directory>");
return 1;
