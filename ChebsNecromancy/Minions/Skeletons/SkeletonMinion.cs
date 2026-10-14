using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using ChebsNecromancy.Items.Armor.Player;
using ChebsNecromancy.Items.Wands;
using ChebsValheimLibrary.Common;
using ChebsValheimLibrary.Minions;
using Jotunn;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Random = UnityEngine.Random;

namespace ChebsNecromancy.Minions.Skeletons
{
    public class SkeletonMinion : UndeadMinion
    {
        public enum SkeletonType
        {
            None,
            [InternalName("ChebGonaz_SkeletonWarrior")] WarriorTier1,
            [InternalName("ChebGonaz_SkeletonWarriorTier2")] WarriorTier2,
            [InternalName("ChebGonaz_SkeletonWarriorTier3")] WarriorTier3,
            [InternalName("ChebGonaz_SkeletonWarriorTier4")] WarriorTier4,
            [InternalName("ChebGonaz_SkeletonArcher")] ArcherTier1,
            [InternalName("ChebGonaz_SkeletonArcherTier2")] ArcherTier2,
            [InternalName("ChebGonaz_SkeletonArcherTier3")] ArcherTier3,
            [InternalName("ChebGonaz_SkeletonArcherPoison")] ArcherPoison,
            [InternalName("ChebGonaz_SkeletonArcherFire")] ArcherFire,
            [InternalName("ChebGonaz_SkeletonArcherFrost")] ArcherFrost,
            [InternalName("ChebGonaz_SkeletonArcherSilver")] ArcherSilver,
            [InternalName("ChebGonaz_SkeletonMage")] MageTier1,
            [InternalName("ChebGonaz_SkeletonMageTier2")] MageTier2,
            [InternalName("ChebGonaz_SkeletonMageTier3")] MageTier3,
            [InternalName("ChebGonaz_PoisonSkeleton")] PoisonTier1,
            [InternalName("ChebGonaz_PoisonSkeleton2")] PoisonTier2,
            [InternalName("ChebGonaz_PoisonSkeleton3")] PoisonTier3,
            [InternalName("ChebGonaz_SkeletonWoodcutter")] Woodcutter,
            [InternalName("ChebGonaz_SkeletonMiner")] Miner,
            [InternalName("ChebGonaz_SkeletonWarriorNeedle")] WarriorNeedle,
            [InternalName("ChebGonaz_SkeletonPriest")] PriestTier1,
            [InternalName("ChebGonaz_SkeletonPriestTier2")] PriestTier2,
            [InternalName("ChebGonaz_SkeletonPriestTier3")] PriestTier3,
        };

        private static List<int> _hashList;

        public static bool IsSkeletonHash(int hash)
        {
            if (_hashList == null)
            {
                _hashList = new List<int>();
                foreach (SkeletonType value in Enum.GetValues(typeof(SkeletonType)))
                {
                    _hashList.Add(InternalName.GetName(value).GetStableHashCode());
                }
            }

            return _hashList.Contains(hash);
        }

        // for limits checking
        private static int _createdOrderIncrementer;

        public static ConfigEntry<float> SkeletonBaseHealth;
        public static ConfigEntry<float> SkeletonHealthMultiplier;

        public static ConfigEntry<float> NecromancyLevelIncrease;
        public static ConfigEntry<float> PoisonNecromancyLevelIncrease;
        public static ConfigEntry<float> ArcherNecromancyLevelIncrease;
        public static ConfigEntry<float> MageNecromancyLevelIncrease;
        
        public static ConfigEntry<int> MaxSkeletons;
        public static ConfigEntry<int> MinionLimitIncrementsEveryXLevels;
        
        public const string BoneColorZdoKey = "SkeletonMinionBoneColor";
        public const string CarapaceArmorZdoKey = "ChebGonaz_CarapaceArmor";
        public static Dictionary<string, Material> Bones = new();

        public static int PlayerBoneColorZdoKeyHash => "ChebGonazBoneColorSetting".GetStableHashCode();

        public new static void CreateConfigs(BaseUnityPlugin plugin)
        {
            const string client = "SkeletonMinion (Client)";
            const string serverSynced = "SkeletonMinion (Server Synced)";
            SkeletonBaseHealth = plugin.Config.Bind(serverSynced, "SkeletonBaseHealth",
                20f, new ConfigDescription("HP = BaseHealth + NecromancyLevel * HealthMultiplier", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkeletonHealthMultiplier = plugin.Config.Bind(serverSynced, "SkeletonHealthMultiplier",
                1.25f, new ConfigDescription("HP = BaseHealth + NecromancyLevel * HealthMultiplier", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NecromancyLevelIncrease = plugin.Config.Bind(serverSynced, 
                "NecromancyLevelIncrease",
                .75f, new ConfigDescription(
                    "How much crafting a skeleton contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            PoisonNecromancyLevelIncrease = plugin.Config.Bind(serverSynced,
                "PoisonSkeletonNecromancyLevelIncrease",
                1f, new ConfigDescription(
                    "How much crafting a Poison Skeleton contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            ArcherNecromancyLevelIncrease = plugin.Config.Bind(serverSynced,
                "ArcherSkeletonNecromancyLevelIncrease",
                1f, new ConfigDescription(
                    "How much crafting an Archer Skeleton contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            MageNecromancyLevelIncrease = plugin.Config.Bind(serverSynced,
                "MageSkeletonNecromancyLevelIncrease",
                1f, new ConfigDescription(
                    "How much crafting a Poison Skeleton contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            MaxSkeletons = plugin.Config.Bind(serverSynced, "MaximumSkeletons",
                0, new ConfigDescription("The maximum amount of skeletons that can be made (0 = unlimited).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            
            MinionLimitIncrementsEveryXLevels = plugin.Config.Bind(serverSynced,
                "MinionLimitIncrementsEveryXLevels",
                10, new ConfigDescription(
                    "Attention: has no effect if minion limits are off. Increases player's maximum minion count by 1 every X levels. For example, if the limit is 3 skeletons and this is set to 10, then at level 10 Necromancy the player can have 4 minions. Then 5 at level 20, and so on.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
        
        #region BoneColor

        public enum BoneColor
        {
            [InternalName("ChebGonaz_SkeletonWhite")]
            White,

            [InternalName("ChebGonaz_SkeletonRed")]
            Red,
            
            [InternalName("ChebGonaz_SkeletonBlue")]
            Blue,

            [InternalName("ChebGonaz_SkeletonGreen")]
            Green,
            
            [InternalName("ChebGonaz_SkeletonBlack")]
            Black,
            
            [InternalName("ChebGonaz_SkeletonDark")]
            Dark,
        }

        public string SkeletonBoneColor
        {
            get
            {
                if (TryGetComponent(out ZNetView zNetView))
                {
                    var matName = zNetView.GetZDO().GetString(BoneColorZdoKey);
                    if (string.IsNullOrEmpty(matName) || string.IsNullOrWhiteSpace(matName))
                        zNetView.GetZDO().Set(BoneColorZdoKey, matName);
                    return matName;
                }
                Logger.LogError("Cannot get bone color because minion has no ZNetView component.");
                return InternalName.GetName(BoneColor.White);
            }
            set
            {
                if (TryGetComponent(out ZNetView zNetView))
                {
                    zNetView.GetZDO().Set(BoneColorZdoKey, value);
                }
                else
                {
                    Logger.LogError($"Cannot set bone color to {value} because it has no ZNetView component.");
                }
            }
        }

        // Persists the carapace summon across relogs (same pattern as
        // CharredMinion's Protector flag): name-based VisEquipment restore
        // can't find the unregistered runtime helmet clone, so WaitForZNet
        // re-equips it from the live template when this is set.
        public void SetIsCarapaceArmor(bool isCarapace)
        {
            if (!TryGetComponent(out ZNetView zNetView))
            {
                Logger.LogError($"Cannot SetIsCarapaceArmor to {isCarapace} because it has no ZNetView component.");
                return;
            }
            zNetView.GetZDO().Set(CarapaceArmorZdoKey, isCarapace);
        }

        public bool IsCarapaceArmor()
        {
            if (!TryGetComponent(out ZNetView zNetView)) return false;
            return zNetView.GetZDO().GetBool(CarapaceArmorZdoKey, false);
        }

        public static void LoadBoneColors(AssetBundle bundle)
        {
            foreach (BoneColor boneColor in Enum.GetValues(typeof(BoneColor)))
            {
                var name = InternalName.GetName(boneColor);
                var mat = bundle.LoadAsset<Material>(name + ".mat");
                if (mat == null) Logger.LogError($"mat for {name} is null!");
                Bones[name] = mat;
            }
        }
        
        public void LoadBoneColorMaterial()
        {
            var boneColor = SkeletonBoneColor;
            if (Bones.TryGetValue(boneColor, out var boneMat))
            {
                var visualSkeleton = transform.Find("Visual/_skeleton_base/Skeleton");
                if (visualSkeleton != null && visualSkeleton.TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer))
                {
                    skinnedMeshRenderer.sharedMaterial = boneMat;
                }
                else
                {
                    Logger.LogError($"{name} (visualSkeleton={visualSkeleton}) Failed to get SkinnedMeshRenderer");
                }
            }
            else
            {
                Logger.LogError($"{name} failed to load bone color: {boneColor} not in list");
            }
        }

        public static void SetBoneColor(BoneColor boneColor)
        {
            Logger.LogInfo($"Changing bone color to {boneColor}, updating minion materials...");
            var player = Player.m_localPlayer;
            if (player == null)
            {
                Logger.LogInfo("Failed to update minion bones: m_localPlayer is null. This is not an " +
                               "error unless you're in-game right now & just means that bones " +
                               "couldn't be updated on existing minions at this moment in time.");
                return;
            }
            
            player.m_nview.GetZDO().Set(PlayerBoneColorZdoKeyHash, (int)boneColor);

            var matName = InternalName.GetName(boneColor);
            var minionsBelongingToPlayer = ZDOMan.instance.m_objectsByID
                .Values
                .ToList()
                .FindAll(zdo =>
                {
                    var zdoPrefab = zdo.GetPrefab();
                    return IsSkeletonHash(zdoPrefab);
                })
                .Where(zdo =>
                    zdo.GetString(MinionOwnershipZdoKey) ==
                    player.GetPlayerName())
                .ToList();
            Logger.LogInfo($"Found {minionsBelongingToPlayer.Count} to update...");
            foreach (var zdo in minionsBelongingToPlayer)
            {
                zdo.Set(BoneColorZdoKey, matName);
            }

            // now that ZDOs have been set, update loaded minions
            var allCharacters = Character.GetAllCharacters();
            foreach (var character in allCharacters)
            {
                if (character.IsDead())
                {
                    continue;
                }

                var minion = character.GetComponent<SkeletonMinion>();
                if (minion == null || !minion.BelongsToPlayer(player.GetPlayerName())) continue;
                minion.LoadBoneColorMaterial();
            }
        }
        
        #endregion

        // Carapace dressing never resolves by name at load (unregistered
        // runtime transplants) and vanilla carapace NREs VisEquipment attach,
        // so the reapply loop below skips these and the carapace flag block
        // after it dresses from live templates instead. Old saves heal on
        // load instead of NRE-spamming.
        private static readonly System.Collections.Generic.HashSet<string> CarapaceItemNames =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                "ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetCarapace",
                "ChebGonaz_HelmetCarapaceSkeleton",
                "ChebGonaz_ArmorCarapaceChest", "ChebGonaz_ArmorCarapaceLegs"
            };

        public override void Awake()
        {
            base.Awake();

            _createdOrderIncrementer++;
            createdOrder = _createdOrderIncrementer;

            StartCoroutine(WaitForZNet());
        }

        IEnumerator WaitForZNet()
        {
            yield return new WaitUntil(() => ZNetScene.instance != null);
            
            ScaleStats(GetCreatedAtLevel());
            
            if (!TryGetComponent(out Humanoid humanoid))
            {
                Logger.LogError("Humanoid component missing!");
                yield break;
            }

            // VisEquipment remembers what armor the skeleton is wearing.
            // Exploit this to reapply the armor so the armor values work
            // again.
            var equipmentHashes = new List<int>()
            {
                humanoid.m_visEquipment.m_currentChestItemHash,
                humanoid.m_visEquipment.m_currentLegItemHash,
                humanoid.m_visEquipment.m_currentHelmetItemHash,
            };
            equipmentHashes.ForEach(hash =>
            {
                var equipmentPrefab = ZNetScene.instance.GetPrefab(hash);
                if (equipmentPrefab != null)
                {
                    if (IsCarapaceArmor() && CarapaceItemNames.Contains(equipmentPrefab.name))
                    {
                        if (BasePlugin.WarriorTier4WeaponDiag.Value)
                            Jotunn.Logger.LogInfo($"[WeaponDiag] reapply {name}: {equipmentPrefab.name} skipped, dressed from live templates below.");
                        return;
                    }
                    if (BasePlugin.WarriorTier4WeaponDiag.Value && name.Contains("Tier4"))
                        Jotunn.Logger.LogInfo($"[WeaponDiag] reapply {name}: hash {hash} -> {equipmentPrefab.name}");
                    humanoid.GiveDefaultItem(equipmentPrefab);
                }
                else if (BasePlugin.WarriorTier4WeaponDiag.Value && name.Contains("Tier4"))
                {
                    Jotunn.Logger.LogInfo($"[WeaponDiag] reapply {name}: hash {hash} resolved null, skipped.");
                }
            });

            // Carapace helmet is an unregistered runtime clone: name restore
            // above can't find it, so re-equip from the live template here.
            // Covers every relog; harmless overwrite when already equipped.
            // Chest/legs ride along: same unregistered-transplant situation.
            if (IsCarapaceArmor())
            {
                var cloneTemplate = ResolveCarapaceHelmetPrefab();
                if (cloneTemplate != null) humanoid.GiveDefaultItem(cloneTemplate);
                var chestTemplate = BasePlugin.GetCarapaceArmorPiece("chest");
                if (chestTemplate != null) humanoid.GiveDefaultItem(chestTemplate);
                var legsTemplate = BasePlugin.GetCarapaceArmorPiece("legs");
                if (legsTemplate != null) humanoid.GiveDefaultItem(legsTemplate);
                if (cloneTemplate != null && cloneTemplate.name.Equals("ChebGonaz_HelmetCarapaceSkeleton", System.StringComparison.OrdinalIgnoreCase))
                    AdjustUnityHelmet();
            }

            LoadEmblemMaterial(humanoid);
            LoadEyeMaterial();
            LoadBoneColorMaterial();

            RestoreDrops();

            // Relog proof for vanilla weapons: after a reload the weapon must
            // resolve with skeleton-compatible animation. Tier4-only, diag-gated.
            if (BasePlugin.WarriorTier4WeaponDiag.Value && name.Contains("Tier4"))
            {
                var awakeWeapon = humanoid.GetCurrentWeapon();
                Jotunn.Logger.LogInfo($"[WeaponDiag] awake {name}: weapon=[{(awakeWeapon?.m_shared != null ? awakeWeapon.m_shared.m_name : "null")}]");
            }

            // Tier4 blackmetal path only: rewrite the wielded vanilla weapon's
            // attack hookup once it equips (async, ~0.6s after spawn). Customs
            // and other tiers are untouched.
            if (name.Contains("Tier4")) StartCoroutine(EnsureSkeletonWeaponAnim());

            // wondering what the code below does? Check comments in the
            // FreshMinion.cs file.
            var freshMinion = GetComponent<FreshMinion>();
            var monsterAI = GetComponent<MonsterAI>();
            monsterAI.m_randomMoveRange = RoamRange.Value;
            if (!Wand.FollowByDefault.Value || freshMinion == null)
            {
                yield return new WaitUntil(() => Player.m_localPlayer != null);
                
                RoamFollowOrWait();
            }

            if (freshMinion != null)
            {
                // remove the component
                Destroy(freshMinion);
            }
        }

        public void LoadEmblemMaterial(Humanoid humanoid)
        {
            if (string.IsNullOrEmpty(Emblem)) return;
            
            var shoulderHash = humanoid.m_visEquipment.m_currentShoulderItemHash;
            var shoulderPrefab = ZNetScene.instance.GetPrefab(shoulderHash);
            if (shoulderPrefab != null
                && shoulderPrefab.TryGetComponent(out ItemDrop itemDrop)
                && itemDrop.name.Equals("CapeLox"))
            {
                var emblem = Emblem;
                if (Emblem.Contains(emblem))
                {
                    var material = NecromancerCape.Emblems[Emblem];
                    humanoid.m_visEquipment.m_shoulderItemInstances.ForEach(g => 
                        g.GetComponentsInChildren<SkinnedMeshRenderer>().ToList().ForEach(m =>
                        {
                            var mats = m.materials;
                            for (int i = 0; i < mats.Length; i++)
                            {
                                mats[i] = material;
                            }
                            m.materials = mats;
                        })
                    );   
                }
            }
        }

        // Same pattern as CharredMinion.ApplyBonusResistances, just scoped to the
        // Mistlands/Carapace-armored Skeleton Warrior via its own separate config.
        private void ApplyMistlandsResistances()
        {
            var character = GetComponent<Character>();
            if (character == null || string.IsNullOrWhiteSpace(BasePlugin.MistlandsBonusResistancesConfig.Value)) return;

            foreach (var entry in BasePlugin.MistlandsBonusResistancesConfig.Value.Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;
                try
                {
                    var type = (HitData.DamageType)System.Enum.Parse(typeof(HitData.DamageType), parts[0].Trim(), true);
                    var modifier = (HitData.DamageModifier)System.Enum.Parse(typeof(HitData.DamageModifier), parts[1].Trim(), true);
                    var mods = character.m_damageModifiers;
                    switch (type)
                    {
                        case HitData.DamageType.Blunt: mods.m_blunt = modifier; break;
                        case HitData.DamageType.Slash: mods.m_slash = modifier; break;
                        case HitData.DamageType.Pierce: mods.m_pierce = modifier; break;
                        case HitData.DamageType.Chop: mods.m_chop = modifier; break;
                        case HitData.DamageType.Pickaxe: mods.m_pickaxe = modifier; break;
                        case HitData.DamageType.Fire: mods.m_fire = modifier; break;
                        case HitData.DamageType.Frost: mods.m_frost = modifier; break;
                        case HitData.DamageType.Lightning: mods.m_lightning = modifier; break;
                        case HitData.DamageType.Poison: mods.m_poison = modifier; break;
                        case HitData.DamageType.Spirit: mods.m_spirit = modifier; break;
                    }
                }
                catch (System.Exception)
                {
                    Logger.LogWarning($"SkeletonMinion: ignoring malformed Mistlands resistance entry '{entry.Trim()}'.");
                }
            }
        }

        public virtual void ScaleStats(float necromancyLevel)
        {
            var character = GetComponent<Character>();
            if (character == null)
            {
                Logger.LogError("ScaleStats: Character component is null!");
                return;
            }

            var health = SkeletonBaseHealth.Value + necromancyLevel * SkeletonHealthMultiplier.Value;
            character.SetMaxHealth(health);
            character.SetHealth(health);
        }

        // isMistlandsArmor defaults to false so every existing caller (DraugrMinion
        // doesn't call this one, but OrbOfBeckoning does) keeps working unchanged.
        // Only SkeletonWand's Warrior branch ever passes true.
        public virtual void ScaleEquipment(float necromancyLevel, SkeletonType skeletonType, ArmorType armorType,
            bool isMistlandsArmor = false)
        {
            var defaultItems = new List<GameObject>();
            var needsCarapaceHelmetScaleFix = false;

            var humanoid = GetComponent<Humanoid>();
            if (humanoid == null)
            {
                Logger.LogError("ScaleEquipment: humanoid is null!");
                return;
            }

            var wand = Player.m_localPlayer.GetRightItem();
            if (wand == null)
            {
                Logger.LogError("ScaleEquipment: wand is null!");
                return;
            }

            string GetHelmetPrefabName()
            {
                if (skeletonType is SkeletonType.MageTier1 or SkeletonType.MageTier2 or SkeletonType.MageTier3)
                {
                    return "ChebGonaz_SkeletonMageCirclet";
                }

                if (skeletonType is SkeletonType.PriestTier1 or SkeletonType.PriestTier2 or SkeletonType.PriestTier3)
                {
                    return "ChebGonaz_SkeletonPriestHood";
                }
                if (skeletonType is SkeletonType.PoisonTier1 or SkeletonType.PoisonTier2 or SkeletonType.PoisonTier3)
                {
                    return armorType switch
                    {
                        ArmorType.Leather => "ChebGonaz_SkeletonHelmetLeatherPoison",
                        ArmorType.LeatherTroll => "ChebGonaz_SkeletonHelmetLeatherPoisonTroll",
                        ArmorType.LeatherWolf => "ChebGonaz_SkeletonHelmetLeatherPoisonWolf",
                        ArmorType.LeatherLox => "ChebGonaz_SkeletonHelmetLeatherPoisonLox",
                        ArmorType.Bronze => "ChebGonaz_SkeletonHelmetBronzePoison",
                        ArmorType.Iron => "ChebGonaz_SkeletonHelmetIronPoison",
                        _ => "ChebGonaz_HelmetBlackIronSkeletonPoison",
                    };
                }
                return armorType switch
                {
                    ArmorType.Leather => "ChebGonaz_SkeletonHelmetLeather",
                    ArmorType.LeatherTroll => "ChebGonaz_SkeletonHelmetLeatherTroll",
                    ArmorType.LeatherWolf => "ChebGonaz_SkeletonHelmetLeatherWolf",
                    ArmorType.LeatherLox => "ChebGonaz_SkeletonHelmetLeatherLox",
                    ArmorType.Bronze => "ChebGonaz_SkeletonHelmetBronze",
                    ArmorType.Iron => "ChebGonaz_SkeletonHelmetIron",
                    _ => "ChebGonaz_HelmetBlackIronSkeleton",
                };
            }

            var helmetPrefabName = GetHelmetPrefabName();
            var helmetPrefab = PrefabManager.Instance.GetPrefab(helmetPrefabName);
            // note: as of 1.2.0 weapons were moved into skeleton prefab variants
            // with different m_randomWeapons set. This is because trying to set
            // dynamically seems very difficult -> skeletons forgetting their weapons
            // on logout/log back in; skeletons thinking they have no weapons
            // and running away from enemies.
            //
            // Fortunately, armor seems to work fine.
            var emblem = Options.Options.Emblem;
            switch (armorType)
            {
                case ArmorType.Leather:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ArmorLeatherChest"),
                        ZNetScene.instance.GetPrefab("ArmorLeatherLegs"),
                        ZNetScene.instance.GetPrefab("CapeDeerHide"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageLeather.Value; }
                    break;
                case ArmorType.LeatherTroll:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherChestTroll"),
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherLegsTroll"),
                        ZNetScene.instance.GetPrefab("CapeTrollHide"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageLeather.Value; }
                    break;
                case ArmorType.LeatherWolf:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherChestWolf"),
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherLegsWolf"),
                        ZNetScene.instance.GetPrefab("CapeWolf"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageLeather.Value; }
                    break;
                case ArmorType.LeatherLox:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherChestLox"),
                        ZNetScene.instance.GetPrefab("ChebGonaz_ArmorLeatherLegsLox"),
                        ZNetScene.instance.GetPrefab("CapeLox"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageLeather.Value; }
                    break;
                case ArmorType.Bronze:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ArmorBronzeChest"),
                        ZNetScene.instance.GetPrefab("ArmorBronzeLegs"),
                        ZNetScene.instance.GetPrefab("CapeLox"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageBronze.Value; }
                    Emblem = InternalName.GetName(emblem);
                    break;
                case ArmorType.Iron:
                    defaultItems.AddRange(new[] {
                        helmetPrefab,
                        ZNetScene.instance.GetPrefab("ArmorIronChest"),
                        ZNetScene.instance.GetPrefab("ArmorIronLegs"),
                        ZNetScene.instance.GetPrefab("CapeLox"),
                    });
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageIron.Value; }
                    Emblem = InternalName.GetName(emblem);
                    break;
                case ArmorType.BlackMetal:
                    // Scoped strictly to WarriorTier4 so Poison/Mage/Priest Tier3
                    // (which also map to ArmorType.BlackMetal elsewhere) are never
                    // affected by this, even if isMistlandsArmor were somehow true
                    // for them.
                    if (isMistlandsArmor && skeletonType == SkeletonType.WarriorTier4)
                    {
                        // Helmet equipped HERE, synchronously with everything else
                        // (identical flow to every working tier). The deferred
                        // coroutine below only adjusts its transform for fit and
                        // never equips: a second, late equip call was the sole
                        // remaining procedural difference vs Tier4-normal, which
                        // attacks fine with the same prefab, AI and weapons.
                        // Null-guard: one unresolvable piece must never abort
                        // the spawn (half-initialized minion = no master, no orders,
                        // stands idle forever). Chest/legs come from the transplant
                        // (vanilla direct NREs VisEquipment attach on skeletons);
                        // cape stays vanilla (proven). Missing pieces log and skip.
                        foreach (var piece in new[] { "chest", "legs" })
                        {
                            var transplant = BasePlugin.GetCarapaceArmorPiece(piece);
                            if (transplant == null) Logger.LogWarning($"ScaleEquipment: carapace {piece} unavailable, spawning without it.");
                            else defaultItems.Add(transplant);
                        }
                        {
                            var cape = ZNetScene.instance.GetPrefab("CapeLox");
                            if (cape == null) Logger.LogWarning("ScaleEquipment: armor prefab 'CapeLox' not found, spawning without it.");
                            else defaultItems.Add(cape);
                        }
                        var syncHelmet = ResolveCarapaceHelmetPrefab();
                        if (syncHelmet != null) defaultItems.Add(syncHelmet);
                        needsCarapaceHelmetScaleFix = true;
                    }
                    else
                    {
                        defaultItems.AddRange(new[] {
                            helmetPrefab,
                            ZNetScene.instance.GetPrefab("ChebGonaz_ArmorBlackIronChest"),
                            ZNetScene.instance.GetPrefab("ChebGonaz_ArmorBlackIronLegs"),
                            ZNetScene.instance.GetPrefab("CapeLox"),
                        });
                    }
                    if (BasePlugin.DurabilityDamage.Value) { wand.m_durability -= BasePlugin.DurabilityDamageBlackIron.Value; }
                    Emblem = InternalName.GetName(emblem);
                    break;
            }
            
            humanoid.m_defaultItems = humanoid.m_defaultItems.Union(defaultItems).ToArray();

            // Spawn-loadout proof: exact prefab names about to be equipped.
            // If a spawn aborts inside GiveDefaultItems below, this line names
            // the guilty loadout (a null here = unresolvable prefab).
            if (isMistlandsArmor && BasePlugin.WarriorTier4WeaponDiag.Value)
                Logger.LogInfo($"ScaleEquipment mistlands loadout: [{string.Join(",", defaultItems.Select(p => p != null ? p.name : "null"))}]");
            
            humanoid.GiveDefaultItems();

            if (needsCarapaceHelmetScaleFix)
            {
                StartCoroutine(AddAndScaleCarapaceHelmetDeferred(humanoid));
            }
             
            if (BasePlugin.DurabilityDamage.Value)
            {
                switch (skeletonType)
                {
                    case SkeletonType.ArcherTier1:
                    case SkeletonType.ArcherTier2:
                    case SkeletonType.ArcherTier3:
                        wand.m_durability -= BasePlugin.DurabilityDamageArcher.Value;
                        break;
                    case SkeletonType.MageTier1:
                    case SkeletonType.MageTier2:
                    case SkeletonType.MageTier3:
                        wand.m_durability -= BasePlugin.DurabilityDamageMage.Value;
                        break;
                    case SkeletonType.PoisonTier1:
                    case SkeletonType.PoisonTier2:
                    case SkeletonType.PoisonTier3:
                        wand.m_durability -= BasePlugin.DurabilityDamagePoison.Value;
                        break;
                    default:
                        wand.m_durability -= BasePlugin.DurabilityDamageWarrior.Value;
                        break;
                }
            }
        }

        // HelmetCarapace renders too small on this rig compared to the known-good
        // Iron helmet, and guessing its instantiated name ("HelmetCarapace") didn't
        // match anything real (0 transforms found) - same lesson learned twice now
        // with VisEquipment field names on the Charred. Diffing is the only
        // technique that's actually worked reliably (Charred gold tint): find
        // whatever Transforms are new since right before this one equip call, then
        // scale only the new "roots" (objects whose parent already existed, ie. the
        // actual attachment points), not every new child mesh individually - scaling
        // a root scales its whole hierarchy together, so the helmet grows as one
        // piece instead of stretching each part from a different pivot.
        // Equips and scales the Carapace helmet entirely inside this one coroutine,
        // with a settle-wait on BOTH sides of the equip call: first so chest/legs/
        // cape AND the pre-existing weapon/shield fully finish rendering before we
        // take the "before" snapshot (last attempt's bug - the weapon/shield hadn't
        // rendered yet when "before" was taken synchronously, so they looked "new"
        // too and got scaled by mistake), then again so the helmet itself has
        // rendered before we take the "after" snapshot and diff.
        // Single source for the mistlands helmet: configured custom prefab,
        // vanilla Carapace, or the skeleton-rigged black iron fallback.
        // NOTE for the future Unity helmet: its equipped root transform must
        // contain "helmet" in the name (like CarapaceHelmet / bronzehelmet) so
        // the adjust pass below finds it without blind diffing.
        private static GameObject ResolveCarapaceHelmetPrefab()
        {
            // Priority: configured (Unity bundle / OBJ-built) -> vanilla -> black iron.
            var carapaceHelmetPrefab = ZNetScene.instance.GetPrefab(BasePlugin.ArmorCarapaceHelmetPrefabConfig.Value);
            if (carapaceHelmetPrefab == null
                && BasePlugin.ArmorCarapaceHelmetPrefabConfig.Value == "ChebGonaz_HelmetCarapaceSkeleton")
            {
                // Runtime OBJ build (no bundle needed). Missing file or parse
                // failure returns null and the chain below carries on.
                carapaceHelmetPrefab = BasePlugin.BuildCarapaceHelmetClone();
            }
            if (carapaceHelmetPrefab != null)
            {
                // Unity prefabs ship visual-only (no game assemblies in Editor):
                // graft a working ItemDrop so equip + ObjectDB resolve cleanly.
                BasePlugin.GraftHelmetItemDrop(carapaceHelmetPrefab);
            }
            else
            {
                carapaceHelmetPrefab = ZNetScene.instance.GetPrefab("HelmetCarapace");
            }
            if (carapaceHelmetPrefab == null)
            {
                Logger.LogWarning($"ResolveCarapaceHelmetPrefab: prefab {BasePlugin.ArmorCarapaceHelmetPrefabConfig.Value} unavailable, falling back to ChebGonaz_HelmetBlackIronSkeleton.");
                carapaceHelmetPrefab = ZNetScene.instance.GetPrefab("ChebGonaz_HelmetBlackIronSkeleton");
            }
            if (carapaceHelmetPrefab == null)
            {
                Logger.LogError("ResolveCarapaceHelmetPrefab: fallback helmet also missing, spawning without helmet.");
            }
            else
            {
                BasePlugin.EnsureTransplantRegistered(carapaceHelmetPrefab); // no-op for native prefabs
            }
            return carapaceHelmetPrefab;
        }

        // Fit-only pass for the ALREADY EQUIPPED (synchronous, see ScaleEquipment)
        // mistlands helmet. Finds it by name instead of before/after diffing:
        // the old diff once caught late-rendering weapon/arm roots by mistake.
        // Never equips anything, so the equip flow stays bit-identical to the
        // proven Tier4-normal path.
        private IEnumerator AddAndScaleCarapaceHelmetDeferred(Humanoid humanoid)
        {
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.3f);
            if (this == null) yield break;

            var resolvedPrefab = ResolveCarapaceHelmetPrefab();
            if (resolvedPrefab == null) yield break;

            var scale = BasePlugin.ArmorCarapaceHelmetScaleConfig.Value;
            // Lift only makes sense for the player-rigged vanilla Carapace mesh
            // sitting inside the skull. The skeleton-rigged fallback (black iron)
            // already fits natively: never shift it, or it would float.
            var isCarapace = resolvedPrefab.name.IndexOf("arapace", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var lift = isCarapace ? BasePlugin.ArmorCarapaceHelmetLiftConfig.Value : 0f;
            // Custom skeleton-rigged helmet ships at correct size and position:
            // equip only, no transform touching.
            if (System.Math.Abs(scale - 1.0f) < 0.001f && System.Math.Abs(lift) < 0.001f)
            {
                Logger.LogInfo($"SkeletonMinion: Carapace helmet {resolvedPrefab.name} equipped without adjustment (custom rig).");
                LogPostHelmetWeapon();
                yield break;
            }

            // Unity-custom helmets get ONLY the configured pitch (fitted by
            // hand in Unity); iron fits natively (touch nothing); vanilla keeps
            // the legacy probe/restore path below.
            if (!resolvedPrefab.name.Equals("HelmetCarapace", System.StringComparison.OrdinalIgnoreCase))
            {
                if (resolvedPrefab.name.Equals("ChebGonaz_HelmetCarapaceSkeleton", System.StringComparison.OrdinalIgnoreCase))
                    AdjustUnityHelmet();
                else
                    Logger.LogInfo($"SkeletonMinion: Carapace helmet {resolvedPrefab.name} equipped fitted, no adjust.");
                LogPostHelmetWeapon();
                yield break;
            }

            // Target the helmet MESH, never a bare attach point/bone: the old
            // first-match hit the skeleton's own Helmet_attach bone (moving the
            // bone instead of the mesh). attach_skin visuals parent to the body,
            // so only shifting the mesh itself places vanilla correctly.
            // Bounds probe (read-only): mesh-space center/size decide whether a
            // rigid placement can work. Then restore engine placement: the
            // rootBone-duplicate experiment broke the evaluation space and hid
            // the mesh, so back to the vanilla rebind (visible inside skull =
            // baseline to beat).
            var helmet = FindHelmetVisual();
            if (helmet != null)
            {
                var probeSkinned = helmet.GetComponent<SkinnedMeshRenderer>()
                    ?? helmet.GetComponentInChildren<SkinnedMeshRenderer>(true);
                try
                {
                    var b = probeSkinned != null && probeSkinned.sharedMesh != null
                        ? probeSkinned.sharedMesh.bounds
                        : new Bounds();
                    Jotunn.Logger.LogInfo($"[HelmetProbe] visual={helmet.name} meshBounds center={b.center} size={b.size}.");
                }
                catch { Jotunn.Logger.LogInfo("[HelmetProbe] bounds unreadable."); }
            }
            else
            {
                Logger.LogWarning($"SkeletonMinion: helmet visual not found for {resolvedPrefab.name}, skipping adjust.");
            }
            RestoreHelmetEnginePlacement();
            LogPostHelmetWeapon();
        }

        // Points a Unity-built helmet's visor forward + sets its height: both
        // assigned (never accumulated), so relogs and re-equips can't stack
        // them. Tune live via pitch/height configs, re-summon to apply.
        private void AdjustUnityHelmet()
        {
            var visual = FindHelmetVisual();
            if (visual == null || !BasePlugin.s_helmetBasePosValid) return;
            visual.localRotation = Quaternion.Euler(BasePlugin.ArmorCarapaceHelmetPitchConfig.Value, 0f, 0f);
            visual.localPosition = BasePlugin.s_helmetBasePos + new Vector3(0f, BasePlugin.ArmorCarapaceHelmetHeightConfig.Value, 0f);
            Logger.LogInfo($"AdjustUnityHelmet: {visual.name} set.");
        }

        // Mesh-first visual search shared by the spawn adjust pass and the
        // relog block: skips bare attach points/bones.
        private Transform FindHelmetVisual()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                var lname = t.name.ToLowerInvariant();
                if (!lname.Contains("helmet") || lname.Contains("attach")) continue;
                return t;
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf("helmet", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                return t;
            }
            return null;
        }

        // Cleanup only: destroys any leftover CarapaceHelmetRoot duplicate from
        // the retired rootBone experiment. The re-equip once done here is
        // REMOVED: equipping is already complete synchronously at spawn, and
        // this second pass NREs inside SetupVisEquipment on some loadouts.
        private void RestoreHelmetEnginePlacement()
        {
            var head = FindBone("Head");
            var dup = head?.Find("CarapaceHelmetRoot");
            if (dup != null) UnityEngine.Object.Destroy(dup.gameObject);
        }

        private void LogPostHelmetWeapon()
        {
            // Attack diagnostic: is the weapon still in hand AFTER the helmet
            // pass? If null here (but set at spawn), something in this flow
            // disarmed the minion and that is why it never attacks.
            if (!BasePlugin.WarriorTier4WeaponDiag.Value) return;
            var postHumanoid = GetComponent<Humanoid>();
            var postWeapon = postHumanoid?.GetCurrentWeapon();
            Jotunn.Logger.LogInfo($"[WeaponDiag] post-helmet {name}: weapon=[{(postWeapon?.m_shared != null ? postWeapon.m_shared.m_name : "null")}]");
        }

        // Rewrites the wielded weapon's attack hookup for the skeleton rig, on
        // the LIVE INSTANCE only (never shared prefabs). Vanilla weapons ship
        // swing_longsword/axe states that don't exist here, so without this
        // the attack fires silently with no swing and no hit-event damage.
        // Customs already say "attack" and are skipped untouched. Idempotent:
        // safe to run at spawn and on every relog.
        private IEnumerator EnsureSkeletonWeaponAnim()
        {
            var humanoid = GetComponent<Humanoid>();
            if (humanoid == null) yield break;

            var deadline = Time.time + 8f;
            while (Time.time < deadline)
            {
                if (this == null) yield break;
                var weapon = humanoid.GetCurrentWeapon();
                if (weapon == null || weapon.m_shared == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
                // NOTE: m_attack may be struct or class; access through the field
                // chain (no locals held across mutation) so both behave.
                string anim;
                int chains;
                try
                {
                    anim = weapon.m_shared.m_attack.m_attackAnimation;
                    chains = weapon.m_shared.m_attack.m_attackChainLevels;
                }
                catch
                {
                    Logger.LogWarning($"EnsureSkeletonWeaponAnim: unreadable attack on {name}, leaving weapon untouched.");
                    yield break;
                }
                if (anim == "attack" && chains == 0)
                {
                    if (BasePlugin.WarriorTier4WeaponDiag.Value)
                        Jotunn.Logger.LogInfo($"[WeaponDiag] weapanim {name}: already compatible ({anim}/{chains}), untouched.");
                    yield break;
                }
                if (anim != null && anim.StartsWith("swing_"))
                {
                    weapon.m_shared.m_attack.m_attackAnimation = "attack";
                    weapon.m_shared.m_attack.m_attackChainLevels = 0;
                    Jotunn.Logger.LogInfo($"[WeaponDiag] weapanim {name}: {anim}->attack on live instance.");
                    // No-leak proof: shared vanilla must still read swing_*.
                    foreach (var baseline in BasePlugin.VanillaWeaponAnimBaseline)
                    {
                        var vanillaPrefab = ZNetScene.instance?.GetPrefab(baseline.Key);
                        var vanillaAnim = vanillaPrefab?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack?.m_attackAnimation;
                        Jotunn.Logger.LogInfo($"[WeaponDiag] weapanim vanilla-intact {baseline.Key}={vanillaAnim} (baseline {baseline.Value}).");
                    }
                    yield break;
                }
                Logger.LogWarning($"EnsureSkeletonWeaponAnim: unknown animation '{anim}' on {name}, leaving weapon untouched.");
                yield break;
            }
        }

        public static void InstantiateSkeleton(int quality, float playerNecromancyLevel,
            SkeletonType skeletonType, ArmorType armorType, bool isMistlandsArmor = false)
        {
            if (skeletonType is SkeletonType.None) return;
            
            var player = Player.m_localPlayer;
            var prefabName = InternalName.GetName(skeletonType);
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (!prefab)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{prefabName} does not exist");
                Logger.LogError($"InstantiateSkeleton: spawning {prefabName} failed");
                return;
            }

            var transform = player.transform;
            var spawnedChar = Instantiate(prefab,
                transform.position + transform.forward * 2f + Vector3.up, Quaternion.identity);
            var character = spawnedChar.GetComponent<Character>();
            // Extra star levels raise effective damage output for the Mistlands/
            // Carapace-armored Warrior (its weapon itself can't be swapped - same
            // "baked into the prefab" limitation discussed for the Charred).
            var isMistlandsWarrior = isMistlandsArmor && skeletonType == SkeletonType.WarriorTier4;
            character.SetLevel(isMistlandsWarrior ? quality + BasePlugin.MistlandsExtraLevelsConfig.Value : quality);

            spawnedChar.AddComponent<FreshMinion>();

            var minion = skeletonType switch
            {
                SkeletonType.PoisonTier1
                    or SkeletonType.PoisonTier2
                    or SkeletonType.PoisonTier3 => spawnedChar.AddComponent<PoisonSkeletonMinion>(),
                SkeletonType.Woodcutter => spawnedChar.AddComponent<SkeletonWoodcutterMinion>(),
                SkeletonType.Miner => spawnedChar.AddComponent<SkeletonMinerMinion>(),
                _ => spawnedChar.AddComponent<SkeletonMinion>()
            };
            minion.SetCreatedAtLevel(playerNecromancyLevel);
            if (isMistlandsArmor && skeletonType == SkeletonType.WarriorTier4)
                minion.SetIsCarapaceArmor(true);
            minion.ScaleEquipment(playerNecromancyLevel, skeletonType, armorType, isMistlandsArmor);
            minion.ScaleStats(playerNecromancyLevel);

            // Attack diagnostic: which weapon is actually in hand right after
            // spawn (before the deferred carapace helmet). Null here = the
            // minion can never play an armed attack.
            if (BasePlugin.WarriorTier4WeaponDiag.Value)
            {
                var spawnHumanoid = spawnedChar.GetComponent<Humanoid>();
                var spawnWeapon = spawnHumanoid?.GetCurrentWeapon();
                Jotunn.Logger.LogInfo($"[WeaponDiag] spawned {skeletonType} mistlands={isMistlandsArmor}: weapon=[{(spawnWeapon?.m_shared != null ? spawnWeapon.m_shared.m_name : "null")}]");
            }

            if (isMistlandsWarrior)
            {
                var boostedHealth = character.GetMaxHealth() * BasePlugin.MistlandsHealthMultiplierConfig.Value;
                character.SetMaxHealth(boostedHealth);
                character.SetHealth(boostedHealth);
                minion.ApplyMistlandsResistances();
            }

            var eyeColor = Options.Options.EyeColor;
            var boneColor = Options.Options.BoneColor;

            minion.Eye = InternalName.GetName(eyeColor);
            minion.SkeletonBoneColor = InternalName.GetName(boneColor);

            if (skeletonType != SkeletonType.Woodcutter
                && skeletonType != SkeletonType.Miner)
            {
                if (Wand.FollowByDefault.Value)
                {
                    minion.Follow(player.gameObject);
                }
                else
                {
                    minion.Wait(player.transform.position);
                }
            }

            var levelIncrease = skeletonType switch
            {
                SkeletonType.ArcherTier1
                    or SkeletonType.ArcherTier2
                    or SkeletonType.ArcherTier3 => ArcherNecromancyLevelIncrease.Value,
                SkeletonType.MageTier1
                    or SkeletonType.MageTier2
                    or SkeletonType.MageTier3
                    or SkeletonType.PriestTier1
                    or SkeletonType.PriestTier2
                    or SkeletonType.PriestTier3 => MageNecromancyLevelIncrease.Value,
                SkeletonType.PoisonTier1
                    or SkeletonType.PoisonTier2
                    or SkeletonType.PoisonTier3 => PoisonNecromancyLevelIncrease.Value,
                _ => NecromancyLevelIncrease.Value
            };
            
            player.RaiseSkill(SkillManager.Instance.GetSkill(BasePlugin.NecromancySkillIdentifier).m_skill,
                levelIncrease);

            minion.UndeadMinionMaster = player.GetPlayerName();

            if (DropOnDeath.Value == DropType.Nothing) return;

            // handle refunding of resources on death
            var characterDrop = spawnedChar.AddComponent<CharacterDrop>();
            if (DropOnDeath.Value == DropType.Everything)
            {
                switch (skeletonType)
                {
                    case SkeletonType.WarriorTier1:
                    case SkeletonType.WarriorTier2:
                    case SkeletonType.WarriorTier3:
                    case SkeletonType.WarriorTier4:
                    case SkeletonType.WarriorNeedle:
                        GenerateDeathDrops(characterDrop, SkeletonWarriorMinion.ItemsCost);
                        break;
                    case SkeletonType.ArcherTier1:
                        GenerateDeathDrops(characterDrop, SkeletonArcherTier1Minion.ItemsCost);
                        break;
                    case SkeletonType.ArcherTier2:
                        GenerateDeathDrops(characterDrop, SkeletonArcherTier2Minion.ItemsCost);
                        break;
                    case SkeletonType.ArcherTier3:
                        GenerateDeathDrops(characterDrop, SkeletonArcherTier3Minion.ItemsCost);
                        break;
                    case SkeletonType.ArcherPoison:
                        GenerateDeathDrops(characterDrop, SkeletonArcherPoisonMinion.ItemsCost);
                        break;
                    case SkeletonType.ArcherFire:
                        GenerateDeathDrops(characterDrop, SkeletonArcherFireMinion.ItemsCost);
                        break;
                    case SkeletonType.ArcherFrost:
                        GenerateDeathDrops(characterDrop, SkeletonArcherFrostMinion.ItemsCost);
                        break;
                    case SkeletonType.ArcherSilver:
                        GenerateDeathDrops(characterDrop, SkeletonArcherSilverMinion.ItemsCost);
                        break;
                    case SkeletonType.MageTier1:
                    case SkeletonType.MageTier2:
                    case SkeletonType.MageTier3:
                        GenerateDeathDrops(characterDrop, SkeletonMageMinion.ItemsCost);
                        break;
                    case SkeletonType.PriestTier1:
                    case SkeletonType.PriestTier2:
                    case SkeletonType.PriestTier3:
                        GenerateDeathDrops(characterDrop, SkeletonPriestMinion.ItemsCost);
                        break;
                    case SkeletonType.PoisonTier1:
                    case SkeletonType.PoisonTier2:
                    case SkeletonType.PoisonTier3:
                        GenerateDeathDrops(characterDrop, PoisonSkeletonMinion.ItemsCost);
                        break;
                    case SkeletonType.Woodcutter:
                        GenerateDeathDrops(characterDrop, SkeletonWoodcutterMinion.ItemsCost);
                        break;
                    case SkeletonType.Miner:
                        GenerateDeathDrops(characterDrop, SkeletonMinerMinion.ItemsCost);
                        break;
                }
            }

            switch (armorType)
            {
                case ArmorType.Leather:
                    AddOrUpdateDrop(characterDrop, 
                        Random.value > .5f ? "DeerHide" : "LeatherScraps", // flip a coin for deer or scraps
                        BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.LeatherTroll:
                    AddOrUpdateDrop(characterDrop, "TrollHide", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.LeatherWolf:
                    AddOrUpdateDrop(characterDrop, "WolfPelt", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.LeatherLox:
                    AddOrUpdateDrop(characterDrop, "LoxPelt", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.Bronze:
                    AddOrUpdateDrop(characterDrop, "Bronze", BasePlugin.ArmorBronzeRequiredConfig.Value);
                    break;
                case ArmorType.Iron:
                    AddOrUpdateDrop(characterDrop, "Iron", BasePlugin.ArmorIronRequiredConfig.Value);
                    break;
                case ArmorType.BlackMetal:
                    AddOrUpdateDrop(characterDrop, "BlackMetal", BasePlugin.ArmorBlackIronRequiredConfig.Value);
                    if (isMistlandsArmor)
                    {
                        AddOrUpdateDrop(characterDrop, "Carapace", BasePlugin.ArmorCarapaceRequiredConfig.Value);
                    }
                    break;
            }

            // the component won't be remembered by the game on logout because
            // only what is on the prefab is remembered. Even changes to the prefab
            // aren't remembered. So we must write what we're dropping into
            // the ZDO as well and then read & restore this on Awake
            minion.RecordDrops(characterDrop);
        }
        
        public static void ConsumeResources(SkeletonType skeletonType, ArmorType armorType,
            bool isMistlandsArmor = false)
        {
            var inventory = Player.m_localPlayer.GetInventory();
            
            switch (skeletonType)
            {
                case SkeletonType.Miner:
                    ConsumeRequirements(SkeletonMinerMinion.ItemsCost, inventory);
                    break;
                case SkeletonType.Woodcutter:
                    ConsumeRequirements(SkeletonWoodcutterMinion.ItemsCost, inventory);
                    break;
        
                case SkeletonType.ArcherTier1:
                    ConsumeRequirements(SkeletonArcherTier1Minion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherTier2:
                    ConsumeRequirements(SkeletonArcherTier2Minion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherTier3:
                    ConsumeRequirements(SkeletonArcherTier3Minion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherPoison:
                    ConsumeRequirements(SkeletonArcherPoisonMinion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherFire:
                    ConsumeRequirements(SkeletonArcherFireMinion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherFrost:
                    ConsumeRequirements(SkeletonArcherFrostMinion.ItemsCost, inventory);
                    break;
                case SkeletonType.ArcherSilver:
                    ConsumeRequirements(SkeletonArcherSilverMinion.ItemsCost, inventory);
                    break;
                
                case SkeletonType.WarriorNeedle:
                    inventory.RemoveItem("$item_needle", BasePlugin.NeedlesRequiredConfig.Value);
                    ConsumeRequirements(SkeletonWarriorMinion.ItemsCost, inventory);
                    break;
        
                case SkeletonType.MageTier1:
                case SkeletonType.MageTier2:
                case SkeletonType.MageTier3:
                    ConsumeRequirements(SkeletonMageMinion.ItemsCost, inventory);
                    break;
                
                case SkeletonType.PriestTier1:
                case SkeletonType.PriestTier2:
                case SkeletonType.PriestTier3:
                    ConsumeRequirements(SkeletonPriestMinion.ItemsCost, inventory);
                    break;
        
                case SkeletonType.PoisonTier1:
                case SkeletonType.PoisonTier2:
                case SkeletonType.PoisonTier3:
                    ConsumeRequirements(PoisonSkeletonMinion.ItemsCost, inventory);
                    break;
                default:
                    ConsumeRequirements(SkeletonWarriorMinion.ItemsCost, inventory);
                    break;
            }
        
            // consume armor materials
            switch (armorType)
            {
                case ArmorType.Leather:
                    // todo: expose these options to config
                    var leatherItemTypes = new List<string>()
                    {
                        "$item_leatherscraps",
                        "$item_deerhide",
                        "$item_scalehide"
                    };
                    
                    foreach (var leatherItem in leatherItemTypes)
                    {
                        var leatherItemsInInventory = inventory.CountItems(leatherItem);
                        if (leatherItemsInInventory >= BasePlugin.ArmorLeatherScrapsRequiredConfig.Value)
                        {
                            inventory.RemoveItem(leatherItem,
                                BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                            break;
                        }
                    }
                    break;
                case ArmorType.LeatherTroll:
                    inventory.RemoveItem("$item_trollhide", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.LeatherWolf:
                    inventory.RemoveItem("$item_wolfpelt", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.LeatherLox:
                    inventory.RemoveItem("$item_loxpelt", BasePlugin.ArmorLeatherScrapsRequiredConfig.Value);
                    break;
                case ArmorType.Bronze:
                    inventory.RemoveItem("$item_bronze", BasePlugin.ArmorBronzeRequiredConfig.Value);
                    break;
                case ArmorType.Iron:
                    inventory.RemoveItem("$item_iron", BasePlugin.ArmorIronRequiredConfig.Value);
                    break;
                case ArmorType.BlackMetal:
                    inventory.RemoveItem("$item_blackmetal", BasePlugin.ArmorBlackIronRequiredConfig.Value);
                    if (isMistlandsArmor)
                    {
                        inventory.RemoveItem("$item_carapace", BasePlugin.ArmorCarapaceRequiredConfig.Value);
                    }
                    break;
            }
        }
        
        public override void LoadEyeMaterial()
        {
            var eyeColor = Eye;
            if (Eyes.TryGetValue(eyeColor, out var eye))
            {
                var eyeL = transform.Find("Visual/_skeleton_base/Armature/Hips/Spine/Spine1/Spine2/Neck/Head/eye_l");
                if (eyeL != null && eyeL.TryGetComponent(out MeshRenderer lMeshRenderer))
                {
                    //var mats = lMeshRenderer.materials;
                    //for (var i = 0; i < mats.Length; i++) mats[i] = eye;
                    lMeshRenderer.sharedMaterial = eye;
                }
                else
                {
                    Logger.LogError($"{name} (eyeL={eyeL}) Failed to get mesh renderer for eye_l");
                }
                
                var eyeR = transform.Find("Visual/_skeleton_base/Armature/Hips/Spine/Spine1/Spine2/Neck/Head/eye_r");
                if (eyeR != null && eyeR.TryGetComponent(out MeshRenderer rMeshRenderer))
                {
                    // var mats = rMeshRenderer.materials;
                    // for (var i = 0; i < mats.Length; i++) mats[i] = eye;
                    rMeshRenderer.sharedMaterial = eye;
                }
                else
                {
                    Logger.LogError($"{name} (eyeL={eyeR}) Failed to get mesh renderer for eye_r");
                }
            }
        }
    }
}
