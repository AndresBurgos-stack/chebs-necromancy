using System.Collections.Generic;
using System.Linq;
using ChebsNecromancy.Items.Armor.Player;
using ChebsNecromancy.Minions;
using HarmonyLib;
using UnityEngine;
using Logger = Jotunn.Logger;

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
    public class PlayerPatches
    {
        [HarmonyPatch(typeof(Player))]
        class PlayerPatch1
        {
            [HarmonyPatch(nameof(Player.PlayerAttackInput))]
            [HarmonyPostfix]
            static void Postfix(float dt, Player __instance)
            {
                // if attacking with a wand, destroy the minion if you own it
                if (!__instance.m_attack) return;
                var friendlySkeletonWands = new List<string> { "$item_friendlyskeletonwand", "$item_friendlyskeletonwand_draugrwand", "$item_chebgonaz_charredwand" };
                var undeadMinion = Physics.OverlapSphere(__instance.transform.position, 2)
                    .Select(collider => collider.GetComponentInParent<UndeadMinion>())
                    .Where(undead => undead != null)
                    .Where(undead => friendlySkeletonWands.Contains(__instance.GetCurrentWeapon()?.m_shared?.m_name))
                    .Where(undead => undead.BelongsToPlayer(__instance.GetPlayerName()))
                    .OrderBy(undead => Vector3.Distance(undead.transform.position, __instance.transform.position))
                    .FirstOrDefault();

                if (undeadMinion) undeadMinion.Kill();
            }
        }

        [HarmonyPatch(typeof(Player))]
        class PlayerPatchWolfGate
        {
            // PlayerAttackInput runs every physics frame: only react to a real attack.
            // Wolf form bites instead of swinging: the human attack is suppressed
            // and striking back alerts enemies through the engine itself.
            [HarmonyPatch(nameof(Player.PlayerAttackInput))]
            [HarmonyPrefix]
            static bool PrefixAttack(Player __instance)
            {
                if (!__instance.m_attack) return true;
                if (WolfForm.IsTransformed(__instance))
                {
                    WolfForm.TryWolfAttack(__instance);
                    return false;
                }
                return true;
            }

            // Dodge is suppressed in wolf form (only K or cooldown exits it).
            [HarmonyPatch(nameof(Player.Dodge))]
            [HarmonyPrefix]
            static bool PrefixDodge(Player __instance)
            {
                if (WolfForm.IsTransformed(__instance)) return false;
                return true;
            }

            // Raw meat is not ItemType.Consumable, so Humanoid.UseItem never
            // routes it to ConsumeItem (it falls into tame-targeting instead).
            // Transmute it at the UseItem gate: covers hotbar and inventory.
            [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem),
                new Type[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool) })]
            [HarmonyPrefix]
            static bool PrefixUseItem(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
            {
                var player = __instance as Player;
                if (player == null
                    || !WolfForm.IsTransformed(player)
                    || !WolfForm.IsBonusMeat(item?.m_dropPrefab?.name, item?.m_shared?.m_name)) return true;
                var inv = inventory ?? player.GetInventory();
                if (inv == null || !inv.RemoveOneItem(item)) return true;
                var cooked = WolfForm.GetCookedCounterpart(item);
                if (cooked != null && cooked.gameObject != null && inv.AddItem(cooked.gameObject, 1))
                {
                    var cookedName = cooked.m_itemData?.m_shared?.m_name;
                    ItemDrop.ItemData cookedData = null;
                    foreach (var entry in inv.GetAllItems())
                    {
                        if (entry != null && entry.m_shared?.m_name == cookedName) { cookedData = entry; break; }
                    }
                    if (cookedData != null) player.EatFood(cookedData);
                    Logger.LogWarning($"WolfForm: devoured raw '{item.m_dropPrefab?.name}' as '{cooked.gameObject.name}'.");
                    return false;
                }
                player.Heal(WolfForm.MeatBonusHP.Value);
                player.AddStamina(WolfForm.MeatBonusStamina.Value);
                Logger.LogWarning($"WolfForm: devoured raw '{item.m_dropPrefab?.name}' (+{WolfForm.MeatBonusHP.Value} HP).");
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                    $"{BasePlugin.Localization.TryTranslate("$chebgonaz_wolfform_devoured")} (+{WolfForm.MeatBonusHP.Value} HP, +{WolfForm.MeatBonusStamina.Value} stamina)");
                return false;
            }

            // Hotbar and inventory eating both funnel through ConsumeItem:
            // raw meat never reaches EatFood (it falls into tame-targeting),
            // so transmute it here instead.
            [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem),
                new Type[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool) })]
            [HarmonyPrefix]
            static bool PrefixConsume(Player __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (!WolfForm.IsTransformed(__instance)
                    || !WolfForm.IsBonusMeat(item?.m_dropPrefab?.name, item?.m_shared?.m_name)) return true;
                if (inventory == null || !inventory.RemoveOneItem(item)) return true;
                __instance.Heal(WolfForm.MeatBonusHP.Value);
                __instance.AddStamina(WolfForm.MeatBonusStamina.Value);
                Logger.LogWarning($"WolfForm: devoured raw '{item.m_dropPrefab?.name}' (+{WolfForm.MeatBonusHP.Value} HP).");
                __result = true;
                return false;
            }
            // allow it, then consume + bonus manually instead of vanilla food.
            [HarmonyPatch(nameof(Player.CanEat))]
            [HarmonyPrefix]
            static bool PrefixCanEat(Player __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (WolfForm.IsTransformed(__instance)
                    && WolfForm.IsBonusMeat(item?.m_dropPrefab?.name, item?.m_shared?.m_name))
                {
                    __result = true;
                    return false;
                }
                return true;
            }

            [HarmonyPatch(nameof(Player.EatFood))]
            [HarmonyPrefix]
            static bool PrefixEatFood(Player __instance, ItemDrop.ItemData item)
            {
                if (!WolfForm.IsTransformed(__instance)
                    || !WolfForm.IsBonusMeat(item?.m_dropPrefab?.name, item?.m_shared?.m_name))
                {
                    return true;
                }
                __instance.GetInventory().RemoveItem(item.m_shared.m_name, 1);
                __instance.Heal(WolfForm.MeatBonusHP.Value);
                __instance.AddStamina(WolfForm.MeatBonusStamina.Value);
                Logger.LogWarning($"WolfForm: devoured raw '{item.m_dropPrefab?.name}' (+{WolfForm.MeatBonusHP.Value} HP).");
                return false;
            }
        }
    }
}