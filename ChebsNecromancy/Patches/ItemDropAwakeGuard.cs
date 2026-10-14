using HarmonyLib;

// ReSharper disable InconsistentNaming

// Grafting ItemDrop onto fresh runtime templates NREs once per template:
// AddComponent runs Awake synchronously, before m_itemData is assigned, and
// the fresh component carries an empty data (m_shared == null), so vanilla
// Awake -> SetQuality -> GetScale reads a null shared. The graft assigns
// valid iron data immediately after (see "grafted onto X" lines), which is
// why everything works despite the log spam. This prefix skips vanilla Awake
// ONLY in that invalid state (which today can only throw); every valid flow
// runs untouched. No behavior change beyond silencing the cosmetic NRE.

namespace ChebsNecromancy.Patches
{
    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    class ItemDrop_Awake_Guard
    {
        [HarmonyPrefix]
        static bool SkipWhenNoShared(ItemDrop __instance)
        {
            try
            {
                if (__instance == null) return true;
                var data = __instance.m_itemData;
                if (data == null || data.m_shared == null) return false;
            }
            catch { return true; }
            return true;
        }
    }
}
