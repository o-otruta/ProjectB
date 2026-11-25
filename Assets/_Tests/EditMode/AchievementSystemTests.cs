using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Achievement rework: 50+ achievements in categories, run constraints, set conditions, run tracker,
    /// meta sync and the slot schedule (1 early active slot + 1 passive from achievements, 3+3 from the Constellation).
    /// </summary>
    public class AchievementSystemTests
    {
        private SaveManager save;
        private AchievementManager manager;

        [SetUp]
        public void SetUp()
        {
            save = new SaveManager(new InMemorySaveStorage());
            manager = new AchievementManager(save);
        }

        [TearDown]
        public void TearDown()
        {
            save?.Dispose();
        }

        private static AchievementData Find(string id)
        {
            foreach (var a in Resources.LoadAll<AchievementData>("Achievements"))
            {
                if (a != null && a.id == id) return a;
            }
            Assert.Fail($"Achievement {id} not found");
            return null;
        }

        private bool Completed(string id) => save.Data.completedAchievements.Contains(id);

        private int Progress(string id)
        {
            save.Data.achievementProgress.TryGetValue(id, out int p);
            return p;
        }

        // ------------------------------------------------------------ catalog

        [Test]
        public void Catalog_Has50Plus_WithEveryCategory_LocalizationKeysAndIcons()
        {
            var all = manager.GetAllAchievements();
            Assert.GreaterOrEqual(all.Count, 50);

            var categories = new HashSet<AchievementCategory>();
            var ids = new HashSet<string>();
            foreach (var a in all)
            {
                Assert.IsTrue(ids.Add(a.id), $"Duplicate id {a.id}");
                Assert.Greater(a.targetValue, 0, a.id);
                Assert.AreEqual($"ach.{a.id}.title", a.nameKey, a.id);
                Assert.AreEqual($"ach.{a.id}.desc", a.descriptionKey, a.id);
                Assert.IsNotNull(a.icon, $"{a.id} icon");
                Assert.IsNotNull(a.runConstraint, $"{a.id} runConstraint");
                if (a.runConstraint.HasAny)
                {
                    Assert.AreEqual(AchievementConditionType.ReachWave, a.conditionType, $"{a.id}: constraints only apply to ReachWave");
                }
                categories.Add(a.category);
            }

            foreach (AchievementCategory c in Enum.GetValues(typeof(AchievementCategory)))
            {
                Assert.IsTrue(categories.Contains(c), $"No achievements in category {c}");
            }
        }

        [Test]
        public void Catalog_UnlocksEveryLockedAbilityAndHero_Once()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCatalog>("Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset");
            var heroes = Resources.Load<HeroCatalog>("HeroCatalog");
            var lockedAbilities = new HashSet<string>();
            foreach (var ab in catalog.abilities) if (ab != null && !ab.unlockedByDefault) lockedAbilities.Add(ab.id);
            var lockedHeroes = new HashSet<string>();
            foreach (var h in heroes.heroes) if (h != null && !h.unlockedByDefault) lockedHeroes.Add(h.id);

            var rewardedAbilities = new List<string>();
            var rewardedHeroes = new List<string>();
            AchievementData allAbilities = null;
            foreach (var a in manager.GetAllAchievements())
            {
                if (a.rewardType == AchievementRewardType.UnlockAbility) rewardedAbilities.Add(a.rewardId);
                if (a.rewardType == AchievementRewardType.UnlockHero) rewardedHeroes.Add(a.rewardId);
                if (a.conditionType == AchievementConditionType.AbilitiesUnlocked) allAbilities = a;
            }

            CollectionAssert.AreEquivalent(lockedAbilities, rewardedAbilities);
            CollectionAssert.AreEquivalent(lockedHeroes, rewardedHeroes);
            Assert.IsNotNull(allAbilities);
            Assert.AreEqual(lockedAbilities.Count, allAbilities.targetValue, "'All abilities' must track the locked ability count");
        }

        [Test]
        public void Slots_AchievementsGiveOneEarlyActiveAndOnePassive_ConstellationGivesThreePlusThree()
        {
            int active = 0, passive = 0;
            foreach (var a in manager.GetAllAchievements())
            {
                if (a.rewardType == AchievementRewardType.UnlockActiveSlot) { active += a.rewardValue; Assert.AreEqual("first_mini_boss", a.id); }
                if (a.rewardType == AchievementRewardType.UnlockPassiveSlot) { passive += a.rewardValue; Assert.AreEqual("defeat_golem", a.id); }
            }
            Assert.AreEqual(1, active);
            Assert.AreEqual(1, passive);

            var early = Find("first_mini_boss");
            Assert.AreEqual(AchievementConditionType.DefeatMiniBosses, early.conditionType);
            Assert.AreEqual(1, early.targetValue, "The first active slot must come in the very first runs");

            var tree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            float treeActive = 0f, treePassive = 0f;
            foreach (var node in tree.nodes)
            {
                treeActive += node.GetEffectValue(TalentEffectType.BonusActiveSlots);
                treePassive += node.GetEffectValue(TalentEffectType.BonusPassiveSlots);
            }
            Assert.AreEqual(3f, treeActive);
            Assert.AreEqual(3f, treePassive);
            Assert.AreEqual(AbilitySlotService.MAX_ACTIVE_SLOTS, AbilitySlotService.BASE_ACTIVE_SLOTS + active + (int)treeActive);
            Assert.AreEqual(AbilitySlotService.MAX_PASSIVE_SLOTS, AbilitySlotService.BASE_PASSIVE_SLOTS + passive + (int)treePassive);

            foreach (string id in new[] { "fury_arsenal", "bastion_discipline", "fortune_satchel" })
            {
                var node = tree.GetById(id);
                Assert.IsNotNull(node, id);
                Assert.GreaterOrEqual(node.shardCost, 8, $"{id}: slot stars must take hours of shards");
                Assert.AreEqual(0, node.coinCost, $"{id}: shards only");
            }
        }

        [Test]
        public void Slots_FirstMiniBossGrantsActive_GolemGrantsPassive()
        {
            using var slots = new AbilitySlotService(save, new MetaUpgradeManager(save));
            Assert.AreEqual(2, slots.MaxActiveSlots);
            manager.OnMiniBossDefeated();
            Assert.AreEqual(3, slots.MaxActiveSlots);
            Assert.AreEqual(2, slots.MaxPassiveSlots);
            manager.OnBossDefeated("golem");
            Assert.AreEqual(3, slots.MaxPassiveSlots);
        }

        // ------------------------------------------------------------ conditions

        [Test]
        public void ReachWave_Constraints_FilterByAbilitiesHeroRerollsAndTime()
        {
            // Without a run snapshot constrained achievements never progress.
            manager.OnWaveReached(30);
            Assert.AreEqual(0, Progress("minimalist_w15"));
            Assert.AreEqual(0, Progress("knight_w30"));
            Assert.IsTrue(Completed("reach_wave_20"));

            manager.OnWaveReached(15, new AchievementRunContext("mage", 2, 0, false, 600f));
            Assert.IsFalse(Completed("minimalist_w15"), "Two active abilities break Minimalist");
            manager.OnWaveReached(15, new AchievementRunContext("mage", 1, 3, false, 600f));
            Assert.IsTrue(Completed("minimalist_w15"));

            manager.OnWaveReached(25, new AchievementRunContext("knight", 3, 1, true, 900f));
            Assert.IsFalse(Completed("fate_w25"), "Rerolls break Trust in Fate");
            Assert.IsFalse(Completed("pure_power_w20"), "A passive breaks Pure Power");
            manager.OnWaveReached(25, new AchievementRunContext("knight", 3, 0, false, 900f));
            Assert.IsTrue(Completed("fate_w25"));
            Assert.IsTrue(Completed("pure_power_w20"));

            manager.OnWaveReached(10, new AchievementRunContext("knight", 3, 0, false, 241f));
            Assert.IsFalse(Completed("speedrun_w10"));
            manager.OnWaveReached(10, new AchievementRunContext("knight", 3, 0, false, 239f));
            Assert.IsTrue(Completed("speedrun_w10"));

            manager.OnWaveReached(30, new AchievementRunContext("ranger", 3, 3, false, 2000f));
            Assert.IsTrue(Completed("ranger_w30"));
            Assert.IsFalse(Completed("knight_w30"));
        }

        [Test]
        public void SetConditions_CountDistinctMembers_AndPersist()
        {
            var ctx = new AchievementRunContext("knight", 1, 1, false, 100f);
            manager.OnWaveReached(15, ctx);
            manager.OnWaveReached(20, ctx);
            Assert.AreEqual(1, Progress("all_heroes_w15"), "Same hero counts once");
            manager.OnWaveReached(14, new AchievementRunContext("mage", 1, 1, false, 100f));
            Assert.AreEqual(1, Progress("all_heroes_w15"), "Below the wave threshold");
            Assert.IsTrue(manager.HasSetMember("all_heroes_w15", "knight"));

            foreach (string hero in new[] { "mage", "ranger", "necromancer" })
            {
                manager.OnWaveReached(15, new AchievementRunContext(hero, 1, 1, false, 100f));
            }
            Assert.IsTrue(Completed("all_heroes_w15"));

            manager.OnWaveEventCompleted(WaveEventType.GoldGoblin);
            manager.OnWaveEventCompleted(WaveEventType.GoldGoblin);
            Assert.AreEqual(1, Progress("all_wave_events"));
            Assert.AreEqual(2, Progress("gold_goblin_5"), "Gold Goblin catches are counted, not deduplicated");
            manager.OnWaveEventCompleted(WaveEventType.SwarmRush);
            Assert.AreEqual(2, Progress("all_wave_events"));
            Assert.AreEqual(2, Progress("gold_goblin_5"), "Other events don't count as goblins");

            manager.OnSynergyActivated("blade_storm", 1);
            manager.OnSynergyActivated("blade_storm", 1);
            Assert.IsTrue(Completed("first_synergy"));
            Assert.AreEqual(1, Progress("unique_synergies_6"));

            // Set members survive a save round-trip.
            var storage = new InMemorySaveStorage();
            using var saveA = new SaveManager(storage);
            var mgrA = new AchievementManager(saveA);
            mgrA.OnMutatorSurvived(WaveMutatorType.Fog);
            saveA.Save();
            using var saveB = new SaveManager(storage);
            var mgrB = new AchievementManager(saveB);
            mgrB.OnMutatorSurvived(WaveMutatorType.Fog);
            saveB.Data.achievementProgress.TryGetValue("all_mutators", out int mutators);
            Assert.AreEqual(1, mutators);
        }

        [Test]
        public void BossConditions_EnrageAndLowHp_AndShardRewards()
        {
            int shardsBefore = save.Data.bossShards;
            manager.OnBigBossKilled(beforeEnrage: false, heroHpPercent: 50f);
            Assert.IsFalse(Completed("boss_before_enrage"));
            Assert.IsFalse(Completed("boss_low_hp"));

            manager.OnBigBossKilled(beforeEnrage: true, heroHpPercent: 18f);
            Assert.IsTrue(Completed("boss_before_enrage"));
            Assert.IsTrue(save.IsAbilityUnlocked("EarthquakeSlam"));
            Assert.IsTrue(Completed("boss_low_hp"));

            manager.OnBossDefeated("necromancer");
            Assert.AreEqual(shardsBefore + Find("defeat_necromancer").rewardValue, save.Data.bossShards);
        }

        [Test]
        public void CoinRewards_AreDoubled()
        {
            Assert.AreEqual(200, Find("reach_wave_5").rewardValue);
            Assert.AreEqual(6000, Find("defeat_demon_lord").rewardValue);
            Assert.AreEqual(4000, Find("all_abilities").rewardValue);
            foreach (var a in manager.GetAllAchievements())
            {
                if (a.rewardType == AchievementRewardType.GiveCoins)
                {
                    Assert.GreaterOrEqual(a.rewardValue, 200, $"{a.id}: coin rewards were doubled (minimum 200)");
                }
            }
        }

        // ------------------------------------------------------------ run tracker & meta sync

        [Test]
        public void RunTracker_ForwardsBusEvents_AndTracksRunState()
        {
            using var bus = new GameEventBus();
            var stats = new RunStatistics();
            var tracker = new AchievementRunTracker(bus, manager, stats, save);
            tracker.Start();
            try
            {
                for (int i = 0; i < 500; i++) bus.Publish(new EnemyDiedEvent(Vector3.zero, null, wasElite: i < 30));
                Assert.IsTrue(Completed("kills_in_run_500"));
                Assert.IsTrue(Completed("elite_kills_30"));

                bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false, wasMiniBoss: true));
                Assert.IsTrue(Completed("first_mini_boss"));

                bus.Publish(new BossSpawnedEvent("golem", "Golem", 100, 100, false));
                bus.Publish(new BossEnragedEvent(Vector3.zero, "golem", 1));
                bus.Publish(new EnemyDiedEvent(Vector3.zero, null, false, wasBoss: true, bossId: "golem"));
                Assert.IsFalse(Completed("boss_before_enrage"), "Enraged boss doesn't count");

                bus.Publish(new BarrelExplodedEvent(Vector3.zero, 3f, 12));
                Assert.IsTrue(Completed("barrel_strike"));

                bus.Publish(new SynergyActivatedEvent("blade_storm", "Blade Storm"));
                bus.Publish(new SynergyActivatedEvent("steam_blast", "Steam Blast"));
                bus.Publish(new SynergyActivatedEvent("steam_blast", "Steam Blast"));
                Assert.AreEqual(2, tracker.DistinctSynergiesThisRun);
                Assert.AreEqual(2, Progress("synergies_in_run_3"));

                bus.Publish(new UltimateActivatedEvent(HeroUltimateType.Shield));
                Assert.AreEqual(1, Progress("ultimates_50"));

                bus.Publish(new CardOfferShownEvent(2, null, null, null, 2, 2, isReroll: true));
                Assert.IsTrue(tracker.UsedReroll);
                save.Data.selectedHeroId = "knight";
                bus.Publish(new WaveCompletedEvent(24, 25));
                Assert.IsTrue(Completed("reach_wave_20"));
                Assert.IsFalse(Completed("fate_w25"), "A rerolled run cannot complete Trust in Fate");

                stats.UpdatePlayTime(31f * 60f);
                bus.Publish(new GameOverEvent(25, 10));
                Assert.IsTrue(Completed("marathon_30"));
            }
            finally
            {
                tracker.Dispose();
            }
        }

        [Test]
        public void MetaSync_CountsRunsTalentsKeystonesUpgradesDailyAndAbilities()
        {
            var tree = new TalentTreeManager(save);
            var meta = new MetaUpgradeManager(save);
            var sync = new MetaAchievementSync(manager, save, tree, meta, null);

            save.Data.totalRuns = 25;
            save.Data.unlockedTalentIds.Add("fury_r1a");
            save.Data.unlockedTalentIds.Add("fury_keystone");
            save.Data.metaUpgradeLevels["hero_hp"] = 20;
            save.Data.metaUpgradeLevels["hero_damage"] = 5;
            save.Data.dailyBonusCycle = 1;
            save.Data.dailyBonusDay = 1;
            for (int i = 0; i < 11; i++) save.Data.unlockedAbilityIds.Add("ability_" + i);

            int shardsBefore = save.Data.bossShards;
            sync.Sync();

            Assert.IsTrue(Completed("play_25_runs"));
            Assert.IsTrue(Completed("first_talent"));
            Assert.AreEqual(2, Progress("talents_15"));
            Assert.IsTrue(Completed("first_keystone"));
            Assert.AreEqual(shardsBefore + Find("first_keystone").rewardValue, save.Data.bossShards);
            Assert.IsTrue(Completed("meta_levels_25"));
            Assert.IsTrue(Completed("daily_7"), "One full 7-day cycle = 7 claims");
            Assert.IsTrue(Completed("all_abilities"));

            // Idempotent.
            int completed = save.Data.completedAchievements.Count;
            sync.Sync();
            Assert.AreEqual(completed, save.Data.completedAchievements.Count);
            tree.Dispose();
        }
    }
}
