using ChebsNecromancy.Minions.Charred;
using HarmonyLib;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedParameter.Local

// Harmony patching is very sensitive regarding parameter names. Everything in this region should be hand crafted
// and not touched by well-meaning but clueless IDE optimizations.
// eg.
// * __instance MUST be named with exactly two underscores.
// * ___m_drops MUST be named with exactly three underscores.
// * Unused parameters must be left there because they must match the method to override
// * All patch methods need to be static
//
// This is because all of this has a special meaning to Harmony.

namespace ChebsNecromancy.Patches
{
    // Multiplies outgoing damage dealt specifically by a Protector (Gold) Charred
    // Warrior. Deliberately NOT done by editing the weapon's ItemData (m_shared is
    // shared across every instance of that weapon type in the world - editing it
    // would buff every copy, not just this one minion) and NOT done by raising the
    // minion's level/star count (CharredTierThreeQuality already defaults to 3, the
    // display cap - going higher shows no stars at all instead of more). Instead,
    // this intercepts the hit at the moment damage is dealt and scales it only when
    // the attacker is specifically a Protector-armored CharredMinion.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    class CharredProtectorDamagePatch
    {
        [HarmonyPrefix]
        static void Prefix(ref HitData hit)
        {
            if (hit == null) return;

            Character attacker = hit.GetAttacker();
            if (attacker == null) return;
            if (!attacker.TryGetComponent(out CharredMinion attackerMinion)) return;
            if (!attackerMinion.IsProtectorArmor()) return;

            var multiplier = CharredMinion.ProtectorDamageMultiplier.Value;
            if (multiplier == 1f) return;

            hit.m_damage.m_damage *= multiplier;
            hit.m_damage.m_blunt *= multiplier;
            hit.m_damage.m_slash *= multiplier;
            hit.m_damage.m_pierce *= multiplier;
            hit.m_damage.m_chop *= multiplier;
            hit.m_damage.m_pickaxe *= multiplier;
            hit.m_damage.m_fire *= multiplier;
            hit.m_damage.m_frost *= multiplier;
            hit.m_damage.m_lightning *= multiplier;
            hit.m_damage.m_poison *= multiplier;
            hit.m_damage.m_spirit *= multiplier;
        }
    }
}
