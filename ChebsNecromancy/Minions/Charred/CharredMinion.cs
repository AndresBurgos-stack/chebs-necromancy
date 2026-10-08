using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ChebsNecromancy.Items.Wands;
using ChebsValheimLibrary.Common;
using ChebsValheimLibrary.Minions;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsNecromancy.Minions.Charred
{
    // Unlike SkeletonMinion/DraugrMinion, Charred minions don't need a custom-modelled
    // asset bundle prefab: Charred_Melee and Charred_Archer already exist as vanilla
    // Ashlands creatures, so we clone them at runtime (same technique BasePlugin.cs
    // already uses to clone "Ghost" into SpiritPylonGhostMinion).
    public class CharredMinion : UndeadMinion
    {
        public enum CharredType
        {
            None,
            [InternalName("ChebGonaz_CharredWarrior")] Warrior,
            [InternalName("ChebGonaz_CharredArcher")] Archer,
            [InternalName("ChebGonaz_CharredTwitcher")] Twitcher,
        };

        private static List<int> _hashList;

        public static bool IsCharredHash(int hash)
        {
            if (_hashList == null)
            {
                _hashList = new List<int>();
                foreach (CharredType value in Enum.GetValues(typeof(CharredType)))
                {
                    if (value is CharredType.None) continue;
                    _hashList.Add(InternalName.GetName(value).GetStableHashCode());
                }
            }

            return _hashList.Contains(hash);
        }

        // for limits checking
        private static int _createdOrderIncrementer;

        public static ConfigEntry<float> CharredBaseHealth;
        public static ConfigEntry<float> CharredHealthMultiplier;
        public static ConfigEntry<float> ArmoredHealthMultiplier;

        public static ConfigEntry<int> MaxCharred;
        public static ConfigEntry<int> MinionLimitIncrementsEveryXLevels;

        public static ConfigEntry<float> NecromancyLevelIncrease;
        public static ConfigEntry<float> ArcherNecromancyLevelIncrease;
        public static ConfigEntry<float> TwitcherNecromancyLevelIncrease;

        public static MemoryConfigEntry<string, List<string>> ArmorUpgradeCost;
        public static MemoryConfigEntry<string, List<string>> ArmoredRefund;
        public static ConfigEntry<bool> PriestHealingAllowed;

        public static ConfigEntry<int> TierOneQuality;
        public static ConfigEntry<int> TierTwoQuality;
        public static ConfigEntry<int> TierTwoLevelReq;
        public static ConfigEntry<int> TierThreeQuality;
        public static ConfigEntry<int> TierThreeLevelReq;

        public static ConfigEntry<string> BonusResistances;
        public static ConfigEntry<bool> CustomColors;
        public static ConfigEntry<string> SoulColor;
        public static ConfigEntry<bool> PassiveRegen;
        public static ConfigEntry<float> RegenTickSeconds;
        public static ConfigEntry<float> RegenPercentPerTick;

        public new static void CreateConfigs(BaseUnityPlugin plugin)
        {
            const string serverSynced = "CharredMinion (Server Synced)";
            const string client = "CharredMinion (Client)";

            CharredBaseHealth = plugin.Config.Bind(serverSynced, "CharredBaseHealth",
                200f, new ConfigDescription("HP = (BaseHealth + NecromancyLevel * HealthMultiplier) * (ArmoredHealthMultiplier if armored)", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CharredHealthMultiplier = plugin.Config.Bind(serverSynced, "CharredHealthMultiplier",
                8f, new ConfigDescription("HP = (BaseHealth + NecromancyLevel * HealthMultiplier) * (ArmoredHealthMultiplier if armored)", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArmoredHealthMultiplier = plugin.Config.Bind(serverSynced, "ArmoredHealthMultiplier",
                1.5f, new ConfigDescription("Extra HP multiplier for armored (Shift) Charred. 1.5 = an armored minion can take on 2-3 vanilla Charred Warriors.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MaxCharred = plugin.Config.Bind(serverSynced, "MaximumCharred",
                0, new ConfigDescription("The maximum amount of Charred minions that can be made (0 = unlimited).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MinionLimitIncrementsEveryXLevels = plugin.Config.Bind(serverSynced,
                "CharredMinionLimitIncrementsEveryXLevels",
                10, new ConfigDescription(
                    "Attention: has no effect if minion limits are off. Increases player's maximum Charred count by 1 every X levels.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NecromancyLevelIncrease = plugin.Config.Bind(serverSynced, "CharredNecromancyLevelIncrease",
                2f, new ConfigDescription(
                    "How much creating a Charred Warrior contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ArcherNecromancyLevelIncrease = plugin.Config.Bind(serverSynced, "CharredArcherNecromancyLevelIncrease",
                2.5f, new ConfigDescription(
                    "How much creating a Charred Archer contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            TwitcherNecromancyLevelIncrease = plugin.Config.Bind(serverSynced, "CharredTwitcherNecromancyLevelIncrease",
                1.5f, new ConfigDescription(
                    "How much creating a Charred Twitcher contributes to your Necromancy level increasing.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            var armorUpgradeCost = plugin.Config.Bind(serverSynced, "CharredArmorUpgradeCost",
                "FlametalNew:5", new ConfigDescription(
                    "The extra items consumed (hold Shift when creating the minion) to summon an armored Charred wearing the full Charred armor set. Please use a comma-delimited list of prefab names with a : and integer for amount.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            ArmorUpgradeCost = new MemoryConfigEntry<string, List<string>>(armorUpgradeCost,
                s => s?.Split(',').Select(str => str.Trim()).ToList());

            var armoredRefund = plugin.Config.Bind(serverSynced, "CharredArmoredRefund",
                "FlametalNew:3", new ConfigDescription(
                    "What an armored Charred refunds on death: a part of the armor metal, like other minions refund their materials. Please use a comma-delimited list of prefab names with a : and integer for amount.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
            ArmoredRefund = new MemoryConfigEntry<string, List<string>>(armoredRefund,
                s => s?.Split(',').Select(str => str.Trim()).ToList());

            PriestHealingAllowed = plugin.Config.Bind(serverSynced, "CharredPriestHealingAllowed",
                false, new ConfigDescription(
                    "Whether the Skeleton Priest can heal Charred minions. Off by default: Charred are not skeletons.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PassiveRegen = plugin.Config.Bind(serverSynced, "CharredPassiveRegen",
                true, new ConfigDescription("Whether Charred minions slowly regenerate HP while out of combat.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RegenTickSeconds = plugin.Config.Bind(serverSynced, "CharredRegenTickSeconds",
                5f, new ConfigDescription("Seconds between passive regen ticks.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RegenPercentPerTick = plugin.Config.Bind(serverSynced, "CharredRegenPercentPerTick",
                2f, new ConfigDescription("Max HP percent restored per regen tick while out of combat.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CustomColors = plugin.Config.Bind(client, "CharredCustomColors",
                true, new ConfigDescription("Master switch for summoned Charred visuals: off keeps them fully vanilla. The Charred Wand stays blue regardless.", null));

            SoulColor = plugin.Config.Bind(client, "CharredSoulColor",
                "#7A8799", new ConfigDescription("HTML color of the summoned Charred soul/eyes particles, eg. #7AB8FF for blue. Applies when CharredCustomColors is on.", null));

            TierOneQuality = plugin.Config.Bind(serverSynced, "CharredTierOneQuality",
                1, new ConfigDescription("Star quality of tier 1 Charred minions.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            TierTwoQuality = plugin.Config.Bind(serverSynced, "CharredTierTwoQuality",
                2, new ConfigDescription("Star quality of tier 2 Charred minions.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            TierTwoLevelReq = plugin.Config.Bind(serverSynced, "CharredTierTwoLevelReq",
                50, new ConfigDescription("Necromancy skill level required to summon tier 2 Charred.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            TierThreeQuality = plugin.Config.Bind(serverSynced, "CharredTierThreeQuality",
                3, new ConfigDescription("Star quality of tier 3 Charred minions.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            TierThreeLevelReq = plugin.Config.Bind(serverSynced, "CharredTierThreeLevelReq",
                90, new ConfigDescription("Necromancy skill level required to summon tier 3 Charred.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BonusResistances = plugin.Config.Bind(serverSynced, "CharredBonusResistances",
                "Slash:Resistant,Blunt:Resistant", new ConfigDescription(
                    "Extra damage resistances applied to Charred minions on every spawn. Format: comma-delimited DamageType:Modifier pairs, eg. Slash:Resistant,Fire:Immune.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

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

            // Armor is synced through VisEquipment, so a minion wearing Charred
            // pieces after a relog is known to be armored without extra ZDO data.
            var visEquipment = GetComponent<Humanoid>()?.m_visEquipment;
            var armored = visEquipment != null
                && (visEquipment.m_currentChestItemHash != 0
                    || visEquipment.m_currentHelmetItemHash != 0
                    || visEquipment.m_currentLegItemHash != 0);

            ScaleStats(GetCreatedAtLevel(), armored);

            ApplyBonusResistances();

            ApplyBlueGlow();

            // VisEquipment remembers what armor the minion is wearing.
            // Reapply it so armor values survive logout/login, same as DraugrMinion does.
            if (TryGetComponent(out Humanoid armorHumanoid))
            {
                var equipmentHashes = new List<int>()
                {
                    armorHumanoid.m_visEquipment.m_currentChestItemHash,
                    armorHumanoid.m_visEquipment.m_currentLegItemHash,
                    armorHumanoid.m_visEquipment.m_currentHelmetItemHash
                };
                equipmentHashes.ForEach(hash =>
                {
                    var equipmentPrefab = ZNetScene.instance.GetPrefab(hash);
                    if (equipmentPrefab != null)
                    {
                        armorHumanoid.GiveDefaultItem(equipmentPrefab);
                    }
                });
            }

            RestoreDrops();

            var freshMinion = GetComponent<FreshMinion>();
            var monsterAI = GetComponent<MonsterAI>();
            if (monsterAI != null) monsterAI.m_randomMoveRange = RoamRange.Value;

            if (!Wand.FollowByDefault.Value || freshMinion == null)
            {
                yield return new WaitUntil(() => Player.m_localPlayer != null);
                RoamFollowOrWait();
            }

            if (freshMinion != null)
            {
                Destroy(freshMinion);
            }

            StartCoroutine(PassiveRegenLoop());
        }

        private IEnumerator PassiveRegenLoop()
        {
            var character = GetComponent<Character>();
            var monsterAI = GetComponent<MonsterAI>();
            while (this != null && character != null && !character.IsDead())
            {
                yield return new WaitForSeconds(RegenTickSeconds.Value);
                if (!PassiveRegen.Value) continue;
                if (character.GetHealth() >= character.GetMaxHealth()) continue;
                if (monsterAI != null
                    && (monsterAI.m_targetCreature != null || monsterAI.m_targetStatic != null)) continue;
                character.Heal(character.GetMaxHealth() * RegenPercentPerTick.Value / 100f);
            }
        }

        private static string _soulHtmlCache;
        private static Color _soulColorCache = new Color(0.48f, 0.53f, 0.6f);

        private void ApplyBlueGlow()
        {
            if (!CustomColors.Value) return;

            // Eyes and chest soul are particle systems: tinting per instance
            // never touches shared materials, so vanilla stays untouched.
            if (SoulColor.Value != _soulHtmlCache)
            {
                _soulHtmlCache = SoulColor.Value;
                var html = (_soulHtmlCache ?? "").Trim();
                if (!html.StartsWith("#")) html = "#" + html;
                if (!ColorUtility.TryParseHtmlString(html, out _soulColorCache))
                {
                    Logger.LogWarning($"CharredMinion: malformed soul color '{_soulHtmlCache}', keeping previous.");
                }
            }
            var soulBlue = _soulColorCache;
            var systems = 0;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                var glowName = ps.gameObject.name;
                if (glowName.IndexOf("EyeGlow", System.StringComparison.OrdinalIgnoreCase) < 0
                    && glowName.IndexOf("chestglow", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var main = ps.main;
                main.startColor = soulBlue;
                systems++;
            }
            Logger.LogWarning($"CharredMinion: blue soul applied to {systems} particle systems.");
        }

        public void ScaleStats(float necromancyLevel, bool armored)
        {
            var character = GetComponent<Character>();
            if (character == null)
            {
                Logger.LogError("ScaleStats: Character component is null!");
                return;
            }

            var health = CharredBaseHealth.Value + necromancyLevel * CharredHealthMultiplier.Value;
            if (armored) health *= ArmoredHealthMultiplier.Value;
            character.SetMaxHealth(health);
            character.SetHealth(health);
        }

        private void ApplyBonusResistances()
        {
            var character = GetComponent<Character>();
            if (character == null || string.IsNullOrWhiteSpace(BonusResistances.Value)) return;

            foreach (var entry in BonusResistances.Value.Split(','))
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
                    Logger.LogWarning($"CharredMinion: ignoring malformed resistance entry '{entry.Trim()}'.");
                }
            }
        }

        public void ScaleCharredEquipment(bool armored)
        {
            if (!armored) return;

            var humanoid = GetComponent<Humanoid>();
            if (humanoid == null)
            {
                Logger.LogError("ScaleCharredEquipment: humanoid is null!");
                return;
            }

            // Charred armor pieces are made for the Charred rig, so they fit by definition.
            // Append to the existing defaults: the vanilla weapon lives in m_defaultItems
            // and replacing the array would disarm the minion.
            var defaultItems = new List<GameObject>(humanoid.m_defaultItems ?? Array.Empty<GameObject>());
            foreach (var prefabName in new[] { "Charred_Breastplate", "Charred_Helmet", "Charred_HipCloth" })
            {
                var equipmentPrefab = ZNetScene.instance.GetPrefab(prefabName);
                if (equipmentPrefab == null)
                {
                    Logger.LogError($"ScaleCharredEquipment: prefab {prefabName} not found!");
                    continue;
                }
                defaultItems.Add(equipmentPrefab);
            }

            humanoid.m_defaultItems = defaultItems.ToArray();
            humanoid.GiveDefaultItems();
        }

        public static void ConsumeResources(CharredType charredType, bool armored)
        {
            var inventory = Player.m_localPlayer.GetInventory();

            switch (charredType)
            {
                case CharredType.Archer:
                    ConsumeRequirements(CharredArcherMinion.ItemsCost, inventory);
                    break;
                case CharredType.Twitcher:
                    ConsumeRequirements(CharredTwitcherMinion.ItemsCost, inventory);
                    break;
                default:
                    ConsumeRequirements(CharredWarriorMinion.ItemsCost, inventory);
                    break;
            }

            if (armored)
            {
                ConsumeRequirements(ArmorUpgradeCost, inventory);
            }
        }

        public static void InstantiateCharred(int quality, float playerNecromancyLevel, CharredType charredType, bool armored)
        {
            if (charredType is CharredType.None) return;

            var player = Player.m_localPlayer;
            var prefabName = InternalName.GetName(charredType);
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (!prefab)
            {
                player.Message(MessageHud.MessageType.TopLeft, $"{prefabName} does not exist");
                Logger.LogError($"InstantiateCharred: spawning {prefabName} failed. Is the Ashlands DLC installed?");
                return;
            }

            var transform = player.transform;
            var spawnedChar = Instantiate(prefab,
                transform.position + transform.forward * 2f + Vector3.up, Quaternion.identity);
            var character = spawnedChar.GetComponent<Character>();
            character.SetLevel(quality);

            spawnedChar.AddComponent<FreshMinion>();

            CharredMinion minion = charredType switch
            {
                CharredType.Archer => spawnedChar.AddComponent<CharredArcherMinion>(),
                CharredType.Twitcher => spawnedChar.AddComponent<CharredTwitcherMinion>(),
                _ => spawnedChar.AddComponent<CharredWarriorMinion>()
            };
            minion.SetCreatedAtLevel(playerNecromancyLevel);
            minion.ScaleCharredEquipment(armored);
            minion.ScaleStats(playerNecromancyLevel, armored);

            if (Wand.FollowByDefault.Value)
            {
                minion.Follow(player.gameObject);
            }
            else
            {
                minion.Wait(player.transform.position);
            }

            var levelIncrease = charredType switch
            {
                CharredType.Archer => ArcherNecromancyLevelIncrease.Value,
                CharredType.Twitcher => TwitcherNecromancyLevelIncrease.Value,
                _ => NecromancyLevelIncrease.Value
            };

            player.RaiseSkill(SkillManager.Instance.GetSkill(BasePlugin.NecromancySkillIdentifier).m_skill,
                levelIncrease);

            minion.UndeadMinionMaster = player.GetPlayerName();

            // Only armored elites refund, and only the metal: base minions and
            // their costs are consumed on summoning. Applies on any DropOnDeath
            // except Nothing, so it works with the default JustResources too.
            if (DropOnDeath.Value == DropType.Nothing) return;

            var characterDrop = spawnedChar.AddComponent<CharacterDrop>();
            if (armored
                && (charredType == CharredType.Warrior || charredType == CharredType.Archer))
            {
                GenerateDeathDrops(characterDrop, ArmoredRefund);
            }

            // the component won't be remembered by the game on logout because
            // only what is on the prefab is remembered, so write what we're
            // dropping into the ZDO and restore it on Awake (see RestoreDrops above).
            minion.RecordDrops(characterDrop);
        }
    }
}
