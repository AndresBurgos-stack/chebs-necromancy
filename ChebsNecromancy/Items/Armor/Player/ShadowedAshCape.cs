using BepInEx;
using BepInEx.Configuration;
using ChebsValheimLibrary.Items;
using Jotunn.Configs;
using Jotunn.Entities;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace ChebsNecromancy.Items.Armor.Player
{
    internal class ShadowedAshCape : Item
    {
        public const string CapeItemName = "ChebGonaz_ShadowedAshCape";
        public const string VanillaPrefabName = "CapeAsh";

        public override string ItemName => CapeItemName;
        public override string PrefabName => VanillaPrefabName;
        protected override string DefaultRecipe => "FlametalNew:10,CelestialFeather:10,CharredBone:5,Eitr:5";

        public static ConfigEntry<bool> Allowed;
        public static ConfigEntry<CraftingTable> CraftingStationRequired;
        public static ConfigEntry<int> CraftingStationLevel;
        public static ConfigEntry<string> CraftingCost;
        public static ConfigEntry<int> NecromancySkillBonus;
        public static ConfigEntry<bool> CustomTint;
        public static ConfigEntry<string> TintColor;

        public override void CreateConfigs(BaseUnityPlugin plugin)
        {
            base.CreateConfigs(plugin);

            Allowed = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "ShadowedAshCapeAllowed",
                true, new ConfigDescription("Whether crafting a Shadowed Ash Cape is allowed or not.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationRequired = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "ShadowedAshCapeCraftingStation",
                CraftingTable.BlackForge, new ConfigDescription("Crafting station where Shadowed Ash Cape is available", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationLevel = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "ShadowedAshCapeCraftingStationLevel",
                3, new ConfigDescription("Crafting station level required to craft Shadowed Ash Cape", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingCost = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "ShadowedAshCapeCraftingCosts",
                DefaultRecipe, new ConfigDescription("Materials needed to craft Shadowed Ash Cape. None or Blank will use Default settings.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NecromancySkillBonus = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "ShadowedAshCapeSkillBonus",
                15, new ConfigDescription("How much wearing the item should raise the Necromancy level (set to 0 to have no set effect at all).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CustomTint = plugin.Config.Bind("ShadowedAshCape (Client)", "ShadowedAshCapeCustomTint",
                true, new ConfigDescription("Tint the cape deep black. Turn off to keep the vanilla look.", null));

            TintColor = plugin.Config.Bind("ShadowedAshCape (Client)", "ShadowedAshCapeTintColor",
                "#141418", new ConfigDescription("HTML color multiplied over the cape materials, eg. #141418 for deep black. Applies when custom tint is on.", null));
        }

        public override void UpdateRecipe()
        {
            UpdateRecipe(CraftingStationRequired, CraftingCost, CraftingStationLevel);
        }

        public override CustomItem GetCustomItemFromPrefab(GameObject prefab, bool fixReference = true)
        {
            ItemConfig config = new ItemConfig();
            config.Name = "$item_chebgonaz_shadowcape";
            config.Description = "$item_chebgonaz_shadowcape_desc";

            if (Allowed.Value)
            {
                if (string.IsNullOrEmpty(CraftingCost.Value))
                {
                    CraftingCost.Value = DefaultRecipe;
                }
                SetRecipeReqs(
                    config,
                    CraftingCost,
                    CraftingStationRequired,
                    CraftingStationLevel
                );
            }
            else
            {
                config.Enabled = false;
            }

            var customItem = new CustomItem(prefab, true, config);
            if (customItem.ItemPrefab == null)
            {
                Logger.LogError($"AddCustomItems: {PrefabName}'s ItemPrefab is null!");
                return null;
            }
            customItem.ItemDrop.m_itemData.m_shared.m_setStatusEffect =
                NecromancySkillBonus.Value > 0 ?
                BasePlugin.SetEffectNecromancyArmor4 : null;
            customItem.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                NecromancySkillBonus.Value > 0 ?
                BasePlugin.SetEffectNecromancyArmor4 : null;

            var shared = customItem.ItemDrop.m_itemData.m_shared;
            shared.m_armor = 12f;
            var mods = shared.m_damageModifiers;
            var frostIdx = mods.FindIndex(mod => mod.m_type == HitData.DamageType.Frost);
            if (frostIdx >= 0)
            {
                var frost = mods[frostIdx];
                frost.m_modifier = HitData.DamageModifier.Resistant;
                mods[frostIdx] = frost;
            }
            else
            {
                mods.Add(new HitData.DamageModPair
                {
                    m_type = HitData.DamageType.Frost,
                    m_modifier = HitData.DamageModifier.Resistant
                });
            }

            if (CustomTint.Value) DarkNecromancerHood.ApplyDarkTint(prefab, ResolveTintColor());
            DarkNecromancerHood.ApplyDarkIcon(prefab, ResolveTintColor());

            return customItem;
        }

        public static SE_Stats CreateSetEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "ChebGonaz_ShadowCapeNecromancy";
            se.m_name = "$se_chebgonaz_shadowcape";
            se.m_tooltip = "$se_chebgonaz_shadowcape_desc";
            return se;
        }

        private static string _tintHtmlCache;
        private static Color _tintColorCache = new Color(0.08f, 0.08f, 0.09f);

        public static Color ResolveTintColor()
        {
            if (TintColor.Value != _tintHtmlCache)
            {
                _tintHtmlCache = TintColor.Value;
                var html = (_tintHtmlCache ?? "").Trim();
                if (!html.StartsWith("#")) html = "#" + html;
                if (!ColorUtility.TryParseHtmlString(html, out _tintColorCache))
                {
                    Logger.LogWarning($"ShadowedAshCape: malformed tint '{_tintHtmlCache}', keeping previous.");
                }
            }
            return _tintColorCache;
        }
    }
}
