using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace WodenRallyEdge;

internal static class ControlActions
{
    internal static Il2CppReferenceArray<GamePadSystem.Actions>? For(Controls controls)
    {
        var pads = controls.field_Private_GamePadSystem_0?.Game_Pads;
        var pad = pads == null || pads.Count == 0 ? controls.field_Private_Game_Pad_0 :
            controls.Controller_Int >= 0 && controls.Controller_Int < pads.Count ? pads[controls.Controller_Int] : null;
        return pad?.PadActions;
    }
}
