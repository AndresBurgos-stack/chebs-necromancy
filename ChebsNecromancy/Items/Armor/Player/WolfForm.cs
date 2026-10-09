using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace ChebsNecromancy.Items.Armor.Player
{
    internal static class WolfForm
    {
        public const string FlagKey = "ChebWolfForm";

        public static ConfigEntry<float> Duration;
        public static ConfigEntry<float> Cooldown;
        public static ConfigEntry<float> SpeedMod;
        public static ConfigEntry<float> NoiseMod;
        public static ConfigEntry<float> StealthMod;
        public static ConfigEntry<float> FallMod;
        public static ConfigEntry<float> PoofScale;
        public static ConfigEntry<float> AttackDamageMult;
        public static ConfigEntry<float> AttackRange;
        public static ConfigEntry<float> AttackArc;
        public static ConfigEntry<float> AttackInterval;
        public static ConfigEntry<string> MeatFoods;
        public static ConfigEntry<float> MeatBonusHP;
        public static ConfigEntry<float> MeatBonusStamina;
        public static ConfigEntry<KeyCode> ToggleKey;

        public static SE_Stats WolfFormEffect;

        private static ConfigEntry<KeyCode> _toggleKeyConfig;
        private static ButtonConfig _toggleButton;
        private static bool _toggleWasDown;
        private static float _cooldownUntil;
        private static float _transformStartTime;

        private class WolfVisual
        {
            public GameObject Wolf;
            public List<Renderer> Hidden = new();
            public Vector3 PrevPos;
            public string SpeedParam;
            public bool ParamsLogged;
            public string AttackTrigger;
            public bool AttackIsTrigger;
            public bool AttackLogged;
            public bool WasTransformed;
        }

        private static readonly Dictionary<global::Player, WolfVisual> _visuals = new();

        public static void CreateConfigs(BaseUnityPlugin plugin)
        {
            const string serverSynced = "WolfForm (Server Synced)";
            const string client = "WolfForm (Client)";

            Duration = plugin.Config.Bind(serverSynced, "WolfFormDuration",
                60f, new ConfigDescription("Seconds the wolf form lasts before expiring.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Cooldown = plugin.Config.Bind(serverSynced, "WolfFormCooldown",
                120f, new ConfigDescription("Seconds before wolf form can be used again after it ends.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SpeedMod = plugin.Config.Bind(serverSynced, "WolfFormSpeedMod",
                0.8f, new ConfigDescription("Extra move speed while transformed.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NoiseMod = plugin.Config.Bind(serverSynced, "WolfFormNoiseMod",
                -100f, new ConfigDescription("Noise reduction while transformed (more negative = quieter).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            StealthMod = plugin.Config.Bind(serverSynced, "WolfFormStealthMod",
                5f, new ConfigDescription("Extra stealth while transformed.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            FallMod = plugin.Config.Bind(serverSynced, "WolfFormFallDamageMod",
                0.5f, new ConfigDescription("Fall damage multiplier while transformed.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PoofScale = plugin.Config.Bind(serverSynced, "WolfFormPoofScale",
                3.5f, new ConfigDescription("Size multiplier of the transform poof effect.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AttackDamageMult = plugin.Config.Bind(serverSynced, "WolfFormAttackDamageMult",
                1f, new ConfigDescription("Damage multiplier of the wolf bite, applied to the equipped weapon's damages (or base claws when unarmed).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AttackRange = plugin.Config.Bind(serverSynced, "WolfFormAttackRange",
                2.6f, new ConfigDescription("Reach of the wolf bite in meters.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AttackArc = plugin.Config.Bind(serverSynced, "WolfFormAttackArc",
                100f, new ConfigDescription("Frontal arc of the wolf bite in degrees.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AttackInterval = plugin.Config.Bind(serverSynced, "WolfFormAttackInterval",
                0.9f, new ConfigDescription("Seconds between wolf bites.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MeatFoods = plugin.Config.Bind(serverSynced, "WolfFormMeatFoods",
                "RawMeat,DeerMeat,WolfMeat,SerpentMeat,HareMeat,BugMeat,ChickenMeat,NeckTail,LoxMeat,AsksvinMeat",
                new ConfigDescription("Raw meats that grant bonus HP/stamina when eaten transformed (comma-delimited prefab names).", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MeatBonusHP = plugin.Config.Bind(serverSynced, "WolfFormMeatBonusHP",
                30f, new ConfigDescription("Bonus HP when eating raw meat transformed.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MeatBonusStamina = plugin.Config.Bind(serverSynced, "WolfFormMeatBonusStamina",
                35f, new ConfigDescription("Bonus stamina when eating raw meat transformed.", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ToggleKey = plugin.Config.Bind(client, "WolfFormToggle",
                KeyCode.K, new ConfigDescription("The key to transform into a wolf and back while the cloak is equipped."));
        }

        public static void RegisterStatusEffect(Sprite icon)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "ChebGonaz_WolfForm";
            se.m_name = "$se_chebgonaz_wolfform";
            se.m_tooltip = "$se_chebgonaz_wolfform_desc";
            se.m_icon = icon;
            se.m_ttl = Duration.Value;
            se.m_cooldown = 0f;
            se.m_speedModifier = SpeedMod.Value;
            se.m_noiseModifier = NoiseMod.Value;
            se.m_stealthModifier = StealthMod.Value;
            se.m_fallDamageModifier = FallMod.Value;
            WolfFormEffect = se;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, true));
        }

        public static void CreateButtons()
        {
            _toggleKeyConfig = ToggleKey;
            if (_toggleKeyConfig.Value == KeyCode.None) return;

            _toggleButton = new ButtonConfig
            {
                Name = WolfCloak.CloakItemName + "Toggle",
                Config = _toggleKeyConfig,
                HintToken = "$chebgonaz_wolftoggle",
                BlockOtherInputs = true
            };
            InputManager.Instance.AddButton(BasePlugin.PluginGuid, _toggleButton);
            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig
            {
                Item = WolfCloak.CloakItemName,
                ButtonConfigs = new[] { _toggleButton }
            });
        }

        public static bool IsTransformed(global::Player player)
        {
            var view = player?.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            if (zdo == null) return false;
            return zdo.GetBool(FlagKey, false);
        }

        private static bool HasWolfFormSE(global::Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return false;
            foreach (var se in seman.GetStatusEffects())
            {
                if (se != null && se.m_name == "$se_chebgonaz_wolfform") return true;
            }
            return false;
        }

        private static bool CloakEquipped(global::Player player)
        {
            var inventory = player.GetInventory();
            if (inventory == null) return false;
            foreach (var equippedItem in inventory.GetEquippedItems())
            {
                if (equippedItem.TokenName().Equals("$item_chebgonaz_wolfcloak")) return true;
            }
            return false;
        }

        public static void HandleUpdate()
        {
            var player = global::Player.m_localPlayer;
            if (player == null || player.IsDead()) return;

            var down = _toggleButton != null && ZInput.GetButton(_toggleButton.Name);
            if (down && !_toggleWasDown) OnTogglePressed(player);
            _toggleWasDown = down;

            if (!CloakEquipped(player) && IsTransformed(player))
            {
                Logger.LogWarning("WolfForm: auto-detransform (cloak missing).");
                DoDetransform(player, true, true);
            }
            else if (IsTransformed(player) && !HasWolfFormSE(player))
            {
                Logger.LogWarning("WolfForm: auto-detransform (SE missing).");
                DoDetransform(player, true, true);
            }
        }

        private static void OnTogglePressed(global::Player player)
        {
            if (!CloakEquipped(player)) return;

            if (IsTransformed(player))
            {
                Logger.LogWarning("WolfForm: manual toggle off.");
                DoDetransform(player, true, true);
                return;
            }

            if (Time.time < _cooldownUntil)
            {
                var left = Mathf.CeilToInt(_cooldownUntil - Time.time);
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                    $"{BasePlugin.Localization.TryTranslate("$chebgonaz_wolfform_cooldown")} {left}s");
                return;
            }

            player.GetSEMan().AddStatusEffect(WolfFormEffect, false, 0, 0f, 0);
            SetTransformed(player, true);
            _transformStartTime = Time.time;
            if (player.GetComponent<WolfFormHud>() == null) player.gameObject.AddComponent<WolfFormHud>();
            Logger.LogWarning($"WolfForm: toggle applied (se={HasWolfFormSE(player)}, flag={IsTransformed(player)}).");
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                BasePlugin.Localization.TryTranslate("$chebgonaz_wolfform_on"));
        }

        private static void SetTransformed(global::Player player, bool transformed)
        {
            var view = player.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            if (zdo == null) return;
            zdo.Set(FlagKey, transformed);
        }

        public static void DoDetransform(global::Player player, bool withCooldown, bool poof)
        {
            if (player.GetSEMan() != null && WolfFormEffect != null)
            {
                player.GetSEMan().RemoveStatusEffect(WolfFormEffect, false);
            }
            SetTransformed(player, false);
            if (withCooldown) _cooldownUntil = Time.time + Cooldown.Value;
            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                    BasePlugin.Localization.TryTranslate("$chebgonaz_wolfform_off"));
            }
            if (poof) SpawnPoof(player.transform.position);
            CleanupVisual(player);
        }

        public static void CombatDetransform(global::Player player)
        {
            Logger.LogWarning("WolfForm: combat input detransforms.");
            DoDetransform(player, true, true);
        }

        public static float RemainingSeconds()
        {
            return Mathf.Max(0f, _transformStartTime + Duration.Value - Time.time);
        }

        public static bool IsBonusMeat(string prefabName, string sharedName)
        {
            if (MeatFoods == null) return false;
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(prefabName)) candidates.Add(NormalizeFoodName(prefabName));
            if (!string.IsNullOrEmpty(sharedName)) candidates.Add(NormalizeFoodName(sharedName));
            if (candidates.Count == 0) return false;
            foreach (var entry in MeatFoods.Value.Split(','))
            {
                if (candidates.Contains(NormalizeFoodName(entry))) return true;
            }
            Logger.LogWarning($"WolfForm: ate '{prefabName}' transformed, no bonus (not in list).");
            return false;
        }

        private static string NormalizeFoodName(string name)
        {
            var clean = name.Trim().ToLowerInvariant();
            if (clean.StartsWith("$item_")) clean = clean.Substring("$item_".Length);
            return clean;
        }

        private static readonly List<CookingStation> _cookingStations = new List<CookingStation>();

        public static ItemDrop GetCookedCounterpart(ItemDrop.ItemData rawItem)
        {
            var rawName = rawItem?.m_dropPrefab?.name;
            if (string.IsNullOrEmpty(rawName)) return null;
            if (_cookingStations.Count == 0 && ZNetScene.instance != null)
            {
                foreach (var prefabName in new[] { "CookingStation", "CookingStation_BlackForge", "BlackForgeCookingStation" })
                {
                    var station = ZNetScene.instance.GetPrefab(prefabName)?.GetComponent<CookingStation>();
                    if (station != null && !_cookingStations.Contains(station)) _cookingStations.Add(station);
                }
                var live = Object.FindObjectOfType<CookingStation>();
                if (live != null && !_cookingStations.Contains(live)) _cookingStations.Add(live);
            }
            var conversionsField = AccessTools.Field(typeof(CookingStation), "m_conversion");
            if (conversionsField == null) return null;
            foreach (var station in _cookingStations)
            {
                if (station == null) continue;
                var list = conversionsField.GetValue(station) as System.Collections.IList;
                if (list == null) continue;
                foreach (var conv in list)
                {
                    if (conv == null) continue;
                    var convType = conv.GetType();
                    var from = AccessTools.Field(convType, "m_from")?.GetValue(conv) as ItemDrop;
                    if (from == null || from.gameObject == null || from.gameObject.name != rawName) continue;
                    var to = AccessTools.Field(convType, "m_to")?.GetValue(conv) as ItemDrop;
                    if (to != null && to.gameObject != null) return to;
                }
            }
            return null;
        }

        public class WolfFormHud : MonoBehaviour
        {
            private GUIStyle _style;

            private void OnGUI()
            {
                var player = global::Player.m_localPlayer;
                if (player == null || !WolfForm.IsTransformed(player)) return;
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                }
                var remain = WolfForm.RemainingSeconds();
                var label = $"WOLF {Mathf.FloorToInt(remain / 60)}:{Mathf.FloorToInt(remain % 60):00}";
                GUI.Label(new Rect(Screen.width / 2f - 80f, Screen.height - 200f, 160f, 30f), label, _style);
            }
        }

        private static void SpawnPoof(Vector3 position)
        {
            var prefab = PrefabManager.Instance.GetPrefab("fx_ArtisanPress_Poof");
            if (prefab == null)
            {
                Logger.LogWarning("WolfForm: poof prefab not found.");
                return;
            }
            var poof = Object.Instantiate(prefab, position + Vector3.up, Quaternion.identity);
            poof.transform.localScale = Vector3.one * PoofScale.Value;
        }

        private static float _attackReadyAt;

        public static void TryWolfAttack(global::Player player)
        {
            if (Time.time < _attackReadyAt) return;
            _attackReadyAt = Time.time + AttackInterval.Value;

            var visual = _visuals.TryGetValue(player, out var v) ? v : null;
            var animator = visual?.Wolf?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (visual.AttackTrigger == null && !visual.AttackLogged)
                {
                    visual.AttackLogged = true;
                    foreach (var param in animator.parameters)
                    {
                        if ((param.type == AnimatorControllerParameterType.Trigger
                                || param.type == AnimatorControllerParameterType.Bool)
                            && (param.name.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || param.name.IndexOf("bite", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || param.name.IndexOf("hit", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            visual.AttackTrigger = param.name;
                            visual.AttackIsTrigger = param.type == AnimatorControllerParameterType.Trigger;
                            break;
                        }
                    }
                    Logger.LogWarning($"WolfForm: attack trigger '{visual.AttackTrigger ?? "<none>"}'.");
                }
                if (visual.AttackTrigger != null)
                {
                    if (visual.AttackIsTrigger) animator.SetTrigger(visual.AttackTrigger);
                    else animator.SetBool(visual.AttackTrigger, true);
                }
            }

            var weaponDamages = player.GetRightItem()?.m_shared?.m_damages;
            var hasWeapon = weaponDamages.HasValue;
            var damages = weaponDamages ?? new HitData.DamageTypes();
            var forward = player.transform.forward;
            var range = AttackRange.Value;
            var halfArc = AttackArc.Value * 0.5f;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character == player || character.IsDead()) continue;
                if (character is global::Player) continue;
                var minion = character.GetComponent<ChebsValheimLibrary.Minions.ChebGonazMinion>();
                if (minion != null && minion.BelongsToPlayer(player.GetPlayerName())) continue;
                var toTarget = character.transform.position - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > range) continue;
                if (Vector3.Angle(forward, toTarget.normalized) > halfArc) continue;

                var hit = new HitData();
                hit.m_attacker = player.GetZDOID();
                hit.m_point = character.GetCenterPoint();
                hit.m_dir = forward;
                hit.m_pushForce = 5f;
                var mult = AttackDamageMult.Value;
                if (hasWeapon)
                {
                    hit.m_damage.m_blunt = damages.m_blunt * mult;
                    hit.m_damage.m_slash = damages.m_slash * mult;
                    hit.m_damage.m_pierce = damages.m_pierce * mult;
                    hit.m_damage.m_chop = damages.m_chop * mult;
                    hit.m_damage.m_pickaxe = damages.m_pickaxe * mult;
                    hit.m_damage.m_fire = damages.m_fire * mult;
                    hit.m_damage.m_frost = damages.m_frost * mult;
                    hit.m_damage.m_lightning = damages.m_lightning * mult;
                    hit.m_damage.m_poison = damages.m_poison * mult;
                    hit.m_damage.m_spirit = damages.m_spirit * mult;
                }
                else
                {
                    hit.m_damage.m_slash = 25f * mult;
                }
                character.Damage(hit);
            }
        }

        public static void PollVisuals()
        {
            var players = global::Player.GetAllPlayers();
            if (players == null) return;

            var seen = new HashSet<global::Player>(players);
            var gone = new List<global::Player>();
            foreach (var tracked in _visuals.Keys)
            {
                if (!seen.Contains(tracked)) gone.Add(tracked);
            }
            foreach (var old in gone)
            {
                CleanupVisual(old);
                _visuals.Remove(old);
            }

            foreach (var player in players)
            {
                if (player == null) continue;
                var transformed = !player.IsDead() && IsTransformed(player);
                if (!_visuals.TryGetValue(player, out var visual))
                {
                    visual = new WolfVisual();
                    _visuals[player] = visual;
                }

                if (transformed && visual.WasTransformed != transformed)
                {
                    SpawnPoof(player.transform.position);
                }
                visual.WasTransformed = transformed;

                if (transformed)
                {
                    EnsureVisual(player, visual);
                }
                else
                {
                    CleanupVisual(player);
                }
            }
        }

        private static void EnsureVisual(global::Player player, WolfVisual visual)
        {
            if (visual.Wolf == null)
            {
                var wolfPrefab = PrefabManager.Instance.GetPrefab("Wolf");
                if (wolfPrefab == null)
                {
                    Logger.LogError("WolfForm: vanilla Wolf prefab not found!");
                    return;
                }
                var wolf = Object.Instantiate(wolfPrefab, player.transform.position, player.transform.rotation);
                // Strip networking first so a half-stripped clone can never linger
                // registered in ZNetScene (that spams RemoveObjects NREs); each
                // destroy is guarded so one bad teardown can't abort the rest.
                var stripOrder = new List<string>
                {
                    "ZNetView", "ZSyncTransform",
                    "CharacterDrop", "Tameable", "MonsterAI", "FootStep",
                    "CapsuleCollider", "Rigidbody", "Humanoid", "Character"
                };
                foreach (var typeName in stripOrder)
                {
                    foreach (var component in wolf.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null || component.GetType().Name != typeName) continue;
                        try
                        {
                            Object.Destroy(component);
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogWarning($"WolfForm: strip {typeName} failed ({ex.GetType().Name}).");
                        }
                    }
                }
                foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    visual.Hidden.Add(renderer);
                }
                visual.Wolf = wolf;
                visual.PrevPos = player.transform.position;
            }

            visual.Wolf.transform.position = player.transform.position;
            visual.Wolf.transform.rotation = player.transform.rotation;

            var animator = visual.Wolf.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (visual.SpeedParam == null)
                {
                    foreach (var param in animator.parameters)
                    {
                        if (param.type == AnimatorControllerParameterType.Float
                            && (param.name.IndexOf("speed", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || param.name.IndexOf("move", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || param.name.IndexOf("forward", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            visual.SpeedParam = param.name;
                            break;
                        }
                    }
                    if (!visual.ParamsLogged)
                    {
                        visual.ParamsLogged = true;
                        Logger.LogWarning($"WolfForm: driving animator param '{visual.SpeedParam ?? "<none>"}'.");
                    }
                }
                if (visual.SpeedParam != null)
                {
                    var velocity = (player.transform.position - visual.PrevPos) / Mathf.Max(Time.deltaTime, 0.001f);
                    velocity.y = 0f;
                    animator.SetFloat(visual.SpeedParam, velocity.magnitude);
                }
            }
            visual.PrevPos = player.transform.position;
        }

        private static void CleanupVisual(global::Player player)
        {
            if (!_visuals.TryGetValue(player, out var visual)) return;
            if (visual.Wolf != null)
            {
                Object.Destroy(visual.Wolf);
                visual.Wolf = null;
            }
            foreach (var renderer in visual.Hidden)
            {
                if (renderer != null) renderer.enabled = true;
            }
            visual.Hidden.Clear();
            visual.SpeedParam = null;
        }
    }
}
