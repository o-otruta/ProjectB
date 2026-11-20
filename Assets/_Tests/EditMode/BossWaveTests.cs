using NUnit.Framework;
using UnityEngine;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;

namespace ProjectB.Tests.EditMode
{
    public class BossWaveTests
    {
        [Test]
        public void WaveClassification_IdentifiesMiniBossAndBossWavesCorrectly()
        {
            // Normal waves
            Assert.IsFalse(WaveManager.IsBossWave(1, 25));
            Assert.IsFalse(WaveManager.IsMiniBossWave(1, 5, 25));
            Assert.IsFalse(WaveManager.IsMiniBossWave(4, 5, 25));

            // Mini-boss waves (5, 10, 15, 20, 30)
            Assert.IsTrue(WaveManager.IsMiniBossWave(5, 5, 25));
            Assert.IsTrue(WaveManager.IsMiniBossWave(10, 5, 25));
            Assert.IsTrue(WaveManager.IsMiniBossWave(20, 5, 25));
            Assert.IsTrue(WaveManager.IsMiniBossWave(30, 5, 25));

            // Boss waves (25, 50, 75, 100) are NOT mini-boss waves
            Assert.IsTrue(WaveManager.IsBossWave(25, 25));
            Assert.IsFalse(WaveManager.IsMiniBossWave(25, 5, 25));
            Assert.IsTrue(WaveManager.IsBossWave(50, 25));
            Assert.IsFalse(WaveManager.IsMiniBossWave(50, 5, 25));

            // Alternating Golem (0) and Necromancer (1)
            Assert.AreEqual(0, WaveManager.GetBossIndexForWave(25, 25, 2));
            Assert.AreEqual(1, WaveManager.GetBossIndexForWave(50, 25, 2));
            Assert.AreEqual(0, WaveManager.GetBossIndexForWave(75, 25, 2));
            Assert.AreEqual(1, WaveManager.GetBossIndexForWave(100, 25, 2));
        }

        [Test]
        public void EnemyBase_MakeMiniBoss_ScalesStatsPublishesBossEventsAndResetsOnReuse()
        {
            var bus = new GameEventBus();
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.name = "SlimeData";
            data.hp = 20;
            data.contactDamage = 10;

            var go = new GameObject("TestEnemy");
            go.transform.localScale = Vector3.one;
            var enemy = go.AddComponent<EnemyBase>();

            int spawnEvents = 0;
            int hpEvents = 0;
            int defeatEvents = 0;
            int lastHp = -1;

            bus.Subscribe<BossSpawnedEvent>(e => { spawnEvents++; lastHp = e.CurrentHp; });
            bus.Subscribe<BossHealthChangedEvent>(e => { hpEvents++; lastHp = e.CurrentHp; });
            bus.Subscribe<BossDefeatedEvent>(_ => defeatEvents++);

            try
            {
                enemy.Initialize(data, null, null, bus, difficultyMultiplier: 1f);
                enemy.MakeMiniBoss(hpMultiplier: 5f, damageMultiplier: 2f, scaleMultiplier: 1.85f);

                Assert.IsTrue(enemy.IsMiniBoss);
                Assert.AreEqual(100, enemy.MaxHp);
                Assert.AreEqual(100, enemy.CurrentHp);
                Assert.AreEqual(1.85f, go.transform.localScale.x, 0.001f);
                Assert.AreEqual(1, spawnEvents);

                enemy.TakeDamage(40);
                Assert.AreEqual(60, enemy.CurrentHp);
                Assert.AreEqual(60, lastHp);
                Assert.AreEqual(1, hpEvents);

                enemy.TakeDamage(60);
                Assert.IsTrue(enemy.IsDead);
                Assert.AreEqual(1, defeatEvents);

                // Reuse from pool resets scale and mini-boss flags
                go.SetActive(true);
                enemy.Initialize(data, null, null, bus, difficultyMultiplier: 1f);
                Assert.IsFalse(enemy.IsMiniBoss);
                Assert.AreEqual(20, enemy.MaxHp);
                Assert.AreEqual(1f, go.transform.localScale.x, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(data);
            }
        }
    }
}
