using WodenRallyEdge.Core;
namespace WodenRallyEdge;
// Missing game types only, for the existing fake Runtime fixture. No model/device implementation.
internal sealed class WheelInput { internal readonly Bindings Bindings = new(); }
internal sealed class MainCar { internal bool Selected = true; }
