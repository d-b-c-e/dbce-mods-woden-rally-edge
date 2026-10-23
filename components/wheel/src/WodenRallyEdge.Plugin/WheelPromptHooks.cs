using HarmonyLib;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The arcade header's prompt is one string: ArcadeHeaderText.OnEnable builds it and the static
// MenuCameraScript.Infotext writes it to the bottom-bar Text and to that Text's TypeWriter, which
// retypes it. Rewrite that string only during the header refresh and only when the wheel gave the
// last menu press. Nothing global changes, so the next stock refresh restores pad/keyboard wording.
[HarmonyPatch(typeof(ArcadeHeaderText), nameof(ArcadeHeaderText.OnEnable))]
internal static class ArcadeHeaderPromptHook
{
    internal static void Prefix() => WheelPrompts.Scoped = WheelPrompts.Active;
    internal static void Finalizer() => WheelPrompts.Scoped = false;
}

[HarmonyPatch(typeof(MenuCameraScript), nameof(MenuCameraScript.Infotext))]
internal static class InfotextPromptHook
{
    private static bool _reentered;
    // Re-invoke the stock writer with the new text instead of relying on by-ref string marshaling.
    internal static bool Prefix(string __0, bool __1, bool __2, float __3)
    {
        if (_reentered || !WheelPrompts.Scoped) return true;
        string text = WheelPrompts.Rewrite(__0);
        if (ReferenceEquals(text, __0)) return true;
        _reentered = true;
        try { MenuCameraScript.Infotext(text, __1, __2, __3); }
        finally { _reentered = false; }
        return false;
    }
}

internal static class WheelPrompts
{
    internal static readonly MenuInputSource Source = new();
    internal static bool Scoped;
    // Native bindings only: shared-profile (Shim) wheel input arrives through the game's pads.
    internal static bool Active => !ShimInput.Owns && Source.WheelLast;
    internal static void WheelDelivered() => Source.Wheel();
    // GamePadSystem never reports Wheel itself; any held stock pad/keyboard state counts as native.
    internal static void AfterPadRead(GamePadSystem.Game_Pad pad)
    {
        if (Runtime.Local != null && !Pause.Paused) return; // menus only; skip the per-frame scan while driving
        try { if (MenuOwnership.Held(pad)) Source.Native(); }
        catch (Exception ex) { Runtime.Log.LogWarning("Wheel prompt input check failed: " + ex.Message); }
    }
    internal static string Rewrite(string text)
    {
        try { return WheelPromptText.Rewrite(text, Label); }
        catch (Exception ex) { Runtime.Log.LogWarning("Wheel prompt kept stock text: " + ex.Message); return text; }
    }
    private static string? Label(string action) =>
        Runtime.Wheel?.Bindings.Buttons.TryGetValue(action, out var b) == true && b.Valid && Runtime.Devices?.IsReading(b.DeviceGuid) == true
            ? Dbce.Wheel.Ffb.DigitalInput.Label(b.Button) : null;
}
