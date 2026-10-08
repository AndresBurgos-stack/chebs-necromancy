using System.Collections;
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

        public static ConfigEntry<int> MaxCharred;
        public static ConfigEntry<int> MinionLimitIncrementsEveryXLevels;

        public static ConfigEntry<float> NecromancyLevelIncrease;
        public static ConfigEntry<float> ArcherNecromancyLevelIncrease;

        public static ConfigEntry<int> TierOneQuality;
        public static ConfigEntry<int> TierTwoQuality;
        public static ConfigEntry<int> TierTwoLevelReq;
        public static ConfigEntry<int> TierThreeQuality;
        public static ConfigEntry<int> TierThreeLevelReq;

        public new static void CreateConfigs(BaseUnityPlugin plugin)
        {
            const string serverSynced = "CharredMinion (Server Synced)";

            CharredBaseHealth = plugin.Config.Bind(serverSynced, "CharredBaseHealth",
                100f, new ConfigDescription("HP = BaseHealth + NecromancyLevel * HealthMultiplier", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CharredHealthMultiplier = plugin.Config.Bind(serverSynced, "CharredHealthMultiplier",
                3f, new ConfigDescription("HP = BaseHealth + NecromancyLevel * HealthMultiplier", null,
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

            ScaleStats(GetCreatedAtLevel());

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
        }

        public void ScaleStats(float necromancyLevel)
        {
            var character = GetComponent<Character>();
            if (character == null)
            {
                Logger.LogError("ScaleStats: Character component is null!");
                return;
            }

            var health = CharredBaseHealth.Value + necromancyLevel * CharredHealthMultiplier.Value;
            character.SetMaxHealth(health);
            character.SetHealth(health);
        }

        public static void ConsumeResources(CharredType charredType)
        {
            var inventory = Player.m_localPlayer.GetInventory();

            switch (charredType)
            {
                case CharredType.Archer:
                    ConsumeRequirements(CharredArcherMinion.ItemsCost, inventory);
                    break;
                default:
                    ConsumeRequirements(CharredWarriorMinion.ItemsCost, inventory);
                    break;
            }
        }

        public static void InstantiateCharred(int quality, float playerNecromancyLevel, CharredType charredType)
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

            var minion = charredType switch
            {
                CharredType.Archer => spawnedChar.AddComponent<CharredArcherMinion>(),
                _ => spawnedChar.AddComponent<CharredWarriorMinion>()
            };
            minion.SetCreatedAtLevel(playerNecromancyLevel);
            minion.ScaleStats(playerNecromancyLevel);

            if (Wand.FollowByDefault.Value)
            {
                minion.Follow(player.gameObject);
            }
            else
            {
                minion.Wait(player.transform.position);
            }

            var levelIncrease = charredType == CharredType.Archer
                ? ArcherNecromancyLevelIncrease.Value
                : NecromancyLevelIncrease.Value;

            player.RaiseSkill(SkillManager.Instance.GetSkill(BasePlugin.NecromancySkillIdentifier).m_skill,
                levelIncrease);

            minion.UndeadMinionMaster = player.GetPlayerName();

            if (DropOnDeath.Value == DropType.Nothing) return;

            var characterDrop = spawnedChar.AddComponent<CharacterDrop>();
            if (DropOnDeath.Value == DropType.Everything)
            {
                switch (charredType)
                {
                    case CharredType.Warrior:
                        GenerateDeathDrops(characterDrop, CharredWarriorMinion.ItemsCost);
                        break;
                    case CharredType.Archer:
                        GenerateDeathDrops(characterDrop, CharredArcherMinion.ItemsCost);
                        break;
                }
            }

            // the component won't be remembered by the game on logout because
            // only what is on the prefab is remembered, so write what we're
            // dropping into the ZDO and restore it on Awake (see RestoreDrops above).
            minion.RecordDrops(characterDrop);
        }
    }
}
