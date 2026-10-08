using BepInEx;
using BepInEx.Configuration;
using System.IO;
using ChebsNecromancy.Minions;
using ChebsNecromancy.Minions.Charred;
using ChebsValheimLibrary.Items;
using ChebsValheimLibrary.Minions;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsNecromancy.Items.Wands
{
    internal class CharredWand : Wand
    {
        #region ConfigEntries
        public static ConfigEntry<CraftingTable> CraftingStationRequired;
        public static ConfigEntry<int> CraftingStationLevel;

        public static ConfigEntry<string> CraftingCost;

        public static ConfigEntry<bool> CharredAllowed;

        public static ConfigEntry<float> CharredSetFollowRange;
        #endregion

        public override string ItemName => "ChebGonaz_CharredWand";
        // Unused at load: the model comes from the vanilla StaffRedTroll prefab,
        // cloned in BasePlugin. Kept to satisfy the Wand contract.
        public override string PrefabName => "StaffRedTroll";
        protected override string DefaultRecipe => "CharredBone:30,FlametalNew:10";

        #region MinionSelector
        public enum MinionOption
        {
            Warrior,
            Archer,
            Twitcher
        }

        private List<MinionOption> _minionOptions = new()
        {
            MinionOption.Warrior,
            MinionOption.Archer,
            MinionOption.Twitcher
        };

        private int _selectedMinionOptionIndex;
        private MinionOption SelectedMinionOption => _minionOptions[_selectedMinionOptionIndex];

        private TextMeshProUGUI _createMinionButtonText;

        #endregion

        private static string LocalizeMinionOption(MinionOption minionOption)
        {
            var key = minionOption switch
            {
                MinionOption.Archer => "$chebgonaz_miniontype_charred_archer",
                MinionOption.Warrior => "$chebgonaz_miniontype_charred_warrior",
                MinionOption.Twitcher => "$chebgonaz_miniontype_charred_twitcher",
                _ => "Error"
            };
            // Never show a raw $key in the UI: fall back to English when the
            // translations don't have it (stale/missing Translations folder).
            var localized = BasePlugin.Localization.TryTranslate(key);
            if (string.IsNullOrEmpty(localized) || localized.StartsWith("$"))
            {
                localized = minionOption switch
                {
                    MinionOption.Archer => "Charred Archer",
                    MinionOption.Warrior => "Charred Warrior",
                    MinionOption.Twitcher => "Charred Twitcher",
                    _ => "Error"
                };
            }
            return localized;
        }

        public override void CreateConfigs(BaseUnityPlugin plugin)
        {
            base.CreateConfigs(plugin);

            var serverSynced = $"{GetType().Name} (Server Synced)";
            var clientSynced = $"{GetType().Name} (Client)";

            CharredSetFollowRange = plugin.Config.Bind(clientSynced, "CharredCommandRange",
                20f, new ConfigDescription("The range from which nearby Charred will hear your command.", null));

            Allowed = plugin.Config.Bind(serverSynced, "CharredWandAllowed",
                true, new ConfigDescription("Whether crafting a Charred Wand is allowed or not.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationRequired = plugin.Config.Bind(serverSynced, "CharredWandCraftingStation",
                CraftingTable.Forge, new ConfigDescription("Crafting station where Charred Wand is available", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingStationLevel = plugin.Config.Bind(serverSynced, "CharredWandCraftingStationLevel",
                1, new ConfigDescription("Crafting station level required to craft Charred Wand", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CraftingCost = plugin.Config.Bind(serverSynced, "CharredWandCraftingCosts",
                DefaultRecipe, new ConfigDescription(
                    "Materials needed to craft Charred Wand. None or Blank will use Default settings.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CharredAllowed = plugin.Config.Bind(serverSynced, "CharredAllowed",
                true, new ConfigDescription("If false, charred aren't loaded at all and can't be summoned.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        public override void UpdateRecipe()
        {
            UpdateRecipe(CraftingStationRequired, CraftingCost, CraftingStationLevel);
        }

        public static void ApplyBlueCustomization(GameObject wandPrefab)
        {

            var iconPath = Path.Combine(Path.GetDirectoryName(typeof(CharredWand).Assembly.Location),
                "Assets", "charredwand_icon_blue.png");
            if (File.Exists(iconPath))
            {
                // Decoded by hand: Unity's PNG helpers live in ImageConversionModule,
                // which this net48 project cannot reference (netstandard conflict).
                var tex = LoadPng(iconPath);
                if (tex != null)
                {
                    wandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[]
                    {
                        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                    };
                }
            }
            else
            {
                Logger.LogWarning("Charred Wand: blue icon file missing, keeping vanilla icon.");
            }

            var tint = new Color(0.45f, 0.65f, 1f);
            foreach (var renderer in wandPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat != null && mat.HasProperty("_Color")) mat.color = tint;
                }
            }
        }

        public static Texture2D LoadPng(string path)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                if (data.Length < 33 || data[0] != 137 || data[1] != 80 || data[2] != 78) return null;

                var pos = 8;
                var width = 0;
                var height = 0;
                var idat = new List<byte>();
                while (pos + 8 <= data.Length)
                {
                    var length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                    var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                    if (type == "IHDR")
                    {
                        width = (data[pos + 8] << 24) | (data[pos + 9] << 16) | (data[pos + 10] << 8) | data[pos + 11];
                        height = (data[pos + 12] << 24) | (data[pos + 13] << 16) | (data[pos + 14] << 8) | data[pos + 15];
                        if (data[pos + 16] != 8 || data[pos + 17] != 6 || data[pos + 20] != 0) return null;
                    }
                    else if (type == "IDAT")
                    {
                        for (var i = 0; i < length; i++) idat.Add(data[pos + 8 + i]);
                    }
                    else if (type == "IEND")
                    {
                        break;
                    }
                    pos += 12 + length;
                }
                if (width <= 0 || height <= 0 || idat.Count == 0) return null;

                byte[] raw;
                using (var input = new MemoryStream(idat.ToArray(), 2, idat.Count - 6))
                using (var deflate = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    raw = output.ToArray();
                }

                const int channels = 4;
                var stride = width * channels;
                var pixels = new Color32[width * height];
                var prev = new byte[stride];
                var line = new byte[stride];
                var offset = 0;
                for (var y = 0; y < height; y++)
                {
                    var filter = raw[offset++];
                    System.Buffer.BlockCopy(raw, offset, line, 0, stride);
                    offset += stride;
                    for (var i = 0; i < stride; i++)
                    {
                        var a = i >= channels ? line[i - channels] : (byte)0;
                        var b = prev[i];
                        var c = i >= channels ? prev[i - channels] : (byte)0;
                        switch (filter)
                        {
                            case 1: line[i] = (byte)(line[i] + a); break;
                            case 2: line[i] = (byte)(line[i] + b); break;
                            case 3: line[i] = (byte)(line[i] + (a + b) / 2); break;
                            case 4:
                                var p = a + b - c;
                                var pa = System.Math.Abs(p - a);
                                var pb = System.Math.Abs(p - b);
                                var pc = System.Math.Abs(p - c);
                                line[i] = (byte)(line[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
                                break;
                        }
                    }
                    System.Buffer.BlockCopy(line, 0, prev, 0, stride);
                    for (var x = 0; x < width; x++)
                    {
                        pixels[(height - 1 - y) * width + x] = new Color32(
                            line[x * channels], line[x * channels + 1], line[x * channels + 2], line[x * channels + 3]);
                    }
                }

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();
                return tex;
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Charred Wand: icon decode failed ({ex.Message}).");
                return null;
            }
        }
        public override CustomItem GetCustomItemFromPrefab(GameObject prefab, bool fixReference = true)
        {
            var config = new ItemConfig();
            config.Name = "$item_chebgonaz_charredwand";
            config.Description = "$item_chebgonaz_charredwand_desc";

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

            customItem.ItemDrop.m_itemData.m_shared.m_setStatusEffect = BasePlugin.SetEffectNecromancyArmor;

            return customItem;
        }

        public override KeyHintConfig GetKeyHint()
        {
            var buttonConfigs = new List<ButtonConfig>();

            if (CreateMinionButton != null) buttonConfigs.Add(CreateMinionButton);
            if (NextMinionButton != null) buttonConfigs.Add(NextMinionButton);
            if (FollowButton != null) buttonConfigs.Add(FollowButton);
            if (WaitButton != null) buttonConfigs.Add(WaitButton);
            if (TeleportButton != null) buttonConfigs.Add(TeleportButton);

            return new KeyHintConfig
            {
                Item = ItemName,
                ButtonConfigs = buttonConfigs.ToArray()
            };
        }

        public override bool HandleInputs()
        {
            if (MessageHud.instance != null
                && Player.m_localPlayer != null
                && Player.m_localPlayer.GetInventory().GetEquippedItems().Find(
                    equippedItem => equippedItem.TokenName().Equals("$item_chebgonaz_charredwand")
                ) != null
               )
            {
                ExtraResourceConsumptionUnlocked =
                    UnlockExtraResourceConsumptionButton == null
                    || ZInput.GetButton(UnlockExtraResourceConsumptionButton.Name);

                if (CreateMinionButton != null)
                {
                    // https://github.com/Valheim-Modding/Jotunn/issues/398
                    if (_createMinionButtonText == null)
                    {
                        var button = GameObject.Find(CreateMinionButton.Name);
                        if (button != null)
                        {
                            _createMinionButtonText = button.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }

                    if (_createMinionButtonText != null)
                    {
                        var createLocalized = BasePlugin.Localization.TryTranslate("$chebgonaz_wand_create");
                        var minionLocalized = LocalizeMinionOption(SelectedMinionOption);
                        _createMinionButtonText.text = $"{createLocalized} {minionLocalized}";
                    }

                    if (ZInput.GetButton(CreateMinionButton.Name))
                    {
                        SpawnMinion(SelectedMinionOption);
                        return true;
                    }
                }

                if (NextMinionButton != null && ZInput.GetButton(NextMinionButton.Name))
                {
                    _selectedMinionOptionIndex++;
                    if (_selectedMinionOptionIndex >= _minionOptions.Count) _selectedMinionOptionIndex = 0;
                    if (_createMinionButtonText != null)
                    {
                        var createLocalized = BasePlugin.Localization.TryTranslate("$chebgonaz_wand_create");
                        var minionLocalized = LocalizeMinionOption(SelectedMinionOption);
                        _createMinionButtonText.text = $"{createLocalized} {minionLocalized}";
                    }
                    return true;
                }

                if (FollowButton != null && ZInput.GetButton(FollowButton.Name))
                {
                    MakeNearbyMinionsFollow(CharredSetFollowRange.Value, true);
                    return true;
                }
                if (WaitButton != null && ZInput.GetButton(WaitButton.Name))
                {
                    if (ExtraResourceConsumptionUnlocked)
                    {
                        MakeNearbyMinionsRoam(CharredSetFollowRange.Value);
                    }
                    else
                    {
                        MakeNearbyMinionsFollow(CharredSetFollowRange.Value, false);
                    }

                    return true;
                }
                if (TeleportButton != null && ZInput.GetButton(TeleportButton.Name))
                {
                    TeleportFollowingMinionsToPlayer();
                    return true;
                }
            }

            return false;
        }

        private void SpawnMinion(MinionOption minionOption)
        {
            var playerNecromancyLevel =
                Player.m_localPlayer.GetSkillLevel(SkillManager.Instance.GetSkill(BasePlugin.NecromancySkillIdentifier).m_skill);

            var inventory = Player.m_localPlayer.GetInventory();
            var charredType = minionOption switch
            {
                MinionOption.Archer => CharredMinion.CharredType.Archer,
                MinionOption.Twitcher => CharredMinion.CharredType.Twitcher,
                _ => CharredMinion.CharredType.Warrior
            };

            // Twitchers never wear armor: always unarmored, Shift or not.
            var armored = charredType != CharredMinion.CharredType.Twitcher
                && ExtraResourceConsumptionUnlocked
                && UndeadMinion.CanSpawn(CharredMinion.ArmorUpgradeCost, inventory, out _);

            SpawnCharred(playerNecromancyLevel, charredType, armored);
        }

        private void SpawnCharred(float playerNecromancyLevel, CharredMinion.CharredType charredType, bool armored)
        {
            if (!CharredAllowed.Value) return;

            var minionLimitIsSet = CharredMinion.MaxCharred.Value > 0;
            if (minionLimitIsSet)
            {
                UndeadMinion.CountActive<CharredMinion>(
                    CharredMinion.MinionLimitIncrementsEveryXLevels.Value,
                    CharredMinion.MaxCharred.Value);
            }

            var inventory = Player.m_localPlayer.GetInventory();
            var itemsCost = charredType switch
            {
                CharredMinion.CharredType.Archer => CharredArcherMinion.ItemsCost,
                CharredMinion.CharredType.Twitcher => CharredTwitcherMinion.ItemsCost,
                _ => CharredWarriorMinion.ItemsCost
            };

            if (!UndeadMinion.CanSpawn(itemsCost, inventory, out var message))
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
                return;
            }

            int quality = CharredMinion.TierOneQuality.Value;
            if (playerNecromancyLevel >= CharredMinion.TierThreeLevelReq.Value)
            {
                quality = CharredMinion.TierThreeQuality.Value;
            }
            else if (playerNecromancyLevel >= CharredMinion.TierTwoLevelReq.Value)
            {
                quality = CharredMinion.TierTwoQuality.Value;
            }

            CharredMinion.ConsumeResources(charredType, armored);

            CharredMinion.InstantiateCharred(quality, playerNecromancyLevel, charredType, armored);
        }
    }
}
