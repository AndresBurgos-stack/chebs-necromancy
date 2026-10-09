using ChebsNecromancy.Items.Armor.Player;
using HarmonyLib;
using UnityEngine;

namespace ChebsNecromancy.Patches
{
    public class WolfStealthPatches
    {
        // Transformed players are nobody's enemy: total stealth until they strike.
        // Striking back reveals them through the engine's own damage response.
        // Fail-open on purpose: any doubt behaves like vanilla.
        // IsEnemy has instance and static overloads: pin the static pair one,
        // otherwise Harmony aborts the whole PatchAll with ambiguous match.
        [HarmonyPatch(typeof(BaseAI))]
        class BaseAIPatchWolfStealth
        {
            [HarmonyPatch(nameof(BaseAI.IsEnemy), new Type[] { typeof(Character), typeof(Character) })]
            [HarmonyPrefix]
            static bool Prefix(Character a, Character b, ref bool __result)
            {
                try
                {
                    var player = a as Player;
                    if (player == null) player = b as Player;
                    if (player == null || !WolfForm.IsTransformed(player)) return true;
                    // Already hunting them? Let vanilla rules retaliate.
                    var other = ReferenceEquals(player, a) ? b : a;
                    var ai = other != null ? other.GetComponent<MonsterAI>() : null;
                    if (ai != null && ai.GetTargetCreature() == player) return true;
                    __result = false;
                    return false;
                }
                catch
                {
                    // fall through to vanilla
                }
                return true;
            }
        }

        // No gear swaps mid-form: new visuals would pop in undisguised.
        // The living check keeps death/tombstone flows intact.
        [HarmonyPatch(typeof(Humanoid))]
        class HumanoidPatchWolfGear
        {
            private static bool GearLocked(Humanoid humanoid)
            {
                try
                {
                    if (humanoid is Player player && !player.IsDead() && WolfForm.IsTransformed(player)) return true;
                }
                catch
                {
                    // fall through: allow
                }
                return false;
            }

            [HarmonyPatch(nameof(Humanoid.EquipItem))]
            [HarmonyPrefix]
            static bool PrefixEquip(Humanoid __instance)
            {
                return !GearLocked(__instance);
            }

            [HarmonyPatch(nameof(Humanoid.UnequipItem))]
            [HarmonyPrefix]
            static bool PrefixUnequip(Humanoid __instance)
            {
                return !GearLocked(__instance);
            }

            [HarmonyPatch(nameof(Humanoid.ToggleEquipped))]
            [HarmonyPrefix]
            static bool PrefixToggle(Humanoid __instance)
            {
                return !GearLocked(__instance);
            }
        }
    }
}
