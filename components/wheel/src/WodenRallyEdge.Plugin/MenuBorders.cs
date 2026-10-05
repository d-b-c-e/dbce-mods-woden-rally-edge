using BepInEx.Configuration;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Hides the menus' black pillarbox (Menu Camera/CANVAS/ScreenBorders) on a three-screen
/// window, where it leaves black bands between the centred menu and the edges.
/// </summary>
/// <remarks>
/// The game re-enables and moves the borders as menus change, so they are checked twice a
/// second. Only the Image components are turned off; the objects stay active for the
/// game's own logic.
/// </remarks>
internal static class MenuBorders
{
    private static ConfigEntry<bool> _enabled = null!;
    private static double _next;
    private static bool _failed;

    internal static void Bind(ConfigFile cfg) =>
        _enabled = cfg.Bind("Triple", "HideMenuBorders", true, "On a three-screen window, hide the black bars the menus draw beside their centred layout.");

    internal static void FrameTick(double now)
    {
        if (now < _next) return;
        _next = now + 0.5;
        if (_failed || !_enabled.Value || Screen.width < Screen.height * 2.9f) return;
        try
        {
            var borders = GameObject.Find("Menu Camera/CANVAS/ScreenBorders");
            if (borders == null) return;
            foreach (var image in borders.GetComponentsInChildren<UnityEngine.UI.Image>())
                if (image.enabled) { image.enabled = false; Runtime.Log.LogInfo("Menu borders: hid " + image.name); }
        }
        catch (Exception ex) { _failed = true; Runtime.Log.LogWarning("Menu borders: off for this launch after error: " + ex.Message); }
    }
}
