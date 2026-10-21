using System.Collections;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ChebsNecromancy.Commands;
using ChebsNecromancy.Commands.Appearance;
using ChebsNecromancy.CustomPrefabs;
using ChebsNecromancy.Items.Armor.Minions;
using ChebsNecromancy.Items.Armor.Player;
using ChebsNecromancy.Items.Wands;
using ChebsNecromancy.Items.Weapons.Minions;
using ChebsNecromancy.Minions;
using ChebsNecromancy.Minions.Charred;
using ChebsNecromancy.Minions.Draugr;
using ChebsNecromancy.Minions.Skeletons;
using ChebsNecromancy.Options;
using ChebsNecromancy.PvPOptions;
using ChebsNecromancy.Structures;
using ChebsValheimLibrary;
using ChebsValheimLibrary.Common;
using ChebsValheimLibrary.Items;
using ChebsValheimLibrary.PvP;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Paths = BepInEx.Paths;

namespace ChebsNecromancy
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid)]
    // Add a soft dependency to Cheb's Mercenaries to cause Cheb's Mercenaries to load first. This resolves issues
    // that arise from jumbled mod load orders: https://github.com/jpw1991/chebs-mercenaries/issues/53
    [BepInDependency("com.chebgonaz.chebsmercenaries", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    //[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    internal class BasePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.chebgonaz.ChebsNecromancy";
        public const string PluginName = "Chebs Necromancy Enhanced";
        public const string PluginVersion = "1.3.2";
        private const string ConfigFileName = PluginGuid + ".cfg";
        private static readonly string ConfigFileFullPath = Path.Combine(Paths.ConfigPath, ConfigFileName);

        public readonly System.Version ChebsValheimLibraryVersion = new("2.7.0");

        private readonly Harmony harmony = new(PluginGuid);
        
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private readonly List<Wand> wands = new()
        {
            new SkeletonWand(),
            new DraugrWand(),
            new CharredWand(),
            new OrbOfBeckoning()
        };

        public const string NecromancySkillIdentifier = "friendlyskeletonwand_necromancy_skill";

        private readonly SpectralShroud spectralShroudItem = new();
        private readonly NecromancerHood necromancersHoodItem = new();
        private readonly NecromancerCape necromancerCapeItem = new();
        private readonly DarkNecromancerHood darkHoodItem = new();
        private readonly ShadowedAshCape shadowCapeItem = new();

        private Material spectralShroudMaterial;
        private Sprite spectralShroudSprite;

        private float inputDelay = 0;

        public static SE_Stats SetEffectNecromancyArmor, SetEffectNecromancyArmor2;
        public static SE_Stats SetEffectNecromancyArmor3, SetEffectNecromancyArmor4;

        // Global Config Acceptable Values
        public AcceptableValueList<bool> BoolValue = new(true, false);
        public AcceptableValueRange<float> FloatQuantityValue = new(1f, 1000f);
        public AcceptableValueRange<int> IntQuantityValue = new(1, 1000);
        
        public static ConfigEntry<bool> HeavyLogging;
        
        public static ConfigEntry<bool> PvPAllowed;

        // if set to true, the particle effects that for some reason hurt radeon are dynamically disabled
        public static ConfigEntry<bool> RadeonFriendly;
        // so that players may disable smoke on wraiths, if they wish (feature request)
        public static ConfigEntry<bool> NoWraithSmoke;

        public static ConfigEntry<int> BoneFragmentsDroppedAmountMin;
        public static ConfigEntry<int> BoneFragmentsDroppedAmountMax;
        public static ConfigEntry<float> BoneFragmentsDroppedChance;

        public static ConfigEntry<int> ArmorLeatherScrapsRequiredConfig;
        public static ConfigEntry<int> ArmorBronzeRequiredConfig;
        public static ConfigEntry<int> ArmorIronRequiredConfig;
        public static ConfigEntry<int> ArmorBlackIronRequiredConfig;
        public static ConfigEntry<int> ArmorCarapaceRequiredConfig;
        public static ConfigEntry<string> ArmorCarapaceHelmetPrefabConfig;
        public static ConfigEntry<float> ArmorCarapaceHelmetScaleConfig;
        public static ConfigEntry<float> ArmorCarapaceHelmetLiftConfig;
        public static ConfigEntry<float> ArmorCarapaceHelmetPitchConfig;
        public static ConfigEntry<float> ArmorCarapaceHelmetHeightConfig;

        // Extra power for the Mistlands/Carapace-armored Skeleton Warrior, on top of
        // its real higher-armor-rating gear. Weapon damage itself can't be touched
        // directly (same "baked into the prefab" limitation noted elsewhere), so the
        // extra level bump is what raises its effective damage output, via Valheim's
        // own star system.
        public static ConfigEntry<float> MistlandsHealthMultiplierConfig;
        public static ConfigEntry<int> MistlandsExtraLevelsConfig;
        public static ConfigEntry<string> MistlandsBonusResistancesConfig;
        public static ConfigEntry<string> WarriorTier4WeaponsConfig;
        public static ConfigEntry<string> WarriorTier4ShieldConfig;
        public static ConfigEntry<bool> WarriorTier4WeaponDiag;
        public static ConfigEntry<float> EitrRegenBonus;
        public static ConfigEntry<int> NeedlesRequiredConfig;

        public static ConfigEntry<bool> DurabilityDamage;
        public static ConfigEntry<float> DurabilityDamageWarrior;
        public static ConfigEntry<float> DurabilityDamageMage;
        public static ConfigEntry<float> DurabilityDamageArcher;
        public static ConfigEntry<float> DurabilityDamagePoison;
        public static ConfigEntry<float> DurabilityDamageLeather;
        public static ConfigEntry<float> DurabilityDamageBronze;
        public static ConfigEntry<float> DurabilityDamageIron;
        public static ConfigEntry<float> DurabilityDamageBlackIron;

        private bool _chebsMercenariesActive = false;

        private void Awake()
        {
            if (!Base.VersionCheck(ChebsValheimLibraryVersion, out var message))
            {
                Jotunn.Logger.LogWarning(message);
            }

            CreateConfigValues();

            Phylactery.ConfigureRPC();
            PvPManager.ConfigureRPC();
            
            PrefabManager.OnVanillaPrefabsAvailable += () =>
            {
                // As of 5.2.0 and CVL version 2.7.0:
                // It's important to call this one during OnVanillaPrefabsAvailable because it utilises vanilla materials
                // when initialising custom items eg. taking the axe material, cloning it, assigning a different texture
                // then applying it to the custom weapon.
                LoadChebGonazAssetBundle();
                
                // At this point all the vanilla shaders are loaded, so go and fix references on the custom skeleton
                // materials.
                foreach (var material in SkeletonMinion.Bones.Values.ToList())
                {
                    material.FixReferences();
                }
                
                // clone ghost minion for spirit pylon
                var ghostPrefab = PrefabManager.Instance.GetPrefab("Ghost");
                if (ghostPrefab != null)
                {
                    var spiritPylonGhostPrefab = PrefabManager.Instance.CreateClonedPrefab(SpiritPylonGhostMinion.PrefabName, ghostPrefab);
                    
                    var humanoid = spiritPylonGhostPrefab.GetComponent<Humanoid>();
                    if (humanoid != null) humanoid.m_faction = Character.Faction.Players;
                    else Jotunn.Logger.LogError($"failed to get Humanoid on {SpiritPylonGhostMinion.PrefabName}");
                    
                    var monsterAI = spiritPylonGhostPrefab.GetComponent<MonsterAI>();
                    if (monsterAI != null) monsterAI.m_attackPlayerObjects = false;
                    else Jotunn.Logger.LogError($"failed to get MonsterAI on {SpiritPylonGhostMinion.PrefabName}");
                    
                    spiritPylonGhostPrefab.AddComponent<SpiritPylonGhostMinion>();
                    
                    // remove any drops eg. ectoplasm
                    var characterDrop = spiritPylonGhostPrefab.GetComponent<CharacterDrop>();
                    if (characterDrop != null) Destroy(characterDrop);
                    
                    CreatureManager.Instance.AddCreature(new CustomCreature(spiritPylonGhostPrefab, false));
                }
                else
                {
                    Jotunn.Logger.LogError($"Failed to establish {SpiritPylonGhostMinion.PrefabName}; Spirit Pylon will not work properly.");
                }
                
                // set spectral shroud up
                var trollCapePrefab = PrefabManager.Instance.GetPrefab("CapeTrollHide");
                if (trollCapePrefab != null)
                {
                    var spectralShroudPrefab = PrefabManager.Instance.CreateClonedPrefab("ChebGonaz_SpectralShroud", trollCapePrefab);
                    
                    var meshRenderer = spectralShroudPrefab.GetComponentInChildren<MeshRenderer>(true);
                    if (meshRenderer != null) meshRenderer.sharedMaterial = spectralShroudMaterial;
                    else Jotunn.Logger.LogError($"failed to get mesh renderer on {trollCapePrefab}");
                    
                    var skinnedMeshRenderer = spectralShroudPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (skinnedMeshRenderer != null) skinnedMeshRenderer.sharedMaterial = spectralShroudMaterial;
                    else Jotunn.Logger.LogError($"failed to get skinned mesh renderer on {trollCapePrefab}");
                    
                    var itemDrop = spectralShroudPrefab.GetComponent<ItemDrop>();
                    if (itemDrop != null)
                    {
                        itemDrop.m_itemData.m_shared.m_armorMaterial = spectralShroudMaterial;
                        itemDrop.m_itemData.m_shared.m_icons = new[] { spectralShroudSprite };
                    }
                    else Jotunn.Logger.LogError($"failed to get item drop on {trollCapePrefab}");
                    
                    
                    ItemManager.Instance.AddItem(spectralShroudItem.GetCustomItemFromPrefab(spectralShroudPrefab));
                }

                // set dark necromancer hood up (Ashlands Eitr hood clone)
                var mageHoodPrefab = PrefabManager.Instance.GetPrefab(DarkNecromancerHood.VanillaPrefabName);
                if (mageHoodPrefab != null)
                {
                    var darkHoodPrefab = PrefabManager.Instance.CreateClonedPrefab(DarkNecromancerHood.HoodItemName, mageHoodPrefab);
                    ItemManager.Instance.AddItem(darkHoodItem.GetCustomItemFromPrefab(darkHoodPrefab));
                }
                else Jotunn.Logger.LogError($"failed to get {DarkNecromancerHood.VanillaPrefabName} prefab");

                // set shadowed ash cape up (Ashlands ash cape clone)
                var ashCapePrefab = PrefabManager.Instance.GetPrefab(ShadowedAshCape.VanillaPrefabName);
                if (ashCapePrefab != null)
                {
                    var shadowCapePrefab = PrefabManager.Instance.CreateClonedPrefab(ShadowedAshCape.CapeItemName, ashCapePrefab);
                    ItemManager.Instance.AddItem(shadowCapeItem.GetCustomItemFromPrefab(shadowCapePrefab));
                }
                else Jotunn.Logger.LogError($"failed to get {ShadowedAshCape.VanillaPrefabName} prefab");

                // clone vanilla Ashlands creatures into tameable Charred minions.
                // Unlike skeletons/draugr these don't need a custom asset-bundle
                // model: Charred_Melee / Charred_Archer already exist in the base game.
                void CloneCharredPrefab(string vanillaPrefabName, string newPrefabName, System.Type minionComponentType, string displayNameKey)
                {
                    var vanillaPrefab = PrefabManager.Instance.GetPrefab(vanillaPrefabName);
                    if (vanillaPrefab == null)
                    {
                        Jotunn.Logger.LogError(
                            $"Failed to find vanilla prefab '{vanillaPrefabName}'. Charred minions require the " +
                            "Ashlands DLC to be installed, and the exact prefab name can change between game " +
                            "versions - verify it in-game if this error appears.");
                        return;
                    }

                    var clone = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, vanillaPrefab);

                    var charredHumanoid = clone.GetComponent<Humanoid>();
                    if (charredHumanoid != null)
                    {
                        charredHumanoid.m_faction = Character.Faction.Players;
                        charredHumanoid.m_name = displayNameKey;
                        // Base minions must always spawn bald: GiveDefaultItems also rolls
                        // m_randomSets and m_randomItems, so strip armor pieces there too.
                        // Weapons are never matched by name and stay untouched.
                        var armorPieces = new HashSet<string>
                        {
                            "Charred_Breastplate", "Charred_Helmet", "Charred_HipCloth", "Charred_MageCloths"
                        };
                        charredHumanoid.m_randomArmor = Array.Empty<GameObject>();
                        if (charredHumanoid.m_randomSets != null)
                        {
                            foreach (var set in charredHumanoid.m_randomSets)
                            {
                                if (set?.m_items != null)
                                {
                                    set.m_items = set.m_items
                                        .Where(go => go == null || !armorPieces.Contains(go.name)).ToArray();
                                }
                            }
                        }
                        if (charredHumanoid.m_randomItems != null)
                        {
                            charredHumanoid.m_randomItems = charredHumanoid.m_randomItems
                                .Where(ri => ri?.m_prefab == null || !armorPieces.Contains(ri.m_prefab.name)).ToArray();
                        }
                    }
                    else Jotunn.Logger.LogError($"failed to get Humanoid on {newPrefabName}");

                    var charredMonsterAI = clone.GetComponent<MonsterAI>();
                    if (charredMonsterAI != null) charredMonsterAI.m_attackPlayerObjects = false;
                    else Jotunn.Logger.LogError($"failed to get MonsterAI on {newPrefabName}");

                    // Vanilla Charred have no Tameable, so the Tameable hover patches
                    // (owner + following/waiting status) never fire. Mirror the Draugr
                    // bundle prefabs: commandable and already tamed.
                    var charredTameable = clone.GetComponent<Tameable>();
                    if (charredTameable == null) charredTameable = clone.AddComponent<Tameable>();
                    charredTameable.m_commandable = true;
                    charredTameable.m_startsTamed = true;
                    charredTameable.m_tamingTime = 1800f;

                    // remove the vanilla loot table; CharredMinion generates its own
                    // resource-refund drops via CharacterDrop/RecordDrops, like Draugr/Skeleton do
                    var charredCharacterDrop = clone.GetComponent<CharacterDrop>();
                    if (charredCharacterDrop != null) Destroy(charredCharacterDrop);

                    clone.AddComponent(minionComponentType);

                    CreatureManager.Instance.AddCreature(new CustomCreature(clone, false));
                }

                // NOTE: "Charred_Melee" and "Charred_Archer" are inferred from the vanilla
                // trophy item IDs (TrophyCharredMelee / TrophyCharredArcher). Confirm the
                // exact creature prefab names in-game before shipping.
                CloneCharredPrefab("Charred_Melee", "ChebGonaz_CharredWarrior", typeof(CharredWarriorMinion), "$chebgonaz_charredwarrior");
                CloneCharredPrefab("Charred_Archer", "ChebGonaz_CharredArcher", typeof(CharredArcherMinion), "$chebgonaz_charredarcher");
                CloneCharredPrefab("Charred_Twitcher", "ChebGonaz_CharredTwitcher", typeof(CharredTwitcherMinion), "$chebgonaz_charredtwitcher");
            };

            harmony.PatchAll();

            CommandManager.Instance.AddConsoleCommand(new KillAllMinions());
            CommandManager.Instance.AddConsoleCommand(new FindDeadPrefabs());
            CommandManager.Instance.AddConsoleCommand(new SummonAllMinions());
            CommandManager.Instance.AddConsoleCommand(new KillAllNeckros());
            CommandManager.Instance.AddConsoleCommand(new SetMinionOwnership());
            CommandManager.Instance.AddConsoleCommand(new SetNeckroHome());
            CommandManager.Instance.AddConsoleCommand(new TeleportNeckros());
            
            CommandManager.Instance.AddConsoleCommand(new SetBoneColor());
            CommandManager.Instance.AddConsoleCommand(new SetEyeColor());
            CommandManager.Instance.AddConsoleCommand(new ShowOptions());

            // PvP commands could've already been added by Cheb's Mercenaries, so check first
            var pvpCommands = new List<ConsoleCommand>()
                { new PvPAddFriend(), new PvPRemoveFriend(), new PvPListFriends() };
            foreach (var pvpCommand in pvpCommands)
            {
                if (!CommandManager.Instance.CustomCommands
                        .ToList().Exists(c => c.Name == pvpCommand.Name))
                    CommandManager.Instance.AddConsoleCommand(pvpCommand);
            }

            StartCoroutine(Phylactery.PhylacteriesCheck());

            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                Logger.LogInfo(!attr.InitialSynchronization
                    ? "Syncing configuration changes from server..."
                    : "Syncing initial configuration...");
                UpdateAllRecipes();
                StartCoroutine(RequestPvPDict());
            };

            StartCoroutine(WatchConfigFile());

            _chebsMercenariesActive = Chainloader.PluginInfos.TryGetValue("com.chebgonaz.chebsmercenaries", out _);
        }

        private IEnumerator RequestPvPDict()
        {
            yield return new WaitUntil(() => ZNet.instance != null && Player.m_localPlayer != null);
            PvPManager.InitialFriendsListRequest();
        }
        
        #region ConfigUpdate
        private byte[] GetFileHash(string fileName)
        {
            var sha1 = HashAlgorithm.Create();
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            return sha1.ComputeHash(stream);
        }

        private IEnumerator WatchConfigFile()
        {
            var lastHash = GetFileHash(ConfigFileFullPath);
            while (true)
            {
                yield return new WaitForSeconds(5);
                var hash = GetFileHash(ConfigFileFullPath);
                if (!hash.SequenceEqual(lastHash))
                {
                    lastHash = hash;
                    ReadConfigValues();
                }
            }
        }
        
        private void ReadConfigValues()
        {
            try
            {
                var adminOrLocal = ZNet.instance.IsServerInstance() || ZNet.instance.IsLocalInstance();
                Logger.LogInfo($"Read updated config values (admin/local={adminOrLocal})");
                if (adminOrLocal) Config.Reload();
                UpdateAllRecipes();
            }
            catch (Exception exc)
            {
                Logger.LogError($"There was an issue loading your {ConfigFileName}: {exc}");
            }
        }
        #endregion

        private void UpdateAllRecipes()
        {
            wands.ForEach(wand => wand.UpdateRecipe());
            necromancersHoodItem.UpdateRecipe();
            spectralShroudItem.UpdateRecipe();
            darkHoodItem.UpdateRecipe();
            shadowCapeItem.UpdateRecipe();

            SpiritPylon.UpdateRecipe();
            RefuelerPylon.UpdateRecipe();
            NeckroGathererPylon.UpdateRecipe();
            BatBeacon.UpdateRecipe();
            FarmingPylon.UpdateRecipe();
            RepairPylon.UpdateRecipe();
            TreasurePylon.UpdateRecipe();
            Phylactery.UpdateRecipe();
        }

        public ConfigEntry<T> ModConfig<T>(
            string group,
            string name,
            T defaultValue,
            string description = "",
            AcceptableValueBase acceptableValues = null,
            bool serverSync = false,
            params object[] tags)
        {
            // Create extended description with list of valid values and server sync
            ConfigDescription extendedDescription = new(
                description + (serverSync
                    ? " [Synced with Server]"
                    : " [Not Synced with Server]"),
                acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = serverSync },
                tags);

            var configEntry = Config.Bind(group, name, defaultValue, extendedDescription);

            return configEntry;
        }

        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;
            
            HeavyLogging = Config.Bind("General (Client)", "HeavyLogging",
                false, new ConfigDescription("Switch on to fill the logs with excessive " +
                                             "logging to assist with debugging."));
            
            PvPAllowed = Config.Bind("General (Server Synced)", "PvPAllowed",
                false, new ConfigDescription("Whether minions will target and attack other players and their minions.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RadeonFriendly = Config.Bind("General (Client)", "RadeonFriendly",
                false, new ConfigDescription("ONLY set this to true if you have graphical issues with " +
                                             "the mod. It will disable all particle effects for the mod's prefabs " +
                                             "which seem to give users with Radeon cards trouble for unknown " +
                                             "reasons. If you have problems with lag it might also help to switch" +
                                             "this setting on."));
            
            OptionsGUI.CreateConfigs(this, PluginGuid);
            
            NoWraithSmoke = Config.Bind("General (Client)", "NoWraithSmoke",
                false, new ConfigDescription("Set this to true if you want to disable smoke on the wraith."));

            #region BoneFragments

            BoneFragmentsDroppedAmountMin = Config.Bind("General (Server Synced)", "BoneFragmentsDroppedAmountMin",
                1, new ConfigDescription("The minimum amount of bones dropped by creatures.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BoneFragmentsDroppedAmountMax = Config.Bind("General (Server Synced)", "BoneFragmentsDroppedAmountMax",
                3, new ConfigDescription("The maximum amount of bones dropped by creatures.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BoneFragmentsDroppedChance = Config.Bind("General (Server Synced)", "BoneFragmentsDroppedChance",
                .25f, new ConfigDescription("The chance of bones dropped by creatures (0 = 0%, 1 = 100%).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            #endregion

            #region MinionUpgrades

            ArmorLeatherScrapsRequiredConfig = Config.Bind("General (Server Synced)", "ArmorLeatherScrapsRequired",
                2, new ConfigDescription("The amount of LeatherScraps required to craft a minion in leather armor.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArmorBronzeRequiredConfig = Config.Bind("General (Server Synced)", "ArmorBronzeRequired",
                1, new ConfigDescription("The amount of Bronze required to craft a minion in bronze armor.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArmorIronRequiredConfig = Config.Bind("General (Server Synced)", "ArmoredIronRequired",
                1, new ConfigDescription("The amount of Iron required to craft a minion in iron armor.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArmorBlackIronRequiredConfig = Config.Bind("General (Server Synced)", "ArmorBlackIronRequired",
                1, new ConfigDescription("The amount of Black Metal required to craft a minion in black iron armor.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Mistlands/Carapace is an extra, on-top-of-Black-Metal upgrade for the
            // Skeleton Warrior - see SkeletonWand's Warrior branch. Not tied to
            // ChebGonazMinion.ArmorType (an external enum we can't extend), so it's
            // its own standalone resource check, consumed in addition to Black Metal.
            ArmorCarapaceRequiredConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceRequired",
                5, new ConfigDescription(
                    "The amount of Carapace required, on top of Black Metal, to craft a Skeleton Warrior in Mistlands/Carapace armor instead of Black Iron armor.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Unity-authored helmet first (bundle chebgonaz-carapace, guide:
            // UNITY_HELMET_GUIDE.md): hand-fitted, ships ready, no adjust pass
            // touches it. Then vanilla Carapace, then black iron. Custom and
            // iron rigs need no scale/lift (vanilla-only knobs below).
            ArmorCarapaceHelmetPrefabConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceHelmetPrefab",
                "ChebGonaz_HelmetCarapaceSkeleton", new ConfigDescription(
                    "Helmet prefab for carapace wearers. Priority: configured (Unity bundle) -> vanilla HelmetCarapace -> black iron.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Only kept for legacy vanilla-helmet tuning. Custom skeleton-rigged
            // helmet must ship at correct size with scale 1.0.
            ArmorCarapaceHelmetScaleConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceHelmetScale",
                1.0f, new ConfigDescription(
                    "Scale multiplier applied to the Carapace helmet on the Skeleton Warrior. 1.0 = no change (correct for custom skeleton-rigged helmet).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Vanilla HelmetCarapace spawns embedded inside the skeleton skull
            // (player-rigged mesh on a skeleton head anchor): scaling grows it
            // in place but never moves it out. Lift shifts the equipped helmet
            // along its local Y so it sits on the crown instead of inside.
            // Tune live: change value, re-summon, observe. Negative = down.
            ArmorCarapaceHelmetLiftConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceHelmetLift",
                0.25f, new ConfigDescription(
                    "Meters to lift the Carapace helmet along its local Y after equipping (0.25 = a quarter meter up). 0 = no lift.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // OBJ bind space aims the visor at the floor: pitch it forward.
            // Assigned (never accumulated). Tune live with F1, re-summon.
            ArmorCarapaceHelmetPitchConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceHelmetPitch",
                -90f, new ConfigDescription(
                    "Degrees about X applied to a Unity-built carapace helmet. -90 pitches a floor-facing visor forward; use +90 if it ends up backwards. Iron/vanilla helmets ignore it.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArmorCarapaceHelmetHeightConfig = Config.Bind("General (Server Synced)", "ArmorCarapaceHelmetHeight",
                0f, new ConfigDescription(
                    "Vertical meters added to a Unity-built carapace helmet (fitted base). Negative lowers it (e.g. -0.08). Tune live, re-summon. Iron/vanilla helmets ignore it.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MistlandsHealthMultiplierConfig = Config.Bind("General (Server Synced)", "MistlandsHealthMultiplier",
                1.3f, new ConfigDescription(
                    "Health multiplier applied on top of the normal Skeleton Warrior health formula for the Mistlands/Carapace-armored variant (Black Metal + enough Carapace). 1.0 = no bonus.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Defaults to 0 (off): SkeletonTierThreeQuality is already 3 by default,
            // Valheim's own star-icon display cap - going to 4 wouldn't show MORE
            // stars, it would show NONE, making the Mistlands variant look weaker
            // at a glance even though it mechanically isn't. Left available for
            // servers that have lowered SkeletonTierThreeQuality below 3 and have
            // room to use this safely, but off by default to avoid that visual bug.
            MistlandsExtraLevelsConfig = Config.Bind("General (Server Synced)", "MistlandsExtraLevels",
                0, new ConfigDescription(
                    "Extra star levels added on top of SkeletonTierThreeQuality for the Mistlands/Carapace-armored Warrior. CAUTION: Valheim's star icon only displays up to 3 - if SkeletonTierThreeQuality is already 3 (the default), going higher shows NO stars at all instead of more, which looks like a downgrade. Only raise this if you've also lowered SkeletonTierThreeQuality to leave room. 0 = no bonus (default, recommended).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MistlandsBonusResistancesConfig = Config.Bind("General (Server Synced)", "MistlandsBonusResistances",
                "Pierce:Resistant", new ConfigDescription(
                    "Comma-delimited DamageType:DamageModifier pairs applied only to the Mistlands/Carapace-armored Skeleton Warrior, eg. 'Pierce:Resistant,Frost:VeryResistant'. Valid DamageType: Blunt, Slash, Pierce, Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit. Valid DamageModifier: Normal, Resistant, VeryResistant, Weak, VeryWeak, Immune, Ignore. Leave blank for none.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Tier4 (BlackMetal) weapon pool: comma-delimited vanilla weapon prefab
            // names rolled randomly via the prefab's baked m_randomWeapon (same
            // mechanism as the original custom weapons, so no relog issues).
            // Real vanilla names (Claude-verified): SwordBlackmetal, AxeBlackMetal.
            // MaceNeedle (Porcupine, blunt Plains) fills the blunt slot; the swap
            // logs every missing name, so an unverified entry can never break
            // anything - worst case that slot is skipped.
            WarriorTier4WeaponsConfig = Config.Bind("General (Server Synced)", "WarriorTier4Weapons",
                "SwordBlackmetal,AxeBlackMetal,MaceNeedle", new ConfigDescription(
                    "Comma-delimited vanilla weapon prefab names for WarriorTier4 (BlackMetal/Caparazon tier): vanilla look + damage, with per-instance skeleton-compatible attack animation. Leave blank to keep the bundle's custom blackmetal weapons.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WarriorTier4WeaponDiag = Config.Bind("General (Client)", "WarriorTier4WeaponDiag",
                false, new ConfigDescription("Log-only diagnostic at load: dumps the factory weapon fields (m_randomWeapon, m_defaultItems, ...) of every Warrior tier prefab before any swap, plus whether each configured vanilla weapon resolves. Turn off after verifying."));

            WarriorTier4ShieldConfig = Config.Bind("General (Server Synced)", "WarriorTier4Shield",
                "ShieldBlackmetal", new ConfigDescription(
                    "Vanilla shield prefab replacing the wooden buckler in WarriorTier4's random shield pool (keeps the 50/50 shield-or-nothing roll). Blank to keep the bundle's wooden shield.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            EitrRegenBonus = Config.Bind("General (Server Synced)", "EitrRegenBonus",
                2f, new ConfigDescription("Eitr (mana) regeneration per second granted by Dark Necromancer Hood while equipped.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NeedlesRequiredConfig = Config.Bind("General (Server Synced)", "NeedlesRequired",
                5, new ConfigDescription("The amount of needles required to craft a needle warrior.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            #endregion

            #region Durability

            DurabilityDamage = Config.Bind("General (Server Synced)", "DurabilityDamage",
                true, new ConfigDescription("Whether using a wand damages its durability.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageWarrior = Config.Bind("General (Server Synced)", "DurabilityDamageWarrior",
                1f, new ConfigDescription("How much creating a warrior damages a wand.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageArcher = Config.Bind("General (Server Synced)", "DurabilityDamageArcher",
                3f, new ConfigDescription("How much creating an archer damages a wand.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageMage = Config.Bind("General (Server Synced)", "DurabilityDamageMage",
                5f, new ConfigDescription("How much creating a mage damages a wand.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamagePoison = Config.Bind("General (Server Synced)", "DurabilityDamagePoison",
                5f, new ConfigDescription("How much creating a poison skeleton damages a wand.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageLeather = Config.Bind("General (Server Synced)", "DurabilityDamageLeather",
                1f, new ConfigDescription(
                    "How much armoring the minion in leather damages the wand (value is added on top of damage from minion type).",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageBronze = Config.Bind("General (Server Synced)", "DurabilityDamageBronze",
                1f, new ConfigDescription(
                    "How much armoring the minion in bronze damages the wand (value is added on top of damage from minion type)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageIron = Config.Bind("General (Server Synced)", "DurabilityDamageIron",
                1f, new ConfigDescription(
                    "How much armoring the minion in iron damages the wand (value is added on top of damage from minion type)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DurabilityDamageBlackIron = Config.Bind("General (Server Synced)", "DurabilityDamageBlackIron",
                1f, new ConfigDescription(
                    "How much armoring the minion in black iron damages the wand (value is added on top of damage from minion type)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            #endregion

            UndeadMinion.CreateConfigs(this);

            SkeletonMinion.CreateConfigs(this);
            PoisonSkeletonMinion.CreateConfigs(this);
            SkeletonArcherFireMinion.CreateConfigs(this);
            SkeletonArcherFrostMinion.CreateConfigs(this);
            SkeletonArcherPoisonMinion.CreateConfigs(this);
            SkeletonArcherSilverMinion.CreateConfigs(this);
            SkeletonArcherTier1Minion.CreateConfigs(this);
            SkeletonArcherTier2Minion.CreateConfigs(this);
            SkeletonArcherTier3Minion.CreateConfigs(this);
            SkeletonMageMinion.CreateConfigs(this);
            SkeletonWarriorMinion.CreateConfigs(this);
            SkeletonWoodcutterMinion.CreateConfigs(this);
            SkeletonMinerMinion.CreateConfigs(this);
            SkeletonPriestMinion.CreateConfigs(this);

            DraugrMinion.CreateConfigs(this);
            DraugrWarriorMinion.CreateConfigs(this);
            DraugrArcherFireMinion.CreateConfigs(this);
            DraugrArcherFrostMinion.CreateConfigs(this);
            DraugrArcherPoisonMinion.CreateConfigs(this);
            DraugrArcherSilverMinion.CreateConfigs(this);
            DraugrArcherTier1Minion.CreateConfigs(this);
            DraugrArcherTier2Minion.CreateConfigs(this);
            DraugrArcherTier3Minion.CreateConfigs(this);

            CharredMinion.CreateConfigs(this);
            CharredWarriorMinion.CreateConfigs(this);
            CharredArcherMinion.CreateConfigs(this);
            CharredTwitcherMinion.CreateConfigs(this);

            GuardianWraithMinion.CreateConfigs(this);

            LeechMinion.CreateConfigs(this);
            BattleNeckroMinion.CreateConfigs(this);

            wands.ForEach(w => w.CreateConfigs(this));

            spectralShroudItem.CreateConfigs(this);
            necromancersHoodItem.CreateConfigs(this);
            darkHoodItem.CreateConfigs(this);
            shadowCapeItem.CreateConfigs(this);

            SpiritPylon.CreateConfigs(this);
            RefuelerPylon.CreateConfigs(this);
            NeckroGathererPylon.CreateConfigs(this);
            BatBeacon.CreateConfigs(this);
            BatLantern.CreateConfigs(this);
            FarmingPylon.CreateConfigs(this);
            RepairPylon.CreateConfigs(this);
            TreasurePylon.CreateConfigs(this);
            Phylactery.CreateConfigs(this);

            NeckroGathererMinion.CreateConfigs(this);
            
            PvPOptionsGUI.CreateConfigs(this, PluginGuid);
        }
        

        // Read-only diagnostic (Claude's rule: never guess a field name twice).
        // Dumps every *weapon* field found by reflection on Humanoid/MonsterAI
        // for each factory Warrior prefab, BEFORE the vanilla swap below runs,
        // so the log shows the real structure without touching anything.
        private static void DumpWarriorWeaponFields()
        {
            if (!WarriorTier4WeaponDiag.Value) return;

            var prefabNames = new[]
            {
                "ChebGonaz_SkeletonWarrior",
                "ChebGonaz_SkeletonWarriorTier2",
                "ChebGonaz_SkeletonWarriorTier3",
                "ChebGonaz_SkeletonWarriorTier4",
            };
            var flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;

            foreach (var prefabName in prefabNames)
            {
                var prefab = PrefabManager.Instance.GetPrefab(prefabName);
                if (prefab == null)
                {
                    Jotunn.Logger.LogWarning($"[WeaponDiag] {prefabName}: prefab not found.");
                    continue;
                }
                var humanoid = prefab.GetComponent<Humanoid>();
                if (humanoid == null)
                {
                    Jotunn.Logger.LogWarning($"[WeaponDiag] {prefabName}: no Humanoid component.");
                    continue;
                }
                Jotunn.Logger.LogInfo($"[WeaponDiag] {prefabName}: m_randomWeapon=[{WeaponNames(humanoid.m_randomWeapon)}] " +
                    $"m_defaultItems=[{WeaponNames(humanoid.m_defaultItems)}] " +
                    $"m_unarmedWeapon=[{(humanoid.m_unarmedWeapon != null ? humanoid.m_unarmedWeapon.name : "null")}] " +
                    $"m_randomSets={humanoid.m_randomSets?.Length.ToString() ?? "null"}");
                foreach (var field in typeof(Humanoid).GetFields(flags))
                {
                    if (field.Name.IndexOf("eapon", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (field.Name is "m_randomWeapon" or "m_unarmedWeapon") continue; // already logged above
                    object value;
                    try { value = field.GetValue(humanoid); }
                    catch { value = "<unreadable>"; }
                    Jotunn.Logger.LogInfo($"[WeaponDiag] {prefabName}: Humanoid.{field.Name} ({field.FieldType.Name}) = {WeaponValue(value)}");
                }
                var monsterAI = prefab.GetComponent<MonsterAI>();
                if (monsterAI == null)
                {
                    Jotunn.Logger.LogInfo($"[WeaponDiag] {prefabName}: no MonsterAI component.");
                    continue;
                }
                var aiWeaponFields = typeof(MonsterAI).GetFields(flags)
                    .Where(f => f.Name.IndexOf("eapon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => f.Name)
                    .ToList();
                Jotunn.Logger.LogInfo($"[WeaponDiag] {prefabName}: MonsterAI weapon-related fields: " +
                    (aiWeaponFields.Count == 0 ? "none" : string.Join(",", aiWeaponFields)));
            }

            var wanted = (WarriorTier4WeaponsConfig.Value ?? "")
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            foreach (var name in wanted)
            {
                var resolved = PrefabManager.Instance.GetPrefab(name);
                Jotunn.Logger.LogInfo($"[WeaponDiag] wanted '{name}': " +
                    (resolved != null ? $"resolves to {resolved.name}" : "NOT FOUND (will be skipped by swap)"));
            }
        }

        private static string WeaponNames(GameObject[] arr)
        {
            if (arr == null) return "null";
            return string.Join(",", arr.Select(g => g != null ? g.name : "null"));
        }

        private static string WeaponValue(object value)
        {
            if (value == null) return "null";
            if (value is GameObject go) return go.name;
            if (value is System.Collections.IEnumerable seq)
            {
                var names = new List<string>();
                foreach (var item in seq) names.Add(item is GameObject g ? g.name : item?.ToString() ?? "null");
                return "[" + string.Join(",", names) + "]";
            }
            return value.ToString();
        }

        // Load-time snapshot of vanilla attack animations, for the no-leak proof
        // (re-read after minions patch their own instances).
        internal static readonly Dictionary<string, string> VanillaWeaponAnimBaseline = new();

        private static readonly Dictionary<string, GameObject> _carapaceArmorCache = new();

        // Chest/legs transplant for carapace wearers (visual-only, like the
        // helmet): vanilla carapace mesh+materials on the proven blackiron
        // hierarchy, bones remapped by name to the skeleton rig. Vanilla
        // carapace armor NREs VisEquipment attach on skeletons (naked,
        // spawn-aborting minions), so it is NEVER equipped directly: miss
        // here falls back to custom blackiron, never vanilla.
        internal static GameObject GetCarapaceArmorPiece(string kind) // "chest" | "legs"
        {
            if (_carapaceArmorCache.TryGetValue(kind, out var cached) && cached != null)
            {
                EnsureTransplantRegistered(cached); // ObjectDB is per-world
                return cached;
            }
            var built = BuildCarapaceArmorTransplant(kind);
            if (built != null)
            {
                _carapaceArmorCache[kind] = built;
                EnsureTransplantRegistered(built);
                return built;
            }
            var fallbackName = kind == "legs" ? "ChebGonaz_ArmorBlackIronLegs" : "ChebGonaz_ArmorBlackIronChest";
            var fallback = PrefabManager.Instance.GetPrefab(fallbackName);
            if (fallback == null) Jotunn.Logger.LogError($"CarapaceArmor: fallback {fallbackName} missing too.");
            return fallback;
        }

        private static GameObject BuildCarapaceArmorTransplant(string kind)
        {
            try
            {
                var ironName = kind == "legs" ? "ChebGonaz_ArmorBlackIronLegs" : "ChebGonaz_ArmorBlackIronChest";
                var vanillaName = kind == "legs" ? "ArmorCarapaceLegs" : "ArmorCarapaceChest";
                var cloneName = kind == "legs" ? "ChebGonaz_ArmorCarapaceLegs" : "ChebGonaz_ArmorCarapaceChest";
                var iron = PrefabManager.Instance.GetPrefab(ironName);
                var vanilla = PrefabManager.Instance.GetPrefab(vanillaName);
                var skeleton = PrefabManager.Instance.GetPrefab("ChebGonaz_SkeletonWarriorTier4");
                if (iron == null || vanilla == null || skeleton == null)
                {
                    Jotunn.Logger.LogWarning($"CarapaceArmor: iron={iron != null} vanilla={vanilla != null} skeleton={skeleton != null}, skipping.");
                    return null;
                }
                var boneMap = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
                    if (!boneMap.ContainsKey(t.name)) boneMap[t.name] = t;
                SkinnedMeshRenderer vanillaSkinned = null;
                foreach (var r in vanilla.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { vanillaSkinned = r; break; }
                SkinnedMeshRenderer ironSkinned = null;
                foreach (var r in iron.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { ironSkinned = r; break; }
                if (vanillaSkinned?.sharedMesh == null || ironSkinned == null)
                {
                    Jotunn.Logger.LogWarning("CarapaceArmor: renderer anatomy mismatch, skipping.");
                    return null;
                }
                var mesh = vanillaSkinned.sharedMesh;
                var used = new HashSet<int>();
                foreach (var bw in mesh.boneWeights)
                {
                    if (bw.weight0 > 0f) used.Add(bw.boneIndex0);
                    if (bw.weight1 > 0f) used.Add(bw.boneIndex1);
                    if (bw.weight2 > 0f) used.Add(bw.boneIndex2);
                    if (bw.weight3 > 0f) used.Add(bw.boneIndex3);
                }
                var vanillaBones = vanillaSkinned.bones;
                if (vanillaBones == null || vanillaBones.Length == 0)
                {
                    Jotunn.Logger.LogWarning("CarapaceArmor: vanilla has no bones array, skipping.");
                    return null;
                }
                var remapped = new Transform[vanillaBones.Length];
                var missing = new List<string>();
                for (int i = 0; i < vanillaBones.Length; i++)
                {
                    var vb = vanillaBones[i];
                    if (vb == null) continue;
                    if (boneMap.TryGetValue(vb.name, out var mapped)) remapped[i] = mapped;
                    else if (used.Contains(i)) missing.Add(vb.name);
                    else remapped[i] = vb;
                }
                if (missing.Count > 0)
                {
                    Jotunn.Logger.LogWarning($"CarapaceArmor: unmapped bones [{string.Join(",", missing)}], skipping.");
                    return null;
                }
                var vRoot = vanillaSkinned.rootBone;
                if (vRoot == null || !boneMap.TryGetValue(vRoot.name, out var sRoot))
                {
                    Jotunn.Logger.LogWarning("CarapaceArmor: root bone unmapped, skipping.");
                    return null;
                }
                // Fresh GameObject (never Instantiate): instantiating the iron
                // prefab registers a ZNetView phantom with the live world and
                // its later destroy poisons ZNetScene.RemoveObjects every
                // frame. Structure (names + offsets) is copied from the proven
                // iron piece, skin from vanilla, ItemDrop grafted from iron
                // (keeps armor stats + equip slot). Same pattern as the helmet.
                var chain = new List<Transform>();
                for (var t = ironSkinned.transform; t != null && t != iron.transform; t = t.parent)
                    chain.Add(t);
                chain.Reverse();
                if (_carapaceHelmetHolder == null)
                {
                    _carapaceHelmetHolder = new GameObject("ChebGonaz_HelmetTemplateHolder");
                    UnityEngine.Object.DontDestroyOnLoad(_carapaceHelmetHolder);
                }
                var clone = new GameObject(cloneName);
                clone.transform.SetParent(_carapaceHelmetHolder.transform, false);
                Transform visualParent = clone.transform;
                if (chain.Count > 0)
                {
                    foreach (var link in chain)
                    {
                        var node = new GameObject(link.name);
                        node.transform.SetParent(visualParent, false);
                        node.transform.localPosition = link.localPosition;
                        node.transform.localRotation = link.localRotation;
                        node.transform.localScale = link.localScale;
                        visualParent = node.transform;
                    }
                }
                var cloneSkinned = visualParent.gameObject.AddComponent<SkinnedMeshRenderer>();
                cloneSkinned.sharedMesh = mesh;
                cloneSkinned.sharedMaterials = vanillaSkinned.sharedMaterials;
                cloneSkinned.bones = remapped;
                cloneSkinned.rootBone = sRoot;
                cloneSkinned.updateWhenOffscreen = true;
                GraftHelmetItemDrop(clone, ironName);
                // ObjectDB-registered so instances resolve m_dropPrefab
                // (unregistered names NRE every SetupVisEquipment). Relog
                // covered via the carapace flag (name restore + re-equip).
                RegisterTransplantInObjectDB(clone);
                Jotunn.Logger.LogInfo($"CarapaceArmor: {cloneName} built (mesh={mesh.name}, bones={remapped.Length}, root={sRoot.name}).");
                return clone;
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"CarapaceArmor {kind} failed ({ex.Message}), falling back.");
                return null;
            }
        }

        // Builds ChebGonaz_HelmetCarapaceSkeleton (the config default) without
        // Unity: the proven skeleton-rigged iron helmet hierarchy, with ONLY
        // its rigid mesh + materials swapped to vanilla Carapace. Anatomy
        // (verified in-bundle): iron = MeshFilter+MeshRenderer under
        // attach/bronzehelmet (rigid, placement proven); vanilla = skinned
        // mesh under attach_skin/CarapaceHelmet. Rigid swap keeps the proven
        // placement; the existing scale/lift tuning still applies (child keeps
        // its name). Stats stay iron (visual-only transplant). Any failure ->
        // null and the iron fallback path carries on like today.
        //
        // LAZY on purpose: built on first carapace spawn IN-GAME, never at
        // load. Instantiating ZNetView-bearing prefabs in the menu scene
        // poisoned the clone (NREs on every later equip). Unregistered on
        // purpose too (registry timing vs live ZNetScene): the template is
        // cached statically + DontDestroyOnLoad, handed by reference to
        // ScaleEquipment, and re-equipped from cache on every relog via the
        // carapace ZDO flag (see WaitForZNet).
        private static GameObject _carapaceHelmetClone;
        private static GameObject _carapaceHelmetHolder;

        // Mesh-space center of the vanilla helmet geometry (read-only bounds).
        // Rigid rendering places mesh-space verts at nodePos + verts, so the
        // node must sit at lift - center*scale to land the geometry center on
        // the attach point (+lift). Without this the helmet renders up in the
        // sky (body-space geometry) and reads as "missing".
        internal static Vector3 s_helmetMeshCenter = Vector3.zero;
        internal static bool s_helmetMeshCenterValid = false;

        // Fitted base position of the Unity-built helmet node (set at build).
        internal static Vector3 s_helmetBasePos = Vector3.zero;
        internal static bool s_helmetBasePosValid = false;

        internal static GameObject BuildCarapaceHelmetClone()
        {
            if (_carapaceHelmetClone != null) return _carapaceHelmetClone; // cached (dead refs read null in Unity)
            const string cloneName = "ChebGonaz_HelmetCarapaceSkeleton";
            try
            {
                // Mesh from the loose OBJ next to the DLL (version-proof: no
                // AssetBundle, no readability flags involved - we own the mesh).
                // Materials by reference from the vanilla helmet (read-only use).
                var modDir = Path.GetDirectoryName(typeof(BasePlugin).Assembly.Location);
                var objPath = modDir != null ? Path.Combine(modDir, "CarapaceHelmet.obj") : null;
                var vanilla = PrefabManager.Instance.GetPrefab("HelmetCarapace");
                SkinnedMeshRenderer vanillaSkinned = null;
                if (vanilla != null)
                {
                    var vgo = vanilla.transform.Find("attach_skin/CarapaceHelmet")?.gameObject
                        ?? System.Array.Find(vanilla.GetComponentsInChildren<Transform>(true),
                            t => t.name.IndexOf("CarapaceHelmet", System.StringComparison.OrdinalIgnoreCase) >= 0)?.gameObject;
                    vanillaSkinned = vgo?.GetComponent<SkinnedMeshRenderer>();
                }
                if (string.IsNullOrEmpty(objPath) || !File.Exists(objPath) || vanillaSkinned == null)
                {
                    Jotunn.Logger.LogWarning($"BuildCarapaceHelmet: objPresent={objPath != null && File.Exists(objPath ?? "")} vanilla={vanillaSkinned != null}, skipping (iron fallback stays).");
                    return null;
                }
                var mesh = ParseObjMesh(objPath, 1000f);
                if (mesh == null) return null;
                // Fresh GameObject (never Instantiate the iron prefab): iron
                // brings its ZNetView + bronzehelmet visuals (wrong adjust
                // target, double builds, network poisoning). Bare
                // AddComponent<ItemDrop> NREs once in Awake (empty m_shared)
                // — single harmless log per session, helmet itself works.
                if (_carapaceHelmetHolder == null)
                {
                    _carapaceHelmetHolder = new GameObject("ChebGonaz_HelmetTemplateHolder");
                    UnityEngine.Object.DontDestroyOnLoad(_carapaceHelmetHolder);
                }
                var clone = new GameObject(cloneName);
                clone.transform.SetParent(_carapaceHelmetHolder.transform, false);
                var attach = new GameObject("attach");
                attach.transform.SetParent(clone.transform, false);
                var meshGO = new GameObject("carapacehelmet");
                meshGO.transform.SetParent(attach.transform, false);
                // Fitted numbers measured from the user-assembled Unity prefab
                // (same mesh, same placement): node scale + position.
                meshGO.transform.localPosition = new Vector3(0f, 0.232f, -0.007f);
                meshGO.transform.localScale = Vector3.one * 0.11f;
                s_helmetBasePos = meshGO.transform.localPosition;
                s_helmetBasePosValid = true;
                meshGO.AddComponent<MeshFilter>().sharedMesh = mesh;
                meshGO.AddComponent<MeshRenderer>().sharedMaterials = vanillaSkinned.sharedMaterials;
                GraftHelmetItemDrop(clone);
                RegisterTransplantInObjectDB(clone);
                _carapaceHelmetClone = clone;
                Jotunn.Logger.LogInfo($"BuildCarapaceHelmet: {cloneName} built from OBJ (verts={mesh.vertexCount}, bounds={mesh.bounds}).");
                return clone;
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"BuildCarapaceHelmet failed ({ex.Message}), iron fallback stays.");
                return null;
            }
        }

        // Minimal OBJ reader (v/vt/vn/f, fan-triangulated). Enough for the
        // 422-vert helmet; returns null on any malformed input.
        private static Mesh ParseObjMesh(string path, float unitScale)
        {
            try
            {
                var positions = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var vertMap = new Dictionary<(int, int, int), int>();
                var outVerts = new List<Vector3>();
                var outNorms = new List<Vector3>();
                var outUvs = new List<Vector2>();
                var tris = new List<int>();
                int Resolve(int idx, int count) => idx < 0 ? count + idx : idx - 1;
                foreach (var rawLine in File.ReadLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("v ")) { var p = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries); positions.Add(new Vector3(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) * unitScale, float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) * unitScale, float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) * unitScale)); }
                    else if (line.StartsWith("vn ")) { var p = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries); normals.Add(new Vector3(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture))); }
                    else if (line.StartsWith("vt ")) { var p = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries); uvs.Add(new Vector2(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture))); }
                    else if (line.StartsWith("f "))
                    {
                        var corners = new List<int>();
                        foreach (var tok in line.Substring(2).Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = (tok + "//").Split('/');
                            int vi = Resolve(int.Parse(parts[0]), positions.Count);
                            int vti = parts.Length > 1 && parts[1].Length > 0 ? Resolve(int.Parse(parts[1]), uvs.Count) : -1;
                            int vni = parts.Length > 2 && parts[2].Length > 0 ? Resolve(int.Parse(parts[2]), normals.Count) : -1;
                            var key = (vi, vti, vni);
                            if (!vertMap.TryGetValue(key, out var idx))
                            {
                                idx = outVerts.Count;
                                vertMap[key] = idx;
                                outVerts.Add(positions[vi]);
                                outUvs.Add(vti >= 0 ? uvs[vti] : Vector2.zero);
                                outNorms.Add(vni >= 0 ? normals[vni] : Vector3.up);
                            }
                            corners.Add(idx);
                        }
                        for (int i = 1; i + 1 < corners.Count; i++) { tris.Add(corners[0]); tris.Add(corners[i]); tris.Add(corners[i + 1]); }
                    }
                }
                if (outVerts.Count == 0 || tris.Count == 0) return null;
                var mesh = new Mesh { name = "CarapaceHelmetRT" };
                mesh.SetVertices(outVerts);
                mesh.SetNormals(outNorms);
                mesh.SetUVs(0, outUvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"ParseObjMesh failed ({ex.GetType().Name}: {ex.Message}).");
                return null;
            }
        }
        // Runtime registration for transplant templates (mirrors what Jotunn
        // does for queued customs at init, but lazily in-game): backfill
        // m_dropPrefab + ObjectDB inject (so instances resolve it) + full
        // PrefabManager/ZNetScene registration (so the VISUAL attach, which
        // resolves by registry and NOT by reference, finds the mesh —
        // without this the helmet equips cleanly yet renders invisible).
        // Templates live under a DontDestroyOnLoad holder, so refs stay
        // valid across world loads; per-serve verification re-injects.
        private static void RegisterTransplantInObjectDB(GameObject template)
        {
            try
            {
                var itemDrop = template.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    Jotunn.Logger.LogWarning($"RegisterTransplant: {template.name} has no ItemDrop, skipping.");
                    return;
                }
                if (itemDrop.m_itemData != null && itemDrop.m_itemData.m_dropPrefab == null)
                    itemDrop.m_itemData.m_dropPrefab = template;
                EnsureTransplantRegistered(template);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"RegisterTransplant {template?.name} failed ({ex.Message}).");
            }
        }

        // Idempotent per-serve verification: ObjectDB is recreated per world,
        // so entries must be re-injected whenever missing or pointing
        // elsewhere. Called on every serve path (spawn + relog re-equip).
        internal static void EnsureTransplantRegistered(GameObject template)
        {
            if (template == null) return;
            var objectDB = ObjectDB.instance;
            if (objectDB == null) return;
            try
            {
                var hash = template.name.GetStableHashCode();
                if (!objectDB.m_itemByHash.TryGetValue(hash, out var current)
                    || !object.ReferenceEquals(current, template))
                {
                    objectDB.m_itemByHash[hash] = template;
                    if (!objectDB.m_items.Contains(template)) objectDB.m_items.Add(template);
                    if (PrefabManager.Instance.GetPrefab(template.name) == null)
                        PrefabManager.Instance.AddPrefab(template);
                    if (ZNetScene.instance != null)
                    {
                        try { PrefabManager.Instance.RegisterToZNetScene(template); }
                        catch (System.Exception ex) { Jotunn.Logger.LogWarning($"RegisterTransplant: ZNetScene register {template.name} ({ex.Message})."); }
                    }
                    var itemDrop = template.GetComponent<ItemDrop>();
                    if (itemDrop?.m_itemData != null && itemDrop.m_itemData.m_dropPrefab == null)
                        itemDrop.m_itemData.m_dropPrefab = template;
                    Jotunn.Logger.LogInfo($"RegisterTransplant: {template.name} registered (ObjectDB + PrefabManager + ZNetScene).");
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"RegisterTransplant {template.name} failed ({ex.Message}).");
            }
        }

        private const System.Reflection.BindingFlags CloneFlags =
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;

        private static object ShallowClone(object orig)
        {
            var clone = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(orig.GetType());
            foreach (var field in orig.GetType().GetFields(CloneFlags))
                field.SetValue(clone, field.GetValue(orig));
            return clone;
        }

        private static bool SetField(object target, string fieldName, object value)
        {
            if (target == null) return false;
            var field = target.GetType().GetField(fieldName, CloneFlags);
            if (field == null) return false;
            field.SetValue(target, value);
            return true;
        }

        // Grafts a working ItemDrop onto runtime-built prefabs (fresh
        // GameObjects have none; Unity-authored ones lost it at build, having
        // no game assemblies in Editor). Copies the named iron donor's data
        // (armor stats + equip slot ride along, so armor grafts from its own
        // iron piece, never from the helmet); dropPrefab resolves by name at
        // equip (Ensure covers registration). Idempotent.
        internal static void GraftHelmetItemDrop(GameObject helmetPrefab, string ironSourceName = "ChebGonaz_HelmetBlackIronSkeleton")
        {
            if (helmetPrefab.GetComponent<ItemDrop>() != null) return;
            var iron = PrefabManager.Instance.GetPrefab(ironSourceName);
            var ironData = iron?.GetComponent<ItemDrop>()?.m_itemData;
            if (ironData == null)
            {
                Jotunn.Logger.LogWarning("GraftHelmetItemDrop: iron reference unavailable.");
                return;
            }
            var graft = helmetPrefab.AddComponent<ItemDrop>();
            var fresh = ShallowClone(ironData);
            SetField(fresh, "m_dropPrefab", null);
            typeof(ItemDrop).GetField("m_itemData", CloneFlags)?.SetValue(graft, fresh);
            Jotunn.Logger.LogInfo($"GraftHelmetItemDrop: ItemDrop grafted onto {helmetPrefab.name} (iron data, dropPrefab by name).");
        }

        // Optional Unity-authored carapace helmet (separate file): visual-only
        // prefab (no game assemblies in Editor). Absent file = today's
        // behavior, info-level only. Convention (see UNITY_HELMET_GUIDE.md):
        // object "ChebGonaz_HelmetCarapaceSkeleton" at the asset path below.
        private void LoadUnityHelmetPrefab()
        {
            const string file = "chebgonaz-carapace";
            const string assetSuffix = "chebgonaz_helmetcarapaceskeleton.prefab";
            try
            {
                var full = Path.Combine(Path.GetDirectoryName(Info.Location), file);
                if (!File.Exists(full))
                {
                    Logger.LogInfo("Unity carapace helmet bundle not present (guide: UNITY_HELMET_GUIDE.md).");
                    return;
                }
                var bundle = AssetUtils.LoadAssetBundle(full);
                if (bundle == null)
                {
                    Logger.LogWarning($"Unity carapace helmet bundle rejected (likely built with an incompatible Unity version; use Unity 6000 <= game runtime).");
                    return;
                }
                try
                {
                    // Suffix match instead of an exact internal path: immune to
                    // folder/case differences between Editor and bundle layout.
                    string match = null;
                    foreach (var assetName in bundle.GetAllAssetNames())
                    {
                        if (assetName != null && assetName.EndsWith(assetSuffix, System.StringComparison.OrdinalIgnoreCase))
                        {
                            match = assetName;
                            break;
                        }
                    }
                    if (match == null)
                    {
                        Logger.LogWarning($"Unity carapace helmet (*{assetSuffix}) not found in {file}.");
                        return;
                    }
                    var helmet = bundle.LoadAsset<GameObject>(match);
                    if (helmet == null)
                    {
                        Logger.LogWarning($"Unity carapace helmet found at {match} but failed to load.");
                        return;
                    }
                    GraftHelmetItemDrop(helmet);
                    PrefabManager.Instance.AddPrefab(helmet);
                    EnsureTransplantRegistered(helmet);
                    Logger.LogInfo($"Unity carapace helmet loaded: {helmet.name}.");
                }
                finally
                {
                    bundle.Unload(false);
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Unity carapace helmet load failed ({ex.Message}).");
            }
        }

        private void LoadChebGonazAssetBundle()
        {
            var modDirectory = Path.GetDirectoryName(Info.Location);
            if (modDirectory == null)
            {
                Logger.LogError("Unable to load asset bundle: Failed to get mod directory");
                return;
            }
            
            var assetBundlePath = Path.Combine(modDirectory, "chebgonaz");
            if (!File.Exists(assetBundlePath))
            {
                Logger.LogError($"Unable to load asset bundle: File doesn't exist {assetBundlePath}");
                return;
            }
            
            var chebgonazAssetBundle = AssetUtils.LoadAssetBundle(assetBundlePath);
            try
            {
                #region Images
                var magic1 =  chebgonazAssetBundle.LoadAsset<Texture2D>("magic_01.png");
                var magic2 =  chebgonazAssetBundle.LoadAsset<Texture2D>("magic_02.png");
                OrbOfBeckoning.magic1 = magic1;
                OrbOfBeckoning.magic2 = magic2;
                #endregion
                
                #region SetEffects
                SE_Stats LoadSetEffectFromBundle(string setEffectName, AssetBundle bundle)
                {
                    //Jotunn.Logger.LogInfo($"Loading {setEffectName}...");
                    var seStat = bundle.LoadAsset<SE_Stats>(setEffectName);
                    if (seStat == null)
                    {
                        Jotunn.Logger.LogError($"LoadSetEffectFromBundle: {setEffectName} is null!");
                    }

                    return seStat;
                }

                SetEffectNecromancyArmor = LoadSetEffectFromBundle("SetEffect_NecromancyArmor", chebgonazAssetBundle);
                SetEffectNecromancyArmor2 = LoadSetEffectFromBundle("SetEffect_NecromancyArmor2", chebgonazAssetBundle);
                #endregion
                
                #region Effects
                var effectNames = new List<string>()
                {
                    "sfx_orbofbeckoning_launch"
                };
                foreach (var effectName in effectNames)
                {
                    var prefab = Base.LoadPrefabFromBundle(effectName, chebgonazAssetBundle, RadeonFriendly.Value);
                    PrefabManager.Instance.AddPrefab(prefab);
                }
                #endregion

                #region Items

                // var spectralShroudPrefab = Base.LoadPrefabFromBundle(spectralShroudItem.PrefabName,
                //     chebgonazAssetBundle, RadeonFriendly.Value);
                // ItemManager.Instance.AddItem(spectralShroudItem.GetCustomItemFromPrefab(spectralShroudPrefab));
                spectralShroudMaterial = chebgonazAssetBundle.LoadAsset<Material>("ChebGonaz_SpectralShroud.mat");
                spectralShroudSprite = chebgonazAssetBundle.LoadAsset<Sprite>("chebgonaz_spectralshroud_icon.png");
                
                var necromancersHoodPrefab = Base.LoadPrefabFromBundle(necromancersHoodItem.PrefabName,
                    chebgonazAssetBundle, RadeonFriendly.Value);
                ItemManager.Instance.AddItem(necromancersHoodItem.GetCustomItemFromPrefab(necromancersHoodPrefab));

                var skeletonPriestHoodItem = new SkeletonPriestHoodItem();
                var skeletonPriestHoodPrefab = Base.LoadPrefabFromBundle(skeletonPriestHoodItem.PrefabName,
                    chebgonazAssetBundle, RadeonFriendly.Value);
                ItemManager.Instance.AddItem(skeletonPriestHoodItem.GetCustomItemFromPrefab(skeletonPriestHoodPrefab));

                var skeletonPriestHealItems = new List<Item>()
                {
                    new SkeletonPriestHealLevel1Item(),
                    new SkeletonPriestHealLevel2Item(),
                    new SkeletonPriestHealLevel3Item()
                };
                skeletonPriestHealItems.ForEach(healItem =>
                {
                    var healItemPrefab = Base.LoadPrefabFromBundle(healItem.PrefabName,
                        chebgonazAssetBundle, RadeonFriendly.Value);
                    ItemManager.Instance.AddItem(healItem.GetCustomItemFromPrefab(healItemPrefab));
                });

                NecromancerCape.LoadEmblems(chebgonazAssetBundle);
                
                // _testBowMaterial = chebgonazAssetBundle.LoadAsset<Material>("ChebGonaz_SkeletonBow.mat");

                // Orb of Beckoning
                var orbOfBeckoningProjectilePrefab =
                    Base.LoadPrefabFromBundle(OrbOfBeckoning.ProjectilePrefabName, chebgonazAssetBundle,
                        RadeonFriendly.Value);
                orbOfBeckoningProjectilePrefab.AddComponent<OrbOfBeckoningProjectile>();

                // minion items
                Base.LoadMinionItems(chebgonazAssetBundle, RadeonFriendly.Value);

                // optional Unity-authored carapace helmet (separate bundle file)
                LoadUnityHelmetPrefab();

                wands.ForEach(wand =>
                {
                    // we do the keyhints later after vanilla items are available
                    // so we can override what's in the prefab
                    GameObject wandPrefab;
                    if (wand is CharredWand)
                    {
                        // Model: vanilla StaffRedTroll item. Behavior: Draugr Wand
                        // melee transplanted onto it (the staff natively casts summons).
                        var staffPrefab = PrefabManager.Instance.GetPrefab("StaffRedTroll");
                        var draugrWandPrefab = Base.LoadPrefabFromBundle("ChebGonaz_DraugrWand.prefab",
                            chebgonazAssetBundle, RadeonFriendly.Value);
                        if (staffPrefab == null)
                        {
                            Jotunn.Logger.LogWarning("Charred Wand: StaffRedTroll not found, keeping Draugr visuals.");
                            staffPrefab = draugrWandPrefab;
                        }
                        wandPrefab = PrefabManager.Instance.CreateClonedPrefab(wand.ItemName, staffPrefab);
                        var charredShared = wandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                        var draugrShared = draugrWandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                        charredShared.m_attack = draugrShared.m_attack;
                        charredShared.m_damages = draugrShared.m_damages;
                        charredShared.m_damagesPerLevel = draugrShared.m_damagesPerLevel;
                        charredShared.m_skillType = draugrShared.m_skillType;
                        charredShared.m_maxDurability = draugrShared.m_maxDurability;
                        charredShared.m_durabilityDrain = draugrShared.m_durabilityDrain;
                        charredShared.m_useDurability = draugrShared.m_useDurability;
                        charredShared.m_useDurabilityDrain = draugrShared.m_useDurabilityDrain;
                        charredShared.m_trailStartEffect = draugrShared.m_trailStartEffect;
                        charredShared.m_triggerEffect = draugrShared.m_triggerEffect;
                        charredShared.m_blockEffect = draugrShared.m_blockEffect;
                        CharredWand.ApplyBlueCustomization(wandPrefab);
                    }
                    else
                    {
                        wandPrefab =
                            Base.LoadPrefabFromBundle(wand.PrefabName, chebgonazAssetBundle, RadeonFriendly.Value);
                    }

                    if (wand is SkeletonWand)
                    {
                        // The Skeleton Wand bundle prefab ships with null trail, trigger
                        // and block effects, which throws on every swing and stays silent.
                        // The Draugr Wand has working ones, so borrow them.
                        var draugrWandPrefab = Base.LoadPrefabFromBundle("ChebGonaz_DraugrWand.prefab",
                            chebgonazAssetBundle, RadeonFriendly.Value);
                        var brokenShared = wandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                        var workingShared = draugrWandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                        brokenShared.m_trailStartEffect = workingShared.m_trailStartEffect;
                        brokenShared.m_triggerEffect = workingShared.m_triggerEffect;
                        brokenShared.m_blockEffect = workingShared.m_blockEffect;
                    }
                    wand.CreateButtons();
                    KeyHintManager.Instance.AddKeyHint(wand.GetKeyHint());

                    // for orb of beckoning, make sure the custom projectile is set
                    if (wand is OrbOfBeckoning)
                    {
                        wandPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack.m_attackProjectile =
                            orbOfBeckoningProjectilePrefab;
                    }
                    
                    ItemManager.Instance.AddItem(wand.GetCustomItemFromPrefab(wandPrefab));
                });

                #endregion

                #region CustomPrefabs

                var largeCargoCratePrefab = Base.LoadPrefabFromBundle(LargeCargoCrate.PrefabName,
                    chebgonazAssetBundle, RadeonFriendly.Value);
                PrefabManager.Instance.AddPrefab(new CustomPrefab(largeCargoCratePrefab, false));

                #endregion

                #region Creatures
                
                UndeadMinion.LoadEyes(chebgonazAssetBundle);
                SkeletonMinion.LoadBoneColors(chebgonazAssetBundle);

                List<string> prefabNames = new();

                foreach (DraugrMinion.DraugrType value in Enum.GetValues(typeof(DraugrMinion.DraugrType)))
                {
                    if (value is DraugrMinion.DraugrType.None) continue;
                    prefabNames.Add(InternalName.GetName(value) + ".prefab");
                }

                // 1.2.0: I had to make extra prefabs for each tier because
                // the skeletons consistently forgot their weapons and became
                // buggy (not attacking enemies) if dynamically set
                foreach (SkeletonMinion.SkeletonType value in Enum.GetValues(typeof(SkeletonMinion.SkeletonType)))
                {
                    if (value is SkeletonMinion.SkeletonType.None) continue;
                    prefabNames.Add(InternalName.GetName(value) + ".prefab");
                }

                prefabNames.Add("ChebGonaz_GuardianWraith.prefab");
                //prefabNames.Add(SpiritPylonGhostMinion.PrefabName + ".prefab");
                prefabNames.Add("ChebGonaz_NeckroGatherer.prefab");
                prefabNames.Add("ChebGonaz_Bat.prefab");
                prefabNames.Add(BattleNeckroMinion.PrefabName + ".prefab");

                foreach (LeechMinion.LeechType value in Enum.GetValues(typeof(LeechMinion.LeechType)))
                {
                    if (value is LeechMinion.LeechType.None) continue;
                    prefabNames.Add(InternalName.GetName(value) + ".prefab");
                }

                prefabNames.ForEach(prefabName =>
                {
                    if (HeavyLogging.Value) Logger.LogInfo($"Loading {prefabName} from asset bundle...");
                    var prefab = Base.LoadPrefabFromBundle(prefabName, chebgonazAssetBundle, 
                        RadeonFriendly.Value
                        || NoWraithSmoke.Value && prefabName.Equals("ChebGonaz_GuardianWraith.prefab"));
                    switch (prefabName)
                    {
                        case "ChebGonaz_DraugrWarrior.prefab":
                        case "ChebGonaz_DraugrWarriorTier2.prefab":
                        case "ChebGonaz_DraugrWarriorTier3.prefab":
                        case "ChebGonaz_DraugrWarriorTier4.prefab":
                        case "ChebGonaz_DraugrWarriorNeedle.prefab":
                            prefab.AddComponent<DraugrWarriorMinion>();
                            break;
                        case "ChebGonaz_DraugrArcher.prefab":
                            prefab.AddComponent<DraugrArcherTier1Minion>();
                            break;
                        case "ChebGonaz_DraugrArcherTier2.prefab":
                            prefab.AddComponent<DraugrArcherTier2Minion>();
                            break;
                        case "ChebGonaz_DraugrArcherTier3.prefab":
                            prefab.AddComponent<DraugrArcherTier3Minion>();
                            break;
                        case "ChebGonaz_DraugrArcherPoison.prefab":
                            prefab.AddComponent<DraugrArcherPoisonMinion>();
                            break;
                        case "ChebGonaz_DraugrArcherFire.prefab":
                            prefab.AddComponent<DraugrArcherFireMinion>();
                            break;
                        case "ChebGonaz_DraugrArcherFrost.prefab":
                            prefab.AddComponent<DraugrArcherFrostMinion>();
                            break;
                        case "ChebGonaz_DraugrArcherSilver.prefab":
                            prefab.AddComponent<DraugrArcherSilverMinion>();
                            break;
                        case "ChebGonaz_SkeletonWarrior.prefab":
                        case "ChebGonaz_SkeletonWarriorTier2.prefab":
                        case "ChebGonaz_SkeletonWarriorTier3.prefab":
                        case "ChebGonaz_SkeletonWarriorTier4.prefab":
                        case "ChebGonaz_SkeletonWarriorNeedle.prefab":
                            prefab.AddComponent<SkeletonWarriorMinion>();
                            break;
                        case "ChebGonaz_SkeletonArcher.prefab":
                            prefab.AddComponent<SkeletonArcherTier1Minion>();
                            break;
                        case "ChebGonaz_SkeletonArcherTier2.prefab":
                            prefab.AddComponent<SkeletonArcherTier2Minion>();
                            break;
                        case "ChebGonaz_SkeletonArcherTier3.prefab":
                            prefab.AddComponent<SkeletonArcherTier3Minion>();
                            break;
                        case "ChebGonaz_SkeletonArcherPoison.prefab":
                            prefab.AddComponent<SkeletonArcherPoisonMinion>();
                            break;
                        case "ChebGonaz_SkeletonArcherFire.prefab":
                            prefab.AddComponent<SkeletonArcherFireMinion>();
                            break;
                        case "ChebGonaz_SkeletonArcherFrost.prefab":
                            prefab.AddComponent<SkeletonArcherFrostMinion>();
                            break;
                        case "ChebGonaz_SkeletonArcherSilver.prefab":
                            prefab.AddComponent<SkeletonArcherSilverMinion>();
                            break;
                        case "ChebGonaz_SkeletonMage.prefab":
                        case "ChebGonaz_SkeletonMageTier2.prefab":
                        case "ChebGonaz_SkeletonMageTier3.prefab":
                            prefab.AddComponent<SkeletonMageMinion>();
                            break;
                        case "ChebGonaz_PoisonSkeleton.prefab":
                        case "ChebGonaz_PoisonSkeleton2.prefab":
                        case "ChebGonaz_PoisonSkeleton3.prefab":
                            prefab.AddComponent<PoisonSkeletonMinion>();
                            break;
                        case "ChebGonaz_SkeletonWoodcutter.prefab":
                            prefab.AddComponent<SkeletonWoodcutterMinion>();
                            break;
                        case "ChebGonaz_SkeletonMiner.prefab":
                            prefab.AddComponent<SkeletonMinerMinion>();
                            break;
                        case "ChebGonaz_GuardianWraith.prefab":
                            prefab.AddComponent<GuardianWraithMinion>();
                            break;
                        case "ChebGonaz_SpiritPylonGhost.prefab":
                            prefab.AddComponent<SpiritPylonGhostMinion>();
                            break;
                        case "ChebGonaz_NeckroGatherer.prefab":
                            prefab.AddComponent<NeckroGathererMinion>();
                            break;
                        case "ChebGonaz_Bat.prefab":
                            prefab.AddComponent<BatBeaconBatMinion>();
                            break;
                        case "ChebGonaz_BattleNeckro.prefab":
                            prefab.AddComponent<BattleNeckroMinion>();
                            break;
                        case "ChebGonaz_Leech.prefab":
                            prefab.AddComponent<LeechMinion>();
                            break;
                        case "ChebGonaz_SkeletonPriest.prefab":
                        case "ChebGonaz_SkeletonPriestTier2.prefab":
                        case "ChebGonaz_SkeletonPriestTier3.prefab":
                            prefab.AddComponent<SkeletonPriestMinion>();
                            break;
                        default:
                            Logger.LogError($"Unknown prefab {prefabName}");
                            break;
                    }

                    CreatureManager.Instance.AddCreature(new CustomCreature(prefab, true));
                });

                // Diagnostic first (factory state, read-only), swap second: the log
                // always shows what each prefab shipped with before we touch it.
                DumpWarriorWeaponFields();

                // Carapace helmet for carapace wearers: runtime mesh transplant
                // (see method). Registers ChebGonaz_HelmetCarapaceSkeleton, the
                // config default, so no config change is needed. Any failure
                // keeps today's iron fallback with a warning in the log.
                // SUSPENDED 2026-10-14: carapace spawns abort with NREs inside
                // GiveDefaultItem/EquipItem and the clone is the only unproven
                // object in that loadout (everything else is vanilla- or
                // bundle-hardened). Neutralized until forensics clear it;
                // ResolveCarapaceHelmetPrefab falls back to iron meanwhile.
                // BuildCarapaceHelmetClone();

                // WarriorTier4 (BlackMetal/Caparazon tier): wield the configured
                // vanilla weapons directly (visible, engine-healthy instances)
                // plus a per-instance animation rewrite (see
                // EnsureSkeletonWeaponAnim): vanilla "swing_longsword/axe"
                // states don't exist on the skeleton rig, so each wielded
                // instance gets m_attackAnimation -> "attack", chainLevels -> 0
                // (like every custom weapon). Shared vanilla prefabs are never
                // mutated. Blank config keeps the bundle customs.
                try
                {
                    var tier4Prefab = PrefabManager.Instance.GetPrefab("ChebGonaz_SkeletonWarriorTier4");
                    var tier4Humanoid = tier4Prefab?.GetComponent<Humanoid>();
                    var wantedWeapons = (WarriorTier4WeaponsConfig.Value ?? "")
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0)
                        .ToList();
                    if (tier4Prefab != null && tier4Humanoid != null && wantedWeapons.Count > 0)
                    {
                        var resolvedWeapons = wantedWeapons
                            .Select(n => PrefabManager.Instance.GetPrefab(n))
                            .Where(p => p != null)
                            .ToList();
                        var missingWeapons = wantedWeapons
                            .Where(n => PrefabManager.Instance.GetPrefab(n) == null)
                            .ToList();
                        foreach (var missing in missingWeapons)
                            Logger.LogWarning($"WarriorTier4Weapons: vanilla prefab '{missing}' not found, skipping.");
                        // Baseline for the no-leak proof: vanilla anims must still
                        // read swing_* after minions patch their own instances.
                        VanillaWeaponAnimBaseline.Clear();
                        foreach (var resolved in resolvedWeapons)
                        {
                            var shared = resolved.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                            VanillaWeaponAnimBaseline[resolved.name] = shared?.m_attack?.m_attackAnimation ?? "null";
                        }
                        if (resolvedWeapons.Count > 0)
                        {
                            tier4Humanoid.m_randomWeapon = resolvedWeapons.ToArray();
                            Logger.LogInfo($"WarriorTier4 weapons -> vanilla: {string.Join(",", resolvedWeapons.Select(p => p.name))}.");
                        }
                        else
                        {
                            Logger.LogWarning("WarriorTier4Weapons: no vanilla weapon resolved, keeping bundle custom weapons.");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"WarriorTier4Weapons patch failed, keeping bundle weapons ({ex.Message}).");
                }

                // WarriorTier4 shield: blackmetal buckler instead of the wooden
                // one, keeping the 50/50 shield-or-nothing roll. Shields need
                // no animation rewrite. Blank config keeps the wooden shield.
                try
                {
                    var tier4ForShield = PrefabManager.Instance.GetPrefab("ChebGonaz_SkeletonWarriorTier4");
                    var tier4ShieldHumanoid = tier4ForShield?.GetComponent<Humanoid>();
                    var wantedShield = (WarriorTier4ShieldConfig.Value ?? "").Trim();
                    if (tier4ForShield != null && tier4ShieldHumanoid != null && wantedShield.Length > 0)
                    {
                        var shieldPrefab = PrefabManager.Instance.GetPrefab(wantedShield);
                        if (shieldPrefab == null)
                        {
                            Logger.LogWarning($"WarriorTier4Shield: vanilla prefab '{wantedShield}' not found, keeping wooden shield.");
                        }
                        else
                        {
                            tier4ShieldHumanoid.m_randomShield = new[] { shieldPrefab, null };
                            Logger.LogInfo($"WarriorTier4 shield -> {shieldPrefab.name} (50/50 roll kept).");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"WarriorTier4Shield patch failed, keeping wooden shield ({ex.Message}).");
                }

                #endregion

                #region Structures

                var spiritPylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(SpiritPylon.ChebsRecipeConfig.PrefabName);
                spiritPylonPrefab.AddComponent<SpiritPylon>();
                PieceManager.Instance.AddPiece(
                    SpiritPylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(spiritPylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(SpiritPylon.ChebsRecipeConfig.IconName))
                );

                var refuelerPylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(RefuelerPylon.ChebsRecipeConfig.PrefabName);
                refuelerPylonPrefab.AddComponent<RefuelerPylon>();
                PieceManager.Instance.AddPiece(
                    RefuelerPylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(refuelerPylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(RefuelerPylon.ChebsRecipeConfig.IconName))
                );

                var neckroGathererPylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(NeckroGathererPylon.ChebsRecipeConfig.PrefabName);
                neckroGathererPylonPrefab.AddComponent<NeckroGathererPylon>();
                PieceManager.Instance.AddPiece(
                    NeckroGathererPylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(neckroGathererPylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(NeckroGathererPylon.ChebsRecipeConfig.IconName))
                );

                var batBeaconPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(BatBeacon.ChebsRecipeConfig.PrefabName);
                batBeaconPrefab.AddComponent<BatBeacon>();
                PieceManager.Instance.AddPiece(
                    BatBeacon.ChebsRecipeConfig.GetCustomPieceFromPrefab(batBeaconPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(BatBeacon.ChebsRecipeConfig.IconName))
                );

                var batLanternPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(BatLantern.ChebsRecipeConfig.PrefabName);
                batLanternPrefab.AddComponent<BatLantern>();
                PieceManager.Instance.AddPiece(
                    BatLantern.ChebsRecipeConfig.GetCustomPieceFromPrefab(batLanternPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(BatLantern.ChebsRecipeConfig.IconName))
                );

                var farmingPylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(FarmingPylon.ChebsRecipeConfig.PrefabName);
                farmingPylonPrefab.AddComponent<FarmingPylon>();
                PieceManager.Instance.AddPiece(
                    FarmingPylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(farmingPylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(FarmingPylon.ChebsRecipeConfig.IconName))
                );

                var repairPylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(RepairPylon.ChebsRecipeConfig.PrefabName);
                repairPylonPrefab.AddComponent<RepairPylon>();
                PieceManager.Instance.AddPiece(
                    RepairPylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(repairPylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(RepairPylon.ChebsRecipeConfig.IconName))
                );

                var treasurePylonPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(TreasurePylon.ChebsRecipeConfig.PrefabName);
                treasurePylonPrefab.AddComponent<TreasurePylon>();
                PieceManager.Instance.AddPiece(
                    TreasurePylon.ChebsRecipeConfig.GetCustomPieceFromPrefab(treasurePylonPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(TreasurePylon.ChebsRecipeConfig.IconName))
                );
                var treasurePylonEffectPrefab = chebgonazAssetBundle.LoadAsset<GameObject>(TreasurePylon.EffectName);
                PrefabManager.Instance.AddPrefab(treasurePylonEffectPrefab);
                
                var phylacteryPrefab =
                    chebgonazAssetBundle.LoadAsset<GameObject>(Phylactery.ChebsRecipeConfig.PrefabName);
                phylacteryPrefab.AddComponent<Phylactery>();
                PieceManager.Instance.AddPiece(
                    Phylactery.ChebsRecipeConfig.GetCustomPieceFromPrefab(phylacteryPrefab,
                        chebgonazAssetBundle.LoadAsset<Sprite>(Phylactery.ChebsRecipeConfig.IconName))
                );

                #endregion

                #region Skills

                var iconSprite = chebgonazAssetBundle.LoadAsset<Sprite>("necromancy_icon.png");
                AddNecromancy(iconSprite);

                #endregion
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while loading assets: {ex}");
            }
            finally
            {
                chebgonazAssetBundle.Unload(false);
            }
        }

        private void AddNecromancy(Sprite iconSprite)
        {
            SkillConfig skill = new()
            {
                Name = "$friendlyskeletonwand_necromancy",
                Description = "$friendlyskeletonwand_necromancy_desc",
                Icon = iconSprite,
                Identifier = NecromancySkillIdentifier
            };

            SkillManager.Instance.AddSkill(skill);

            // necromancy skill doesn't exist until mod is loaded, so we have to set it here rather than in unity
            SetEffectNecromancyArmor.m_skillLevel = SkillManager.Instance.GetSkill(NecromancySkillIdentifier).m_skill;
            SetEffectNecromancyArmor.m_skillLevelModifier = SpectralShroud.NecromancySkillBonus.Value;

            SetEffectNecromancyArmor2.m_skillLevel = SkillManager.Instance.GetSkill(NecromancySkillIdentifier).m_skill;
            SetEffectNecromancyArmor2.m_skillLevelModifier = NecromancerHood.NecromancySkillBonus.Value;

            SetEffectNecromancyArmor3 = DarkNecromancerHood.CreateSetEffect();
            SetEffectNecromancyArmor3.m_skillLevel = SkillManager.Instance.GetSkill(NecromancySkillIdentifier).m_skill;
            SetEffectNecromancyArmor3.m_skillLevelModifier = DarkNecromancerHood.NecromancySkillBonus.Value;

            SetEffectNecromancyArmor4 = ShadowedAshCape.CreateSetEffect();
            SetEffectNecromancyArmor4.m_skillLevel = SkillManager.Instance.GetSkill(NecromancySkillIdentifier).m_skill;
            SetEffectNecromancyArmor4.m_skillLevelModifier = ShadowedAshCape.NecromancySkillBonus.Value;
        }
        
        private void Update()
        {
            if (ZInput.instance != null)
            {
                if (Time.time > inputDelay)
                {
                    wands.ForEach(wand =>
                    {
                        if (wand.HandleInputs())
                        {
                            inputDelay = Time.time + .5f;
                        }
                    });
                }

                if (OptionsGUI.OptionsButton != null && ZInput.GetButtonUp(OptionsGUI.OptionsButton.Name))
                {
                    OptionsGUI.TogglePanel();
                }
                
                if (!_chebsMercenariesActive && PvPOptionsGUI.OptionsButton != null 
                                             && ZInput.GetButtonUp(PvPOptionsGUI.OptionsButton.Name))
                {
                    PvPOptionsGUI.TogglePanel();
                }
            }

            spectralShroudItem.DoOnUpdate();
        }
    }
}
