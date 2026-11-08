using BepInEx.Configuration;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Hides the game's black pillarbox on a three-screen window: the menus' borders
/// (Menu Camera/CANVAS/ScreenBorders), where they leave black bands between the centred menu
/// and the edges, and the stage's borders (SYSTEM/ScreenBorders).
/// </summary>
/// <remarks>
/// The game re-enables and moves the borders as menus change, so they are checked twice a
/// second. Only the Image components are turned off; the objects stay active for the
/// game's own logic.
/// The stage's borders are a camera-space canvas 0.1 m in front of the driving camera. The
/// stock 0.2 m near clip always hides them, but the bonnet and bumper views clip at 0.03 m,
/// and with three views off (F11) they covered the middle of the span in a dark block (ISS-0047).
/// </remarks>
internal static class MenuBorders
{
    private static readonly string[] Paths = { "Menu Camera/CANVAS/ScreenBorders", "SYSTEM/ScreenBorders" };
    private static ConfigEntry<bool> _enabled = null!;
    private static double _next;
    private static bool _failed;

    internal static void Bind(ConfigFile cfg) =>
        _enabled = cfg.Bind("Triple", "HideMenuBorders", true, "On a three-screen window, hide the black bars the game draws beside its centered menus and views.");

    internal static void FrameTick(double now)
    {
        if (now < _next) return;
        _next = now + 0.5;
        if (_failed || !_enabled.Value || Screen.width < Screen.height * 2.9f) return;
        try
        {
            foreach (var path in Paths)
            {
                var borders = GameObject.Find(path);
                if (borders == null) continue;
                foreach (var image in borders.GetComponentsInChildren<UnityEngine.UI.Image>())
                    if (image.enabled) { image.enabled = false; Runtime.Log.LogInfo("Menu borders: hid " + path + "/" + image.name); }
            }
        }
        catch (Exception ex) { _failed = true; Runtime.Log.LogWarning("Menu borders: off for this launch after error: " + ex.Message); }
    }
}
