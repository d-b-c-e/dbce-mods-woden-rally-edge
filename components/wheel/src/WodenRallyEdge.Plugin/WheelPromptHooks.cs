using System.Reflection;
using HarmonyLib;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The arcade header's prompt is one string: ArcadeHeaderText.OnEnable builds it and the static
// MenuCameraScript.Infotext writes it to the bottom-bar Text and to that Text's TypeWriter, which
// retypes it. Rewrite that string only during the header refresh and only when the wheel gave the
// last menu press. Nothing global changes; a stock refresh restores pad/keyboard wording.
// Optional: WheelPrompts.Apply patches these after the required fail-closed PatchAll, never inside it.
internal static class ArcadeHeaderPromptHook
{
    internal static void Prefix(ArcadeHeaderText __instance)
    {
        WheelPrompts.Scoped = WheelPrompts.Applied && WheelPrompts.Active;
        if (WheelPrompts.Applied) WheelPrompts.Shown(__instance); // a hook left behind by a failed rollback stays inert
    }
    internal static void Finalizer() => WheelPrompts.Scoped = false;
}

internal static class InfotextPromptHook
{
    private static bool _reentered;
    // Re-invoke the stock writer with the new text instead of relying on by-ref string marshaling.
    internal static bool Prefix(string __0, bool __1, bool __2, float __3)
    {
        if (_reentered || !WheelPrompts.Applied || !WheelPrompts.Scoped) return true;
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
    internal static bool Scoped, Applied;

    // Optional presentation, patched apart from the required PatchAll: a failure removes any half-applied hook,
    // logs once and leaves stock prompts, never stopping controls, force or F6.
    internal static void Apply(Harmony harmony)
    {
        Applied = false; Scoped = false;
        var patched = new List<(MethodBase Original, MethodInfo Patch)>();
        try
        {
            var header = AccessTools.Method(typeof(ArcadeHeaderText), nameof(ArcadeHeaderText.OnEnable)) ?? throw new MissingMethodException("ArcadeHeaderText.OnEnable");
            var prefix = AccessTools.Method(typeof(ArcadeHeaderPromptHook), nameof(ArcadeHeaderPromptHook.Prefix));
            var finalizer = AccessTools.Method(typeof(ArcadeHeaderPromptHook), nameof(ArcadeHeaderPromptHook.Finalizer));
            // Record each target before patching, so a Patch that throws after installing is still rolled back.
            patched.Add((header, prefix)); patched.Add((header, finalizer));
            harmony.Patch(header, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
            var info = AccessTools.Method(typeof(MenuCameraScript), nameof(MenuCameraScript.Infotext)) ?? throw new MissingMethodException("MenuCameraScript.Infotext");
            var rewrite = AccessTools.Method(typeof(InfotextPromptHook), nameof(InfotextPromptHook.Prefix));
            patched.Add((info, rewrite));
            harmony.Patch(info, prefix: new HarmonyMethod(rewrite));
            Applied = true;
        }
        catch (Exception ex)
        {
            Applied = false; Scoped = false;
            // Best effort; any hook that survives this rollback is inert while Applied is false.
            foreach (var (original, patch) in patched) try { harmony.Unpatch(original, patch); } catch { }
            Runtime.Log.LogWarning("Wheel prompts unavailable; stock prompts kept: " + ex.Message);
        }
    }
    private static ArcadeHeaderText? _header;
    private static string? _shown;
    internal static void Shown(ArcadeHeaderText header) { _header = header; _shown = Key; }
    // What the header would show now: the wheel labels, or the stock pad (0) / keyboard (1) branch.
    private static string Key => Active ? string.Join("|", WheelPromptText.Tokens.Select(t => t.Action).Distinct().Select(Label))
        : "stock " + (int)GamePadSystem.LastInput;
    // The stock header only redraws on enable. While it stays on screen, re-run that same stock writer
    // when the wheel/native choice or a bound label changes, so a device switch restores stock text.
    internal static void Update()
    {
        if (_header == null) return;
        if (!Applied) { _header = null; Scoped = false; return; }
        try
        {
            if (!_header.isActiveAndEnabled) { _header = null; return; }
            if (Key != _shown) _header.OnEnable();
        }
        catch (Exception ex) { _header = null; Runtime.Log.LogWarning("Wheel prompt refresh stopped: " + ex.Message); }
    }
    // Native bindings only: shared-profile (Shim) wheel input arrives through the game's pads.
    internal static bool Active => !ShimInput.Owns && Source.WheelLast;
    internal static void WheelDelivered() => Source.Wheel();
    // GamePadSystem never reports Wheel itself; any held stock pad/keyboard state counts as native.
    internal static void AfterPadRead(GamePadSystem.Game_Pad pad)
    {
        if (!Applied || (Runtime.Local != null && !Pause.Paused)) return; // menus only; skip the per-frame scan while driving
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
