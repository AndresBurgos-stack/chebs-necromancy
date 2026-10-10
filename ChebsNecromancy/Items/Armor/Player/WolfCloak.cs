using BepInEx;
using BepInEx.Configuration;
using ChebsValheimLibrary.Items;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsNecromancy.Items.Armor.Player
{
    internal class WolfCloak : Item
    {
        public const string CloakItemName = "ChebGonaz_WolfCloak";
        public override string ItemName => CloakItemName;
        public override string PrefabName => "CapeWolf";
        protected override string DefaultRecipe => "WolfPelt:10,WolfFang:4,TrophyWolf:1";

        public static ConfigEntry<bool> Allowed;

        public static ConfigEntry<CraftingTable> CraftingStationRequired;
        public static ConfigEntry<int> CraftingStationLevel;

        public static ConfigEntry<string> CraftingCost;

        public override void CreateConfigs(BaseUnityPlugin plugin)
        {
            base.CreateConfigs(plugin);

            Allowed = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "WolfCloakAllowed",
                true, new ConfigDescription("Whether crafting a Wolf Cloak is allowed or not.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationRequired = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "WolfCloakCraftingStation",
                CraftingTable.Forge, new ConfigDescription("Crafting station where Wolf Cloak is available", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationLevel = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "WolfCloakCraftingStationLevel",
                2, new ConfigDescription("Crafting station level required to craft Wolf Cloak", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingCost = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "WolfCloakCraftingCosts",
                DefaultRecipe, new ConfigDescription("Materials needed to craft Wolf Cloak. None or Blank will use Default settings.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        public override void UpdateRecipe()
        {
            UpdateRecipe(CraftingStationRequired, CraftingCost, CraftingStationLevel);
        }

        public override CustomItem GetCustomItemFromPrefab(GameObject prefab, bool fixReference = true)
        {
            var config = new ItemConfig();
            config.Name = "$item_chebgonaz_wolfcloak";
            config.Description = "$item_chebgonaz_wolfcloak_desc";

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

            var customItem = new CustomItem(prefab, fixReference, config);
            if (customItem.ItemPrefab == null)
            {
                Logger.LogError($"AddCustomItems: {PrefabName}'s ItemPrefab is null!");
                return null;
            }

            return customItem;
        }
    }
}
