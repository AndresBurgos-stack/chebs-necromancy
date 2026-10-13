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
    internal class DarkNecromancerHood : Item
    {
        public const string HoodItemName = "ChebGonaz_DarkNecromancerHood";
        public const string VanillaPrefabName = "HelmetMage_Ashlands";

        public override string ItemName => HoodItemName;
        public override string PrefabName => VanillaPrefabName;
        protected override string DefaultRecipe => "FlametalNew:16,Eitr:4,AskHide:3,CharredBone:2";

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

            Allowed = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "DarkNecromancerHoodAllowed",
                true, new ConfigDescription("Whether crafting a Dark Necromancer Hood is allowed or not.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationRequired = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "DarkNecromancerHoodCraftingStation",
                CraftingTable.BlackForge, new ConfigDescription("Crafting station where Dark Necromancer Hood is available", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationLevel = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "DarkNecromancerHoodCraftingStationLevel",
                3, new ConfigDescription("Crafting station level required to craft Dark Necromancer Hood", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingCost = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "DarkNecromancerHoodCraftingCosts",
                DefaultRecipe, new ConfigDescription("Materials needed to craft Dark Necromancer Hood. None or Blank will use Default settings.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NecromancySkillBonus = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "DarkNecromancerHoodSkillBonus",
                15, new ConfigDescription("How much wearing the item should raise the Necromancy level (set to 0 to have no set effect at all).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CustomTint = plugin.Config.Bind("DarkNecromancerHood (Client)", "DarkNecromancerHoodCustomTint",
                true, new ConfigDescription("Tint the hood dark. Turn off to keep the vanilla look.", null));

            TintColor = plugin.Config.Bind("DarkNecromancerHood (Client)", "DarkNecromancerHoodTintColor",
                "#2A2A30", new ConfigDescription("HTML color multiplied over the hood materials, eg. #2A2A30 for somber black. Applies when custom tint is on.", null));
        }

        public override void UpdateRecipe()
        {
            UpdateRecipe(CraftingStationRequired, CraftingCost, CraftingStationLevel);
        }

        public override CustomItem GetCustomItemFromPrefab(GameObject prefab, bool fixReference = true)
        {
            ItemConfig config = new ItemConfig();
            config.Name = "$item_chebgonaz_darkhood";
            config.Description = "$item_chebgonaz_darkhood_desc";

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
                BasePlugin.SetEffectNecromancyArmor3 : null;
            customItem.ItemDrop.m_itemData.m_shared.m_equipStatusEffect =
                NecromancySkillBonus.Value > 0 ?
                BasePlugin.SetEffectNecromancyArmor3 : null;

            var shared = customItem.ItemDrop.m_itemData.m_shared;
            shared.m_armor = 38f;
            shared.m_movementModifier = -0.02f;

            if (CustomTint.Value) ApplyDarkTint(prefab, ResolveTintColor());
            ApplyDarkIcon(prefab, ResolveTintColor());

            return customItem;
        }

        public static SE_Stats CreateSetEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "ChebGonaz_DarkHoodNecromancy";
            se.m_name = "$se_chebgonaz_darkhood";
            se.m_tooltip = "$se_chebgonaz_darkhood_desc";
            return se;
        }

        private static string _tintHtmlCache;
        private static Color _tintColorCache = new Color(0.16f, 0.16f, 0.19f);

        public static Color ResolveTintColor()
        {
            if (TintColor.Value != _tintHtmlCache)
            {
                _tintHtmlCache = TintColor.Value;
                var html = (_tintHtmlCache ?? "").Trim();
                if (!html.StartsWith("#")) html = "#" + html;
                if (!ColorUtility.TryParseHtmlString(html, out _tintColorCache))
                {
                    Logger.LogWarning($"DarkNecromancerHood: malformed tint '{_tintHtmlCache}', keeping previous.");
                }
            }
            return _tintColorCache;
        }

        public static void ApplyDarkTint(GameObject prefab, Color tint)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                TintRenderer(renderer, tint);
            }
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                TintRenderer(renderer, tint);
            }
        }

        private static void TintRenderer(Renderer renderer, Color tint)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat != null && mat.HasProperty("_Color")) mat.color = tint;
            }
        }

        public static void ApplyDarkIcon(GameObject prefab, Color tint)
        {
            try
            {
                var itemDrop = prefab.GetComponent<ItemDrop>();
                var icons = itemDrop?.m_itemData?.m_shared?.m_icons;
                if (icons == null || icons.Length == 0 || icons[0] == null) return;
                var dark = CreateDarkIcon(icons[0], tint);
                if (dark != null) itemDrop.m_itemData.m_shared.m_icons = new[] { dark };
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"DarkNecromancerHood: icon tint failed ({ex.GetType().Name}), keeping vanilla icon.");
            }
        }

        public static Sprite CreateDarkIcon(Sprite source, Color tint)
        {
            var src = source.texture;
            var rect = source.textureRect;
            var rt = RenderTexture.GetTemporary((int)rect.width, (int)rect.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt, new Vector2(rect.width / src.width, rect.height / src.height), new Vector2(rect.x / src.width, rect.y / src.height));
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rect.width, rect.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color(p.r * tint.r, p.g * tint.g, p.b * tint.b, p.a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}
