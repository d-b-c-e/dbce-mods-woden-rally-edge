using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// These native consumers do not have a selected Unity submit handler. Borrow
// one existing input field for one original callback; native side effects and
// eligibility remain native. Never write a scene, selection or progression flag.
[HarmonyPatch(typeof(ArcadeCarSelect), nameof(ArcadeCarSelect.FixedUpdate))]
internal static class ArcadeWheelMenuHook
{
    internal static bool Prefix(ArcadeCarSelect __instance, out CustomMenuLease? __state)
    {
        __state = null;
        if (!CustomMenuLease.Recover()) return false;
        if (!StartupMenuGuard.AllowNativeMenu()) { CustomWheelMenu.Reset(); return false; }
        __state = CustomWheelMenu.Arcade(__instance); return true;
    }
    internal static void Postfix(CustomMenuLease? __state) => __state?.Restore();
    internal static void Finalizer(CustomMenuLease? __state) => __state?.Restore();
}

[HarmonyPatch(typeof(StagePresentation), nameof(StagePresentation.Update))]
internal static class PresentationWheelMenuHook
{
    internal static bool Prefix(StagePresentation __instance, out CustomMenuLease? __state)
    {
        __state = null;
        if (!CustomMenuLease.Recover()) return false;
        if (!StartupMenuGuard.AllowNativeMenu()) { CustomWheelMenu.Reset(); return false; }
        __state = CustomWheelMenu.Presentation(__instance); return true;
    }
    internal static void Postfix(CustomMenuLease? __state) => __state?.Restore();
    internal static void Finalizer(CustomMenuLease? __state) => __state?.Restore();
}

// Photo mode's take and exit are Pause.Update reading Pause.MainControl's A and B (docs/WHEEL-PROMPTS.md). That method
// also runs the pause screen every frame, so it always runs (a failed earlier restore still holds it back, as above);
// a field is lent only while photo mode is up.
[HarmonyPatch(typeof(Pause), nameof(Pause.Update))]
internal static class PhotoModeWheelMenuHook
{
    internal static bool Prefix(Pause __instance, out CustomMenuLease? __state)
    {
        __state = null;
        if (!CustomMenuLease.Recover()) return false;
        __state = CustomWheelMenu.PhotoMode(__instance); return true;
    }
    internal static void Postfix(CustomMenuLease? __state) => __state?.Restore();
    internal static void Finalizer(CustomMenuLease? __state) => __state?.Restore();
}

internal sealed class CustomMenuLease
{
    private static CustomMenuLease? _pending;
    internal static bool Recover()
    {
        if (_pending==null) return true;
        CustomWheelMenu.Reset(); _pending.Restore(); return _pending==null;
    }
    private MenuControls? _controls;
    private readonly string _action;
    private readonly bool _previous;
    private bool Read(MenuControls c) => _action switch {
        "Confirm" => c.ButtonA, "Back" => c.ButtonB, "Menu left" => c.DPadLeft,
        "Menu right" => c.DPadRight, "Menu up" => c.DPadUp, "Menu down" => c.DpadDown,
        _ => throw new InvalidOperationException("Unqualified menu action") };
    private void Write(MenuControls c, bool value)
    {
        switch (_action) {
            case "Confirm": c.ButtonA=value; break; case "Back": c.ButtonB=value; break;
            case "Menu left": c.DPadLeft=value; break; case "Menu right": c.DPadRight=value; break;
            case "Menu up": c.DPadUp=value; break; case "Menu down": c.DpadDown=value; break;
            default: throw new InvalidOperationException("Unqualified menu action");
        }
    }
    internal CustomMenuLease(MenuControls controls, string action)
    {
        _action=action; _previous=Read(controls); _controls=controls;
        try { Write(controls,true); if(!Read(controls)) throw new InvalidOperationException("Native menu input readback differs"); WheelPrompts.WheelDelivered(); }
        catch { Restore(); throw; }
    }
    internal void Restore()
    {
        if (_controls is not { } c) return;
        try {
            Write(c,_previous);
            if(Read(c)!=_previous) throw new InvalidOperationException("Native menu restore readback differs");
            _controls=null;
            if(ReferenceEquals(_pending,this)) _pending=null;
            Runtime.Log.LogInfo("Bound wheel custom menu input restored: " + _action + "=" + _previous);
        } catch(Exception ex) { _pending=this; Runtime.Log.LogWarning("Custom menu restore failed: " + ex.Message); }
    }
}

internal static class CustomWheelMenu
{
    private static readonly InputReleaseGate Release = new();
    private static readonly string[] Actions = MenuNavigation.Actions.Concat(new[]{"Pause"}).ToArray();
    private static int _instance;
    private static ArcadeCarSelect? _arcade;
    private static Pause? _pause;
    private static int _photo;
    private static double _nextError;
    internal static void Reset() { Release.Reset(); _instance=0; }
    // Car choice uses native fields; transmission choice uses real Unity buttons. In photo mode A and B belong to
    // Pause.Update, never to the hidden pause menu's selection.
    internal static bool OwnsUnityDispatch {
        get {
            try { return _arcade != null && _arcade.isActiveAndEnabled && _arcade.PlayerIndex==0 &&
                _arcade.field_Private_Boolean_0 && GameMaster.NrOfPlayers==1 && !GameMaster.Demo && Runtime.Local==null ||
                _pause != null && PhotoRoute && Pause.Paused && _pause.PhotomodeActive; }
            catch { return false; }
        }
    }
    // Where photo mode's wheel route can serve at all; its prompts are relabelled only then.
    internal static bool PhotoRoute => GameMaster.NrOfPlayers==1 && !GameMaster.Demo;
    private static bool NativeHeld(MenuControls c) => c.ButtonA || c.ButtonB || c.ButtonStart || c.ButtonBack ||
        c.DPadLeft || c.DPadRight || c.DPadUp || c.DpadDown || c.LSLeft || c.LSRight || c.LSUp || c.LSDown;
    private static string? Press(int instance, bool eligible, MenuControls? controls)
    {
        if (_instance!=instance) { Reset(); _instance=instance; }
        var wheel=Runtime.Wheel; var hub=Runtime.Devices;
        if (!eligible || controls==null || !Runtime.Focused || Panel.Open || MenuOwnership.Closing ||
            wheel==null || hub==null || wheel.Capturing || NativeHeld(controls) || Input.GetKey(KeyCode.Escape))
        { Release.Reset(); return null; }
        string? held=null;
        foreach(string action in Actions) {
            if(ShimInput.Owns) {
                if(!ShimInput.Ready) { Release.Reset(); return null; }
            } else {
                if(!wheel.Bindings.Buttons.TryGetValue(action,out var b)) continue;
                var devices=hub.Devices.Where(d=>d.Info.InstanceGuid==b.DeviceGuid).ToArray();
                if(!b.Valid || devices.Length!=1 || !devices[0].Ok) { Release.Reset(); return null; }
            }
            if(wheel.Button(action,false)) {
                // Ambiguous simultaneous directions/actions never choose a menu for the user.
                if(held!=null) { Release.Reset(); return null; }
                held=action;
            }
        }
        if(held==null) { Release.Observe(true,false,Runtime.Clock.Elapsed.TotalSeconds); return null; }
        bool ready=Release.Ready; Release.Reset();
        return ready && held!="Pause" ? held : null;
    }
    private static CustomMenuLease Begin(MenuControls c,string action,string consumer)
    {
        var lease=new CustomMenuLease(c,action);
        try {
            MenuNavigation.SuppressNativeAction(action);
            Runtime.Log.LogInfo("Bound wheel " + action + ": native " + consumer + " input");
            return lease;
        } catch { lease.Restore(); throw; }
    }
    internal static CustomMenuLease? Arcade(ArcadeCarSelect car)
    {
        try {
            _arcade=car;
            bool eligible=OwnsUnityDispatch && !car.field_Private_Boolean_1 && !MenuCameraScript.Exiting;
            var controls=car.field_Private_MenuControls_0;
            string? action=Press(car.GetInstanceID(),eligible,controls);
            return action==null || controls==null ? null : Begin(controls,action,"Arcade car selection");
        } catch(Exception ex) { Failed(ex); return null; }
    }
    internal static CustomMenuLease? Presentation(StagePresentation stage)
    {
        try {
            bool eligible=stage.isActiveAndEnabled && stage.field_Private_Boolean_0 && !stage.field_Private_Boolean_1 &&
                !MenuCameraScript.Exiting && !GameMaster.Demo && GameMaster.NrOfPlayers==1 && Runtime.Local==null;
            string? action=Press(stage.GetInstanceID(),eligible,stage.MyControls);
            return action=="Confirm" && stage.MyControls!=null ? Begin(stage.MyControls,action,"stage presentation") : null;
        } catch(Exception ex) { Failed(ex); return null; }
    }
    // Pause.Update takes the photo on MainControl.ButtonA and leaves photo mode on ButtonB, each once until its own
    // B_Pressed latch clears. Leaving photo mode resets the release gate, so the next entry needs a fresh neutral.
    internal static CustomMenuLease? PhotoMode(Pause pause)
    {
        try {
            if (!Pause.Paused || !pause.PhotomodeActive) {   // every race frame: no further native calls
                if (_photo!=0 && _photo==pause.GetInstanceID()) { _photo=0; _pause=null; Reset(); }
                return null;
            }
            int id=pause.GetInstanceID();
            _photo=id; _pause=pause;
            if (!StartupMenuGuard.AllowNativeMenu()) { Reset(); return null; }
            var controls=pause.MainControl;
            string? action=Press(id,PhotoRoute && !pause.B_Pressed,controls);
            return action is "Confirm" or "Back" && controls!=null ? Begin(controls,action,"photo mode") : null;
        } catch(Exception ex) { Failed(ex); return null; }
    }
    private static void Failed(Exception ex)
    {
        Reset();
        if(Runtime.Clock.Elapsed.TotalSeconds < _nextError) return;
        _nextError=Runtime.Clock.Elapsed.TotalSeconds+10;
        Runtime.Log.LogWarning("Custom menu wheel input unavailable: " + ex.Message);
    }
}
