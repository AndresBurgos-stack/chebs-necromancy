using System.Collections.Generic;
using HarmonyLib;

// ReSharper disable InconsistentNaming

// Console TAB-complete (ServerDevcommands) calls ZNetScene.GetPrefabNames,
// which iterates m_prefabs and reads .name on each entry. Any destroyed
// (Unity fake-null) entry NREs there and spams the console on every TAB.
// The dead entry is left by any mod that registers a prefab and later loses
// its source object; the scan command (chebgonaz_finddeadprefabs) counts
// them. This prefix purges dead entries before the original runs, so TAB
// works regardless of which mod left the corpse. No behavior change beyond
// skipping entries that would have crashed anyway.

namespace ChebsNecromancy.Patches
{
    [HarmonyPatch(typeof(ZNetScene), "GetPrefabNames")]
    class ZNetScene_GetPrefabNames_Patch
    {
        [HarmonyPrefix]
        static void PurgeDeadBeforeNames(ZNetScene __instance)
        {
            if (__instance == null) return;
            try { __instance.m_prefabs.RemoveAll(p => p == null); }
            catch { /* scan command covers reporting */ }
            try
            {
                List<int> dead = null;
                foreach (var kv in __instance.m_namedPrefabs)
                {
                    bool deadEntry = kv.Value == null;
                    if (!deadEntry)
                    {
                        try { var n = kv.Value.name; }
                        catch { deadEntry = true; }
                    }
                    if (deadEntry)
                    {
                        if (dead == null) dead = new List<int>();
                        dead.Add(kv.Key);
                    }
                }
                if (dead != null)
                    foreach (var k in dead) __instance.m_namedPrefabs.Remove(k);
            }
            catch { /* scan command covers reporting */ }
        }
    }
}
