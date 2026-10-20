using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// ReSharper disable InconsistentNaming

// Regeneración de Eitr por segundo al equipar Dark Necromancer Hood.
// SE_Stats en esta versión de Valheim no expone m_eitrRegen, así que se
// hace mediante un componente en el Player que revisa el equipamiento
// cada segundo y aplica eitr directamente. No afecta a otros items ni
// a la lógica de combate.

namespace ChebsNecromancy.Patches
{
    [HarmonyPatch(typeof(Player), "Awake")]
    internal class Player_EitrRegen_Awake
    {
        static void Postfix(Player __instance)
        {
            // Evitar duplicados si el parche se aplica dos veces
            if (__instance.GetComponent<EitrRegenComponent>() == null)
                __instance.gameObject.AddComponent<EitrRegenComponent>();
        }
    }

    internal class EitrRegenComponent : MonoBehaviour
    {
        private Player player;
        private float timer;
        private FieldInfo equippedItemsField;
        private FieldInfo eitrField;

        private void Awake()
        {
            player = GetComponent<Player>();
            // Cachear campos privados (una sola vez)
            equippedItemsField = typeof(Inventory).GetField(
                "m_equippedItems",
                BindingFlags.NonPublic | BindingFlags.Instance);
            eitrField = typeof(Character).GetField(
                "m_eitr",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void Update()
        {
            if (BasePlugin.EitrRegenBonus.Value <= 0f) return;

            timer += Time.deltaTime;
            if (timer < 1f) return;
            timer = 0f;

            ApplyEitrRegen();
        }

        private void ApplyEitrRegen()
        {
            if (player == null || equippedItemsField == null || eitrField == null) return;

            var inventory = player.GetInventory();
            if (inventory == null) return;

            var equipped = equippedItemsField.GetValue(inventory) as List<ItemDrop.ItemData>;
            if (equipped == null) return;

            bool hasHood = false;

            foreach (var item in equipped)
            {
                if (item == null || item.m_dropPrefab == null) continue;
                var name = item.m_dropPrefab.name;

                if (name.Equals("HelmetMage_Ashlands") || name.Contains("DarkNecromancerHood"))
                    hasHood = true;
            }

            if (!hasHood) return;

            // Aplicar eitr directamente al campo privado m_eitr de Character.
            // Solo el Dark Necromancer Hood otorga este bonus.
            float current = (float)eitrField.GetValue(player);
            eitrField.SetValue(player, current + BasePlugin.EitrRegenBonus.Value);
        }
    }
}
