using HarmonyLib;

namespace WodenRallyEdge;

[HarmonyPatch(typeof(GamePadSystem), nameof(GamePadSystem.Update))]
internal static class StockMenuInputHook
{
    private static void Prefix(GamePadSystem __instance) => MenuOwnership.BeforeStock(__instance);
    private static void Postfix(GamePadSystem __instance) => MenuOwnership.AfterStock(__instance);
}
[HarmonyPatch(typeof(GamePadSystem), nameof(GamePadSystem.ReadInputs))]
internal static class StockPadInputHook
{
    private static void Postfix(GamePadSystem.Game_Pad __0) { MenuOwnership.AfterRead(__0); DevInput.AfterPadRead(__0); }
}
