// Unrelated binding-default/digital-index boundaries only. No plugin/native/device loaded.
namespace WodenRallyEdge.Core { static class CameraTuning { public static Dictionary<string,string> DefaultKeys() => new(); } }
namespace Dbce.Wheel.Ffb { static class DigitalInput { public static bool Valid(int value) => value >= 0 && value < 160; } }
