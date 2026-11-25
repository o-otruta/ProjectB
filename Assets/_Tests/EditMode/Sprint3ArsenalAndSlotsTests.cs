using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class Sprint3ArsenalAndSlotsTests
    {
        private readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) UnityEngine.Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();

            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            spawned.Add(obj);
            return obj;
        }

        private sealed class FakeSkillTreeSlotProvider : IAbilitySlotBonusProvider
        {
            public int BonusActive;
            public int BonusPassive;
            public event Action OnSlotBonusesChanged;

            public int GetBonusActiveSlots() => BonusActive;
            public int GetBonusPassiveSlots() => BonusPassive;
            public void NotifyChanged() => OnSlotBonusesChanged?.Invoke();
        }

        [Test]
        public void HeroRuntimeStats_CdrClampsAtHalf_CritMultiplierStartsAt11AndScales_AndCoinMultiplierScalesEconomy()
        {
            var stats = new HeroRuntimeStats();
            Assert.AreEqual(1f, stats.CooldownReductionMultiplier, 0.001f);
            Assert.AreEqual(2f, stats.GetEffectiveCooldown(2f), 0.001f);
            Assert.AreEqual(1.1f, stats.CritMultiplier, 0.001f);

            stats.AddCooldownReductionPercent(25f);
            Assert.AreEqual(0.75f, stats.CooldownReductionMultiplier, 0.001f);
            Assert.AreEqual(1.5f, stats.GetEffectiveCooldown(2f), 0.001f);

            // Clamps at 50% max CDR
            stats.AddCooldownReductionPercent(60f);
            Assert.AreEqual(0.5f, stats.CooldownReduction, 0.001f);
            Assert.AreEqual(0.5f, stats.CooldownReductionMultiplier, 0.001f);
            Assert.AreEqual(1.0f, stats.GetEffectiveCooldown(2f), 0.001f);

            // Crit 0% never crits, 100% always crits at 1.1x base then 1.35x after +0.25x upgrade
            int nonCrit = stats.RollDamage(100f, out bool isCrit0);
            Assert.IsFalse(isCrit0);
            Assert.AreEqual(100, nonCrit);

            stats.AddCritChancePercent(100f);
            int critDmgBase = stats.RollDamage(100f, out bool isCrit100);
            Assert.IsTrue(isCrit100);
            Assert.AreEqual(110, critDmgBase);

            stats.AddCritMultiplier(0.25f);
            Assert.AreEqual(1.35f, stats.CritMultiplier, 0.001f);
            int critDmgUpgraded = stats.RollDamage(100f, out bool isCritUpgraded);
            Assert.IsTrue(isCritUpgraded);
            Assert.AreEqual(135, critDmgUpgraded);

            // Permanent MetaUpgrade CritMultiplier (+0.1x/lvl) stacks onto BASE_CRIT_MULTIPLIER
            using (var metaSave = new SaveManager())
            {
                metaSave.SetMetaUpgradeLevel("crit_multiplier", 3, saveImmediately: false);
                var metaMgr = new MetaUpgradeManager(metaSave);
                var metaStats = new HeroRuntimeStats(metaMgr);
                Assert.AreEqual(1.4f, metaStats.CritMultiplier, 0.001f);
            }

            // Coin multiplier scales HeroEconomy.AddCoin
            stats.AddCoinBonusPercent(50f);
            Assert.AreEqual(1.5f, stats.CoinMultiplier, 0.001f);

            var runStats = new RunStatistics();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("CoinHero"));
            var economy = heroGo.AddComponent<HeroEconomy>();
            economy.Construct(runStats, bus, stats);
            economy.AddCoin(10);
            Assert.AreEqual(15, runStats.CoinsEarned);
        }

        [Test]
        public void EnemyBase_CritDamagePublishesIsCritEvent_AndDamageNumberCachesCritStrings()
        {
            var bus = new GameEventBus();
            EnemyDamagedEvent captured = default;
            int eventCount = 0;
            bus.Subscribe<EnemyDamagedEvent>(e =>
            {
                captured = e;
                eventCount++;
            });

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 200;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("CritEnemy"));
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, null, null, bus, 1f);

            enemy.TakeDamage(42, isCrit: true);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(42, captured.Damage);
            Assert.IsTrue(captured.IsCrit);

            Assert.AreSame(DamageNumberItem.GetCachedCritNumberText(42), DamageNumberItem.GetCachedCritNumberText(42));
            Assert.AreEqual("42!", DamageNumberItem.GetCachedCritNumberText(42));
            Assert.AreEqual("1200!", DamageNumberItem.GetCachedCritNumberText(1200));
        }

        [Test]
        public void AbilitySlots_StartAtTwoEach_RejectThird_UnlockViaAchievementsAndSkillTree_PersistAndLiveUpdateHud()
        {
            var saveManager = new SaveManager();
            try
            {
                var metaManager = new MetaUpgradeManager(saveManager);
                using var slotService = new AbilitySlotService(saveManager, metaManager);
                var achManager = new AchievementManager(saveManager);
                var stats = new HeroRuntimeStats();
                var bus = new GameEventBus();

                Assert.AreEqual(2, slotService.MaxActiveSlots);
                Assert.AreEqual(2, slotService.MaxPassiveSlots);

                var heroGo = Track(new GameObject("SlotTestHero"));
                var heroAbilities = heroGo.AddComponent<HeroAbilities>();
                heroAbilities.Construct(stats, bus, achManager, null, slotService);

                Assert.AreEqual(2, heroAbilities.UnlockedActiveSlots);
                Assert.AreEqual(2, heroAbilities.UnlockedPassiveSlots);

                // Build HUD with 6 active + 6 passive sockets
                var prefabGo = Track(new GameObject("SlotPrefab", typeof(RectTransform), typeof(Image), typeof(AbilityHudSlotUI)));
                var slotPrefab = prefabGo.GetComponent<AbilityHudSlotUI>();
                var bgGo = Track(new GameObject("Bg", typeof(RectTransform), typeof(Image)));
                bgGo.transform.SetParent(prefabGo.transform, false);
                var lvlBadgeGo = Track(new GameObject("LevelBadge", typeof(RectTransform), typeof(Image)));
                lvlBadgeGo.transform.SetParent(prefabGo.transform, false);
                var lvlTmp = Track(new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI)));
                lvlTmp.transform.SetParent(lvlBadgeGo.transform, false);
                SetPrivateField(slotPrefab, "borderImage", prefabGo.GetComponent<Image>());
                SetPrivateField(slotPrefab, "backgroundImage", bgGo.GetComponent<Image>());
                SetPrivateField(slotPrefab, "levelText", lvlTmp.GetComponent<TextMeshProUGUI>());

                var hudGo = Track(new GameObject("AbilityHud", typeof(RectTransform), typeof(AbilityHudUI)));
                var activeRow = Track(new GameObject("ActiveSlots", typeof(RectTransform)));
                activeRow.transform.SetParent(hudGo.transform, false);
                var passiveRow = Track(new GameObject("PassiveSlots", typeof(RectTransform)));
                passiveRow.transform.SetParent(hudGo.transform, false);

                var hud = hudGo.GetComponent<AbilityHudUI>();
                SetPrivateField(hud, "activeSlotsContainer", activeRow.transform);
                SetPrivateField(hud, "passiveSlotsContainer", passiveRow.transform);
                SetPrivateField(hud, "slotPrefab", slotPrefab);
                hud.Construct(heroAbilities);

                // Equip 2 actives + 2 passives; 3rd of each must be rejected
                var actives = new List<ActiveAbilityData>();
                var passives = new List<StatPassiveAbilityData>();
                var cards = new List<CardData>();
                for (int i = 0; i < 4; i++)
                {
                    var a = Track(ScriptableObject.CreateInstance<SeismicHammerAbilityData>());
                    a.id = $"TestAct_{i}";
                    a.abilityName = $"TestAct_{i}";
                    a.type = AbilityType.Active;
                    actives.Add(a);

                    var p = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
                    p.id = $"TestPas_{i}";
                    p.abilityName = $"TestPas_{i}";
                    p.type = AbilityType.Passive;
                    p.effectType = PassiveEffectType.ArmorFlat;
                    p.baseValue = 5f;
                    passives.Add(p);

                    var ac = Track(ScriptableObject.CreateInstance<AbilityCardData>());
                    ac.abilityData = a;
                    cards.Add(ac);

                    var pc = Track(ScriptableObject.CreateInstance<AbilityCardData>());
                    pc.abilityData = p;
                    cards.Add(pc);
                }

                for (int i = 0; i < 2; i++)
                {
                    Assert.IsTrue(heroAbilities.AddAbility(actives[i]));
                    Assert.IsTrue(heroAbilities.AddAbility(passives[i]));
                }

                Assert.IsFalse(heroAbilities.CanAddActive());
                Assert.IsFalse(heroAbilities.CanAddPassive());
                Assert.IsFalse(heroAbilities.AddAbility(actives[2]), "3rd active must be rejected at 2 slots");
                Assert.IsFalse(heroAbilities.AddAbility(passives[2]), "3rd passive must be rejected at 2 slots");

                // Verify UpgradeManager filters out new active/passive ability cards when capped at 3
                var mgrGo = Track(new GameObject("SlotUpgradeMgr"));
                var mgr = mgrGo.AddComponent<UpgradeManager>();
                mgr.cardPool = cards;
                mgr.Construct(null, null, null, heroAbilities, null, saveManager);
                mgr.InitializeRunPool();
                Assert.AreEqual(0, mgr.GetRandomCards(5).Count, "No new ability cards should be offered when 2/2 slots are full");

                // Verify HUD has 2 filled + 4 locked per row
                for (int i = 2; i < 6; i++)
                {
                    Assert.IsTrue(hud.ActiveSlots[i].IsLocked);
                    Assert.IsTrue(hud.PassiveSlots[i].IsLocked);
                }

                // Wave 25 -> active slot 1 (W5) + passive slot 2 (W25); 250 kills -> passive slot 1.
                // Active slot 2 now needs a Golem kill, so actives stay at 3.
                achManager.OnWaveReached(25);
                achManager.AddProgress("unlock_passive_slot_1", 250);

                Assert.AreEqual(3, slotService.MaxActiveSlots);
                Assert.AreEqual(4, slotService.MaxPassiveSlots);
                Assert.IsTrue(heroAbilities.CanAddActive());
                Assert.IsTrue(heroAbilities.CanAddPassive());

                // Live-updated HUD should now show slot 2 as empty unlocked!
                Assert.IsTrue(hud.ActiveSlots[2].IsEmpty);
                Assert.IsFalse(hud.ActiveSlots[2].IsLocked);
                Assert.IsTrue(hud.ActiveSlots[3].IsLocked);
                Assert.IsTrue(hud.PassiveSlots[3].IsEmpty);
                Assert.IsTrue(hud.PassiveSlots[4].IsLocked);

                // 3rd active & passive now equip cleanly
                Assert.IsTrue(heroAbilities.AddAbility(actives[2]));
                Assert.IsTrue(heroAbilities.AddAbility(passives[2]));

                // Persist across SaveManager reload
                saveManager.Save();
                var reloadedSave = new SaveManager();
                try
                {
                    using var reloadedSlots = new AbilitySlotService(reloadedSave, new MetaUpgradeManager(reloadedSave));
                    Assert.AreEqual(3, reloadedSlots.MaxActiveSlots);
                    Assert.AreEqual(4, reloadedSlots.MaxPassiveSlots);
                }
                finally
                {
                    reloadedSave.Dispose();
                }

                // Skill-tree provider expands slots and clamps cleanly at 6+6
                var skillTree = new FakeSkillTreeSlotProvider { BonusActive = 5, BonusPassive = 5 };
                int capacityEvents = 0;
                slotService.OnSlotCapacityChanged += () => capacityEvents++;
                slotService.RegisterBonusProvider(skillTree);
                Assert.AreEqual(6, slotService.MaxActiveSlots);
                Assert.AreEqual(6, slotService.MaxPassiveSlots);
                Assert.GreaterOrEqual(capacityEvents, 1);

                skillTree.BonusActive = 0;
                skillTree.BonusPassive = 0;
                skillTree.NotifyChanged();
                Assert.AreEqual(3, slotService.MaxActiveSlots);
                Assert.AreEqual(4, slotService.MaxPassiveSlots);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void SixNewActiveAbilities_WorkAndScaleWithStats()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("ActiveTestHero"));
            heroGo.transform.position = Vector3.zero;

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 300;
            enemyData.contactDamage = 5;

            EnemyBase SpawnEnemy(string name, Vector3 pos)
            {
                var go = Track(new GameObject(name));
                go.layer = 8;
                go.transform.position = pos;
                go.AddComponent<BoxCollider>();
                var e = go.AddComponent<EnemyBase>();
                e.Initialize(enemyData, heroGo.transform, null, bus, 1f);
                return e;
            }

            var e1 = SpawnEnemy("E1", new Vector3(2f, 0f, 0f));
            var e2 = SpawnEnemy("E2", new Vector3(4f, 0f, 0f));
            var e3 = SpawnEnemy("E3", new Vector3(6f, 0f, 0f));
            Physics.SyncTransforms();

            // 1. Chain Lightning
            var chainData = Track(ScriptableObject.CreateInstance<ChainLightningAbilityData>());
            chainData.id = "ChainLightning";
            chainData.baseDamage = 20f;
            chainData.baseCooldown = 1.4f;
            chainData.bounceCount = 2;
            chainData.searchRadius = 10f;
            chainData.bounceRadius = 5f;
            chainData.targetLayer = 1 << 8;

            var chain = (ChainLightningAbility)chainData.CreateAbility(heroGo.transform);
            chain.SetRuntimeStats(stats);
            chain.SetEventBus(bus);
            chain.Initialize(chainData);
            int hitChainCount = chain.TriggerChainCast();
            Assert.AreEqual(3, hitChainCount, "1 initial + 2 bounces = 3 targets hit");
            Assert.AreEqual(280, e1.CurrentHp);
            Assert.AreEqual(280, e2.CurrentHp);
            Assert.AreEqual(280, e3.CurrentHp);

            // 2. Poison Flask + Poison Puddle
            var poisonData = Track(ScriptableObject.CreateInstance<PoisonFlaskAbilityData>());
            poisonData.id = "PoisonAlchemist";
            poisonData.baseDamage = 15f;
            poisonData.puddleRadius = 3f;
            poisonData.puddleDuration = 4f;
            poisonData.tickInterval = 0.4f;
            poisonData.flaskCount = 1;
            poisonData.throwRange = 10f;
            poisonData.slowFactor = 0.8f;
            poisonData.targetLayer = 1 << 8;

            var poison = (PoisonFlaskAbility)poisonData.CreateAbility(heroGo.transform);
            poison.SetRuntimeStats(stats);
            poison.Initialize(poisonData);
            Assert.AreEqual(1, poison.ThrowFlasks());
            var puddle = poison.SpawnPuddleAt(e1.transform.position);
            Assert.IsNotNull(puddle);
            Assert.AreEqual(265, e1.CurrentHp, "PoisonPuddle deals immediate tick on landing");

            // 3. Guardian Shields (damage + knockback)
            float e1XBeforeShield = e1.transform.position.x;
            var shieldData = Track(ScriptableObject.CreateInstance<GuardianShieldAbilityData>());
            shieldData.id = "GuardianShield";
            shieldData.baseDamage = 12f;
            shieldData.shieldCount = 4;
            shieldData.orbitRadius = 2.0f;
            shieldData.knockbackDistance = 1.5f;
            shieldData.shieldHitRadius = 1.6f;
            shieldData.targetLayer = 1 << 8;

            var shield = (GuardianShieldAbility)shieldData.CreateAbility(heroGo.transform);
            shield.SetRuntimeStats(stats);
            shield.Initialize(shieldData);
            shield.ApplyShieldScan();
            Assert.Less(e1.CurrentHp, 265, "Guardian shield damages adjacent enemy");
            Assert.Greater(e1.transform.position.x, e1XBeforeShield, "Guardian shield pushes enemy away from hero");

            // 4. Mini Dragon
            var dragonData = Track(ScriptableObject.CreateInstance<MiniDragonAbilityData>());
            dragonData.id = "MiniDragon";
            dragonData.baseDamage = 12f;
            dragonData.dragonCount = 1;
            dragonData.coneProjectileCount = 3;
            dragonData.searchRadius = 12f;
            dragonData.targetLayer = 1 << 8;

            var dragon = (MiniDragonAbility)dragonData.CreateAbility(heroGo.transform);
            dragon.SetRuntimeStats(stats);
            dragon.Initialize(dragonData);
            Assert.AreEqual(1, dragon.ActiveDragonCount);
            Assert.AreEqual(3, dragon.FireAllDragons());
            dragon.ApplyModifier(ModifierType.Count, 1f);
            Assert.AreEqual(2, dragon.ActiveDragonCount);
            Assert.AreEqual(6, dragon.FireAllDragons());

            // 5. Voxel Tornado
            var tornadoData = Track(ScriptableObject.CreateInstance<VoxelTornadoAbilityData>());
            tornadoData.id = "VoxelTornado";
            tornadoData.baseDamage = 10f;
            tornadoData.vortexRadius = 5f;
            tornadoData.duration = 4f;
            tornadoData.tornadoCount = 1;
            tornadoData.targetLayer = 1 << 8;

            var tornado = (VoxelTornadoAbility)tornadoData.CreateAbility(heroGo.transform);
            tornado.SetRuntimeStats(stats);
            tornado.Initialize(tornadoData);
            int hpBeforeTornado = e2.CurrentHp;
            Assert.AreEqual(1, tornado.SpawnTornadoes());
            Assert.Less(e2.CurrentHp, hpBeforeTornado, "Tornado deals damage within vortex radius");

            // 6. Seismic Hammer (AoE damage + slow + event)
            int slamEvents = 0;
            bus.Subscribe<SeismicSlamEvent>(_ => slamEvents++);
            var hammerData = Track(ScriptableObject.CreateInstance<SeismicHammerAbilityData>());
            hammerData.id = "EarthquakeSlam";
            hammerData.baseDamage = 45f;
            hammerData.slamRadius = 10f;
            hammerData.slowFactor = 0.2f;
            hammerData.slowDuration = 1.5f;
            hammerData.targetLayer = 1 << 8;

            var hammer = (SeismicHammerAbility)hammerData.CreateAbility(heroGo.transform);
            hammer.SetRuntimeStats(stats);
            hammer.SetEventBus(bus);
            hammer.Initialize(hammerData);
            int hpBeforeSlam = e3.CurrentHp;
            int slamHits = hammer.TriggerSlam();
            Assert.GreaterOrEqual(slamHits, 3);
            Assert.AreEqual(hpBeforeSlam - 45, e3.CurrentHp);
            Assert.AreEqual(1, slamEvents);
        }

        [Test]
        public void FourNewPassives_ApplyEffectsAndRegenForTitanHeart()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.maxHp = 100;
            heroData.passiveType = HeroPassiveType.None;
            heroData.passiveValue = 0f;

            var heroGo = Track(new GameObject("PassiveHero"));
            var health = heroGo.AddComponent<HeroHealth>();
            health.Construct(stats, bus, null);
            SetPrivateField(health, "heroData", heroData);
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);

            // Base is 2 passive slots; the test equips 3 passives -> grant +1 via a skill-tree provider on clean in-memory save
            using var passiveSave = new SaveManager(new InMemorySaveStorage());
            using var passiveSlots = new AbilitySlotService(passiveSave, null);
            passiveSlots.RegisterBonusProvider(new FakeSkillTreeSlotProvider { BonusPassive = 1 });
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, health, passiveSlots);

            var chrono = AssetDatabase.LoadAssetAtPath<StatPassiveAbilityData>("Assets/_ScriptableObjects/Abilities/ChronoTomeData.asset");
            var crit = AssetDatabase.LoadAssetAtPath<StatPassiveAbilityData>("Assets/_ScriptableObjects/Abilities/CriticalLensData.asset");
            var titan = AssetDatabase.LoadAssetAtPath<StatPassiveAbilityData>("Assets/_ScriptableObjects/Abilities/TitanHeartData.asset");
            var idol = AssetDatabase.LoadAssetAtPath<StatPassiveAbilityData>("Assets/_ScriptableObjects/Abilities/GoldenIdolData.asset");

            Assert.IsNotNull(chrono);
            Assert.IsNotNull(crit);
            Assert.IsNotNull(titan);
            Assert.IsNotNull(idol);
            Assert.AreEqual(PassiveEffectType.HealthRegenPerSecond, titan.effectType);

            // Equip ChronoTome (+10% CDR), CriticalLens (+12% Crit, 1.1x mult), TitanHeart (+2 HP/s regen)
            Assert.IsTrue(heroAbilities.AddAbility(chrono));
            Assert.IsTrue(heroAbilities.AddAbility(crit));
            Assert.IsTrue(heroAbilities.AddAbility(titan));

            Assert.AreEqual(0.9f, stats.CooldownReductionMultiplier, 0.001f);
            Assert.AreEqual(0.12f, stats.CritChance, 0.001f);
            Assert.AreEqual(1.1f, stats.CritMultiplier, 0.001f);
            Assert.AreEqual(100, health.MaxHp);

            StatPassiveAbility titanAbility = null;
            foreach (var p in heroAbilities.PassiveAbilities)
            {
                if (p.Data == titan) titanAbility = p as StatPassiveAbility;
            }
            Assert.IsNotNull(titanAbility);
            Assert.AreEqual(2f, titanAbility.CurrentRegenPerSecond, 0.001f);

            // Upgrade ChronoTome (+8% CDR), CriticalLens (+8% Crit & +0.25x CritMultiplier), TitanHeart (+1.5 HP/s)
            heroAbilities.UpgradeAbility("ChronoTome", ModifierType.Cooldown, 8f);
            heroAbilities.UpgradeAbility("CriticalLens", ModifierType.Damage, 8f);
            heroAbilities.UpgradeAbility("CriticalLens", ModifierType.CritMultiplier, 0.25f);
            heroAbilities.UpgradeAbility("TitanHeart", ModifierType.Duration, 1.5f);
            Assert.AreEqual(0.82f, stats.CooldownReductionMultiplier, 0.001f);
            Assert.AreEqual(0.20f, stats.CritChance, 0.001f);
            Assert.AreEqual(1.35f, stats.CritMultiplier, 0.001f);
            Assert.AreEqual(3.5f, titanAbility.CurrentRegenPerSecond, 0.001f);

            // GoldenIdol in a fresh HeroAbilities instance (3 passive cap)
            var stats2 = new HeroRuntimeStats();
            var heroGo2 = Track(new GameObject("IdolHero"));
            var ha2 = heroGo2.AddComponent<HeroAbilities>();
            ha2.Construct(stats2, bus, null, null, null);
            Assert.IsTrue(ha2.AddAbility(idol));
            Assert.AreEqual(1.25f, stats2.CoinMultiplier, 0.001f);
            ha2.UpgradeAbility("GoldenIdol", ModifierType.Area, 20f);
            Assert.AreEqual(1.45f, stats2.CoinMultiplier, 0.001f);
        }

        [Test]
        public void FiveNewSynergies_AutoActivateWithPairsAndPublishSynergyActivatedEvent()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            int synergyEvents = 0;
            string lastSynergyId = null;
            bus.Subscribe<SynergyActivatedEvent>(evt =>
            {
                synergyEvents++;
                lastSynergyId = evt.SynergyId;
            });

            using var saveManager = new SaveManager();
            saveManager.AddBonusActiveSlots(3, false);
            saveManager.AddBonusPassiveSlots(3, false);
            using var slotService = new AbilitySlotService(saveManager, null);

            var heroGo = Track(new GameObject("SynergyHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null, slotService);

            var synergies = new List<SynergyData>
            {
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_Thunderstorm.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_ToxicInferno.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_AegisPhalanx.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_DragonLord.asset"),
                AssetDatabase.LoadAssetAtPath<SynergyData>("Assets/_ScriptableObjects/Synergies/Synergy_CataclysmVortex.asset"),
            };

            foreach (var syn in synergies)
            {
                Assert.IsNotNull(syn);
            }

            heroAbilities.SetAvailableSynergies(synergies);

            var applier = new UpgradeApplier(null, stats, heroAbilities, bus);
            var mgrGo = Track(new GameObject("SynergyUpgradeMgr"));
            var mgr = mgrGo.AddComponent<UpgradeManager>();
            mgr.SetCardCatalog(Track(ScriptableObject.CreateInstance<CardCatalog>()));
            mgr.synergies = synergies;
            mgr.Construct(null, null, null, heroAbilities, applier, saveManager);
            mgr.InitializeRunPool();

            var sword = AssetDatabase.LoadAssetAtPath<AbilityData>("Assets/_ScriptableObjects/Abilities/OrbitalSmallSwordData.asset");
            var guardian = AssetDatabase.LoadAssetAtPath<AbilityData>("Assets/_ScriptableObjects/Abilities/GuardianShieldData.asset");
            Assert.IsTrue(heroAbilities.AddAbility(sword));
            Assert.AreEqual(0, synergyEvents);
            Assert.IsFalse(heroAbilities.HasSynergy("aegis_phalanx"));

            Assert.IsTrue(heroAbilities.AddAbility(guardian));
            Assert.AreEqual(1, synergyEvents);
            Assert.AreEqual("aegis_phalanx", lastSynergyId);
            Assert.IsTrue(heroAbilities.HasSynergy("aegis_phalanx"));
            Assert.AreEqual(1, heroAbilities.GetAbilityLevel("orbital_small_sword"));
            Assert.AreEqual(1, heroAbilities.GetAbilityLevel("GuardianShield"));
            Assert.AreEqual(0f, stats.Armor, 0.001f);
            Assert.AreEqual(0, mgr.GetRandomCards(5).Count, "Synergies must not appear in LevelUp card pool");
        }

        [Test]
        public void Sprint3AssetsCatalogAchievementsAndGameplayScene_AreProperlyWired()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>("Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset");
            Assert.IsNotNull(catalog);
            Assert.GreaterOrEqual(catalog.abilities.Count, 21);
            foreach (var ab in catalog.abilities)
            {
                Assert.IsNotNull(ab);
                Assert.IsNotNull(ab.icon, $"Ability {ab.id} missing icon");
            }

            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/UI/AbilityHudSlot.prefab");
            Assert.IsNotNull(slotPrefab);
            var slotUi = slotPrefab.GetComponent<AbilityHudSlotUI>();
            Assert.IsNotNull(slotUi);
            Assert.IsNotNull(GetPrivateField(slotUi, "lockedIconSprite"), "AbilityHudSlot.prefab must have lockedIconSprite wired");

            // Check 6 slot achievements + 5 ability unlock achievements
            using var saveManager = new SaveManager();
            var achMgr = new AchievementManager(saveManager);
            var allAch = achMgr.GetAllAchievements();
            Assert.GreaterOrEqual(allAch.Count, 21);

            var expectedAbilityAch = new Dictionary<string, string>
            {
                ["collect_5_abilities"] = "MiniDragon",
                ["kill_1000_enemies"] = "VoxelTornado",
                ["reach_wave_50"] = "EarthquakeSlam",
                ["no_damage_5_waves"] = "CriticalLens",
                ["find_5_chests"] = "GoldenIdol"
            };

            int slotActiveCount = 0;
            int slotPassiveCount = 0;
            foreach (var ach in allAch)
            {
                Assert.IsNotNull(ach.icon, $"Achievement {ach.id} missing icon");
                if (expectedAbilityAch.TryGetValue(ach.id, out string expectedAbility))
                {
                    Assert.AreEqual(AchievementRewardType.UnlockAbility, ach.rewardType, ach.id);
                    Assert.AreEqual(expectedAbility, ach.rewardId, ach.id);
                }
                if (ach.rewardType == AchievementRewardType.UnlockActiveSlot) slotActiveCount++;
                if (ach.rewardType == AchievementRewardType.UnlockPassiveSlot) slotPassiveCount++;
            }
            Assert.AreEqual(3, slotActiveCount, "Expected 3 UnlockActiveSlot achievements (3 -> 6 slots)");
            Assert.AreEqual(3, slotPassiveCount, "Expected 3 UnlockPassiveSlot achievements (3 -> 6 slots)");

            // Verify Gameplay.unity UpgradeManager wiring
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                UpgradeManager upgradeMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    upgradeMgr = root.GetComponentInChildren<UpgradeManager>(true);
                    if (upgradeMgr != null) break;
                }

                Assert.IsNotNull(upgradeMgr);
                Assert.GreaterOrEqual(upgradeMgr.synergies.Count, 8, "Expected 3 MVP + 5 Sprint 3 synergies");

                CardCatalog cardCatalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
                Assert.IsNotNull(cardCatalog, "Missing Resources/CardCatalog.asset");

                var expectedCardNames = new[]
                {
                    "ChainLightningCard", "PoisonFlaskCard", "GuardianShieldCard",
                    "MiniDragonCard", "VoxelTornadoCard", "SeismicHammerCard",
                    "ChronoTomeCard", "CriticalLensCard", "TitanHeartCard", "GoldenIdolCard"
                };

                var catalogCards = new List<CardData>(cardCatalog.cards);
                foreach (var cardName in expectedCardNames)
                {
                    var card = AssetDatabase.LoadAssetAtPath<AbilityCardData>($"Assets/_ScriptableObjects/Cards/{cardName}.asset");
                    Assert.IsNotNull(card, $"Missing card asset {cardName}");
                    Assert.Contains(card, catalogCards, $"Resources/CardCatalog missing {cardName}");
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            // Verify CritMultiplier meta upgrade & modifier card exist with generated icons
            var critMeta = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>("Assets/Resources/MetaUpgrades/crit_multiplier.asset");
            Assert.IsNotNull(critMeta, "Missing crit_multiplier MetaUpgradeData");
            Assert.AreEqual(MetaUpgradeEffectType.CritMultiplier, critMeta.effectType);
            Assert.IsNotNull(critMeta.icon, "crit_multiplier missing generated icon");

            var critLensCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/CriticalLensCard.asset");
            var critMultMod = AssetDatabase.LoadAssetAtPath<AbilityModifierCardData>("Assets/_ScriptableObjects/Cards/CriticalLensMod_Multiplier.asset");
            Assert.IsNotNull(critMultMod, "Missing CriticalLensMod_Multiplier");
            Assert.AreEqual(ModifierType.CritMultiplier, critMultMod.modifierType);
            Assert.AreEqual(0.25f, critMultMod.value, 0.001f);
            Assert.IsNotNull(critMultMod.icon, "CriticalLensMod_Multiplier missing generated icon");
            Assert.Contains(critMultMod, critLensCard.unlockedModifiers);
        }

        [Test]
        public void DebugCheatMenu_GrantOrUpgradeAbility_EquipsLockedAbilitiesRespectsSlotsAndLevelsUp()
        {
            using var saveManager = new SaveManager();
            using var slotService = new AbilitySlotService(saveManager, null);
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();

            var heroGo = Track(new GameObject("CheatTestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null, slotService);

            var applier = new UpgradeApplier(null, stats, heroAbilities, bus);
            var mgrGo = Track(new GameObject("CheatUpgradeMgr"));
            var upgradeMgr = mgrGo.AddComponent<UpgradeManager>();

            var miniDragonCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/MiniDragonCard.asset");
            var chainCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/ChainLightningCard.asset");
            var guardianCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/GuardianShieldCard.asset");
            var poisonCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/PoisonFlaskCard.asset");
            var titanCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>("Assets/_ScriptableObjects/Cards/TitanHeartCard.asset");

            upgradeMgr.cardPool = new List<CardData> { miniDragonCard, chainCard, guardianCard, poisonCard, titanCard };
            upgradeMgr.Construct(null, null, null, heroAbilities, applier, saveManager);
            upgradeMgr.InitializeRunPool();

            var cheatGo = Track(new GameObject("CheatMenu"));
            var cheatMenu = cheatGo.AddComponent<DebugCheatMenu>();
            cheatMenu.Construct(null, null, null, null, null, null, null, saveManager, heroAbilities, upgradeMgr, applier);

            // 1. Can grant even locked-by-default ability (MiniDragon) and level it up repeatedly
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(miniDragonCard));
            Assert.AreEqual(1, heroAbilities.GetAbilityLevel("MiniDragon"));
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(miniDragonCard));
            Assert.AreEqual(2, heroAbilities.GetAbilityLevel("MiniDragon"));
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(miniDragonCard));
            Assert.AreEqual(3, heroAbilities.GetAbilityLevel("MiniDragon"));

            // 2. Respects 2-slot base cap for active abilities
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(chainCard));
            Assert.IsFalse(cheatMenu.GrantOrUpgradeAbility(guardianCard), "3rd active must be blocked when 2 active slots are full");
            Assert.IsFalse(cheatMenu.GrantOrUpgradeAbility(poisonCard), "3rd active must be blocked when 2 active slots are full");
            Assert.IsFalse(heroAbilities.HasAbility("PoisonAlchemist"));

            // 3. Grants and levels up passive ability (TitanHeart)
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(titanCard));
            Assert.AreEqual(1, heroAbilities.GetAbilityLevel("TitanHeart"));
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(titanCard));
            Assert.AreEqual(2, heroAbilities.GetAbilityLevel("TitanHeart"));
        }

        [Test]
        public void DebugCheatMenu_AbilityPicker_DisplaysAllActivesAndPassivesWithoutClipping()
        {
            List<CardData> realCardPool;
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                UpgradeManager sceneMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    sceneMgr = root.GetComponentInChildren<UpgradeManager>(true);
                    if (sceneMgr != null) break;
                }

                Assert.IsNotNull(sceneMgr);
                CardCatalog catalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
                Assert.IsNotNull(catalog);
                realCardPool = new List<CardData>(catalog.cards);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            using var saveManager = new SaveManager();
            using var slotService = new AbilitySlotService(saveManager, null);
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();

            var heroGo = Track(new GameObject("PickerHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null, slotService);

            var applier = new UpgradeApplier(null, stats, heroAbilities, bus);
            var mgrGo = Track(new GameObject("PickerUpgradeMgr"));
            var upgradeMgr = mgrGo.AddComponent<UpgradeManager>();
            upgradeMgr.cardPool = realCardPool;
            upgradeMgr.Construct(null, null, null, heroAbilities, applier, saveManager);
            upgradeMgr.InitializeRunPool();

            var canvasGo = Track(new GameObject("PickerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(1080f, 1920f);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var cheatGo = Track(new GameObject("DebugCheatMenuGo", typeof(RectTransform)));
            cheatGo.transform.SetParent(canvasGo.transform, false);
            var cheatRt = cheatGo.GetComponent<RectTransform>();
            cheatRt.anchorMin = Vector2.zero;
            cheatRt.anchorMax = Vector2.one;
            cheatRt.offsetMin = Vector2.zero;
            cheatRt.offsetMax = Vector2.zero;

            var cheatMenu = cheatGo.AddComponent<DebugCheatMenu>();
            cheatMenu.Construct(null, null, null, null, null, null, null, saveManager, heroAbilities, upgradeMgr, applier);

            typeof(DebugCheatMenu).GetMethod("BuildOverlayUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cheatMenu, null);
            typeof(DebugCheatMenu).GetMethod("OpenAbilityPicker", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cheatMenu, null);
            Canvas.ForceUpdateCanvases();

            var pickerPanel = (GameObject)GetPrivateField(cheatMenu, "abilityPickerPanel");
            var scrollGo = pickerPanel.transform.Find("AbilityScroll");
            Assert.IsNotNull(scrollGo);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(scrollRt.rect.height, 700f, "AbilityScroll viewport must not collapse/clip at 100px");

            var contentTransform = (Transform)GetPrivateField(cheatMenu, "pickerContentTransform");
            var contentRt = contentTransform.GetComponent<RectTransform>();
            Assert.AreEqual(17, contentTransform.childCount, "Expected all 17 Active abilities in ACTIVES tab");
            Assert.GreaterOrEqual(contentRt.rect.height, 600f, "Grid content height must fit all active rows");

            var activesLabel = (TextMeshProUGUI)GetPrivateField(cheatMenu, "activesTabLabel");
            var passivesLabel = (TextMeshProUGUI)GetPrivateField(cheatMenu, "passivesTabLabel");
            Assert.AreEqual("ACTIVES (17)", activesLabel.text);
            Assert.AreEqual("PASSIVES (10)", passivesLabel.text);

            var passivesBtn = (Button)GetPrivateField(cheatMenu, "passivesTabBtn");
            passivesBtn.onClick.Invoke();
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(10, contentTransform.childCount, "Expected all 10 Passive abilities in PASSIVES tab with no leftovers");
        }

        [Test]
        public void DebugCheatMenu_AbilityPicker_LoadsFromCardCatalogWhenCardPoolIsNull()
        {
            CardCatalog catalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(catalog, "Resources/CardCatalog.asset must exist");

            using var saveManager = new SaveManager();
            using var slotService = new AbilitySlotService(saveManager, null);
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();

            var heroGo = Track(new GameObject("CatalogPickerHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null, slotService);

            var applier = new UpgradeApplier(null, stats, heroAbilities, bus);
            var mgrGo = Track(new GameObject("CatalogPickerUpgradeMgr"));
            var upgradeMgr = mgrGo.AddComponent<UpgradeManager>();
            // Production setup: UpgradeManager.cardPool is null; cards come from injected CardCatalog.
            Assert.IsNull(upgradeMgr.cardPool);
            upgradeMgr.Construct(null, null, null, heroAbilities, applier, saveManager, cardCatalog: catalog);
            upgradeMgr.InitializeRunPool();

            var canvasGo = Track(new GameObject("CatalogPickerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 1920f);

            var cheatGo = Track(new GameObject("CatalogCheatMenuGo", typeof(RectTransform)));
            cheatGo.transform.SetParent(canvasGo.transform, false);
            var cheatMenu = cheatGo.AddComponent<DebugCheatMenu>();
            cheatMenu.Construct(null, null, null, null, null, null, null, saveManager, heroAbilities, upgradeMgr, applier);

            typeof(DebugCheatMenu).GetMethod("BuildOverlayUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cheatMenu, null);
            typeof(DebugCheatMenu).GetMethod("OpenAbilityPicker", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cheatMenu, null);
            Canvas.ForceUpdateCanvases();

            var contentTransform = (Transform)GetPrivateField(cheatMenu, "pickerContentTransform");
            var activesLabel = (TextMeshProUGUI)GetPrivateField(cheatMenu, "activesTabLabel");
            var passivesLabel = (TextMeshProUGUI)GetPrivateField(cheatMenu, "passivesTabLabel");

            Assert.AreEqual(17, contentTransform.childCount, "Ability picker must list all 17 Active abilities from CardCatalog when cardPool is null");
            Assert.AreEqual("ACTIVES (17)", activesLabel.text);
            Assert.AreEqual("PASSIVES (10)", passivesLabel.text);

            var passivesBtn = (Button)GetPrivateField(cheatMenu, "passivesTabBtn");
            passivesBtn.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(10, contentTransform.childCount, "Ability picker must list all 10 Passive abilities from CardCatalog");

            var cachedCards = (List<AbilityCardData>)GetPrivateField(cheatMenu, "cachedCards");
            Assert.AreEqual(27, cachedCards.Count);
            AbilityCardData sampleActive = cachedCards.Find(c => c.abilityData != null && c.abilityData.type == AbilityType.Active);
            Assert.IsNotNull(sampleActive);
            Assert.IsTrue(cheatMenu.GrantOrUpgradeAbility(sampleActive));
            Assert.IsTrue(heroAbilities.HasAbility(sampleActive.abilityData.id));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}
