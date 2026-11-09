// Unrelated binding-default/digital-index boundaries only. No plugin/native/device loaded.
namespace WodenRallyEdge.Core { static class CameraTuning { public static readonly string[] Actions = System.Array.Empty<string>(); public static Dictionary<string,string> DefaultKeys() => new(); public static bool MigratePreviousDefaults(Dictionary<string,string> keys) => false; } }
namespace Dbce.Wheel.Ffb { static class DigitalInput { public static bool Valid(int value) => value >= 0 && value < 160; } }
