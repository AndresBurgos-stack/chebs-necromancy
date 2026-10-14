using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ChebsNecromancy.Commands
{
    // Diagnostic: scans prefab registries for destroyed entries (Unity
    // fake-nulls whose .name access throws). Points at the culprit behind
    // console TAB-complete NREs instead of guessing which mod left it.
    public class FindDeadPrefabs : ConsoleCommand
    {
        public override string Name => "chebgonaz_finddeadprefabs";

        public override string Help => "Scans prefab registries for destroyed entries behind console TAB-complete NREs. Add 'purge' to remove them.";

        public override void Run(string[] args)
        {
            bool purge = args.Length > 0 && args[0].Equals("purge", System.StringComparison.OrdinalIgnoreCase);
            int dead = 0, alive = 0, purged = 0;
            var scene = ZNetScene.instance;
            if (scene != null)
            {
                // Unity fake-null: destroyed refs compare == null but .name
                // throws (exactly the TAB-complete NRE). Check == null first
                // so the scan itself never throws.
                foreach (var prefab in scene.m_prefabs)
                {
                    if (prefab == null) dead++;
                    else
                    {
                        try { var n = prefab.name; alive++; }
                        catch { dead++; }
                    }
                }
                if (purge)
                {
                    int before = scene.m_prefabs.Count;
                    scene.m_prefabs.RemoveAll(p => p == null);
                    purged += before - scene.m_prefabs.Count;
                }
                var deadKeys = new System.Collections.Generic.List<int>();
                foreach (var kv in scene.m_namedPrefabs)
                {
                    if (kv.Value == null) { dead++; deadKeys.Add(kv.Key); }
                    else
                    {
                        try { var n = kv.Value.name; alive++; }
                        catch { dead++; deadKeys.Add(kv.Key); }
                    }
                }
                if (purge)
                {
                    foreach (var k in deadKeys)
                        if (scene.m_namedPrefabs.Remove(k)) purged++;
                }
            }
            var objectDB = ObjectDB.instance;
            if (objectDB != null)
            {
                var deadItemKeys = new System.Collections.Generic.List<int>();
                foreach (var kv in objectDB.m_itemByHash)
                {
                    if (kv.Value == null) { dead++; deadItemKeys.Add(kv.Key); }
                    else
                    {
                        try { var n = kv.Value.name; alive++; }
                        catch { dead++; deadItemKeys.Add(kv.Key); }
                    }
                }
                if (purge)
                {
                    foreach (var k in deadItemKeys)
                        if (objectDB.m_itemByHash.Remove(k)) purged++;
                }
            }
            Jotunn.Logger.LogInfo($"[DeadPrefab] scan done: {alive} alive, {dead} destroyed{(purge ? $", {purged} purged" : " (run with 'purge' to remove them)")}.");
        }
    }
}
