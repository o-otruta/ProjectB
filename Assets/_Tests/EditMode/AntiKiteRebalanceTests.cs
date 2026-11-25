using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Анти-кайт ребаланс: спавн по ходу движения, упреждающие стрелки, прыжок и ярость боссов, слоты 2+2.
    /// </summary>
    public class AntiKiteRebalanceTests
    {
        [Test]
        public void TimedEnrage_StartsAfterThreshold_AndRampsEveryInterval()
        {
            Assert.AreEqual(0, BossBase.ComputeTimedEnrageLevel(10f, 75f, 30f));
            Assert.AreEqual(0, BossBase.ComputeTimedEnrageLevel(74.9f, 75f, 30f));
            Assert.AreEqual(1, BossBase.ComputeTimedEnrageLevel(75f, 75f, 30f));
            Assert.AreEqual(1, BossBase.ComputeTimedEnrageLevel(104f, 75f, 30f));
            Assert.AreEqual(2, BossBase.ComputeTimedEnrageLevel(105f, 75f, 30f));
            Assert.AreEqual(0, BossBase.ComputeTimedEnrageLevel(500f, 0f, 30f), "enrageAfterSeconds = 0 disables enrage");
        }

        [Test]
        public void Bosses_HaveTimedEnrage_AndGolemCanLeapAndLeaveRubble()
        {
            string[] guids = AssetDatabase.FindAssets("t:BossData", new[] { "Assets/_ScriptableObjects/Bosses" });
            Assert.Greater(guids.Length, 0);
            foreach (string guid in guids)
            {
                var boss = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
                if (boss == null || boss.isMiniBoss) continue;
                Assert.Greater(boss.enrageAfterSeconds, 0f, $"{boss.name}: boss fights must end via timed enrage");
                Assert.GreaterOrEqual(boss.enrageMaxSpeedMultiplier, boss.enrageSpeedMultiplier);
            }

            var golem = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/GolemBossData.asset");
            Assert.IsNotNull(golem);
            Assert.Greater(golem.leapCooldown, 0f, "Golem must be able to leap toward a kiting hero");
            Assert.Greater(golem.leapMaxDistance, golem.leapMinDistance);
            Assert.Greater(golem.rubbleZoneRadius, 0f);
            Assert.Less(golem.rubbleSlowFactor, 1f);
        }

        [Test]
        public void EnemyShooters_ShootFartherFasterWithLead()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/Weapons/EnemyShooterWeaponData.asset");
            Assert.IsNotNull(weapon);
            Assert.GreaterOrEqual(weapon.range, 11f);
            Assert.GreaterOrEqual(weapon.projectileSpeed, 9f);
            Assert.That(weapon.leadFactor, Is.InRange(0.4f, 0.8f), "Partial lead: punishes circling, a sharp turn still dodges");
            Assert.GreaterOrEqual(weapon.attackCooldown, 3f, "Rarer shots offset the longer range and lead (bot batch: shooters killed 12/36 at 2s)");

            var goblin = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>("Assets/_ScriptableObjects/Enemies/GoblinData.asset");
            Assert.Less(goblin.stopDistance, weapon.range, "Shooter must stop inside its own range");
        }

        [Test]
        public void WaveConfig_AntiKiteDefaults_AreEnabled()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(config);
            Assert.That(config.forwardSpawnChance, Is.InRange(0.2f, 0.8f));
            Assert.IsTrue(config.relocateTrailingEnemies);
            Assert.Less(config.trailingBehindDot, 0.5f);
        }

        [Test]
        public void AbilitySlots_StartAtTwoPlusTwo_OneEarlyAchievementEach_RestAreDeep()
        {
            Assert.AreEqual(2, AbilitySlotService.BASE_ACTIVE_SLOTS);
            Assert.AreEqual(2, AbilitySlotService.BASE_PASSIVE_SLOTS);
            Assert.AreEqual(6, AbilitySlotService.MAX_ACTIVE_SLOTS);
            Assert.AreEqual(6, AbilitySlotService.MAX_PASSIVE_SLOTS);

            var all = Resources.LoadAll<AchievementData>("Achievements");
            AchievementData Find(string id)
            {
                foreach (var a in all) if (a != null && a.id == id) return a;
                Assert.Fail($"Achievement {id} not found");
                return null;
            }

            var a1 = Find("unlock_active_slot_1");
            Assert.AreEqual(AchievementConditionType.ReachWave, a1.conditionType);
            Assert.AreEqual(5, a1.targetValue, "One early active slot so the player learns the mechanic");

            var a2 = Find("unlock_active_slot_2");
            Assert.AreEqual(AchievementConditionType.DefeatBoss, a2.conditionType);
            Assert.AreEqual("golem", a2.targetId);

            var a3 = Find("unlock_active_slot_3");
            Assert.AreEqual(AchievementConditionType.ReachWave, a3.conditionType);
            Assert.AreEqual(50, a3.targetValue);

            var p2 = Find("unlock_passive_slot_2");
            Assert.AreEqual(AchievementConditionType.ReachWave, p2.conditionType);
            Assert.AreEqual(25, p2.targetValue);
        }
    }
}
