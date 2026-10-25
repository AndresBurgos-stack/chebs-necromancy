using System.Reflection;
using ChebsNecromancy.Items.Armor.Player;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace ChebsNecromancy.Patches
{
    [HarmonyPatch(typeof(Player), "Awake")]
    internal class Player_EitrRegen_Awake
    {
        static void Postfix(Player __instance)
        {
            if (__instance.GetComponent<EitrRegenComponent>() == null)
                __instance.gameObject.AddComponent<EitrRegenComponent>();
        }
    }

    internal class EitrRegenComponent : MonoBehaviour
    {
        private static FieldInfo _eitrField;

        private Player _player;
        private float _timer;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _eitrField ??= typeof(Player).GetField("m_eitr",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void Update()
        {
            if (_player == null || BasePlugin.EitrRegenBonus == null) return;
            if (BasePlugin.EitrRegenBonus.Value <= 0f) return;

            _timer += Time.deltaTime;
            if (_timer < 1f) return;
            _timer = 0f;

            ApplyEitrRegen();
        }

        private void ApplyEitrRegen()
        {
            if (_eitrField == null) return;

            var inventory = _player.GetInventory();
            if (inventory == null) return;

            var hasHood = false;
            foreach (var item in inventory.GetEquippedItems())
            {
                if (item?.m_dropPrefab == null) continue;
                if (item.m_dropPrefab.name.Contains(DarkNecromancerHood.HoodItemName))
                {
                    hasHood = true;
                    break;
                }
            }
            if (!hasHood) return;

            var current = (float)_eitrField.GetValue(_player);
            var maxEitr = _player.GetMaxEitr();
            if (current >= maxEitr) return;
            _eitrField.SetValue(_player, Mathf.Min(maxEitr, current + BasePlugin.EitrRegenBonus.Value));
        }
    }
}
