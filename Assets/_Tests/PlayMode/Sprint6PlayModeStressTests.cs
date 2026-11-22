using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectB.Abilities;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.Visuals;

namespace ProjectB.Tests.PlayMode
{
    public class Sprint6PlayModeStressTests
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
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    UnityEngine.Object.Destroy(spawned[i]);
                }
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
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        [UnityTest]
        public IEnumerator PlayMode_Wave40SwarmWith100PlusEnemiesAnd6ActiveSkills_RunsSmoothlyAndMagnetizesPickups()
        {
            using var saveManager = new SaveManager();
            saveManager.AddBonusActiveSlots(3, saveImmediately: false);
            saveManager.AddBonusPassiveSlots(3, saveImmediately: false);
            using var slotService = new AbilitySlotService(saveManager, null);
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var stats = new HeroRuntimeStats();
            stats.AddCooldownReductionPercent(35f);
            stats.AddCritChancePercent(50f);
            stats.AddCritMultiplier(0.4f);

            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(20f, 1f, 20f);

            var heroGo = Track(new GameObject("PlayModeHero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(stats, bus, null);
            heroHealth.SetGodMode(true);

            var heroExp = heroGo.AddComponent<HeroExperience>();
            heroExp.Construct(stats, bus);

            var heroEcon = heroGo.AddComponent<HeroEconomy>();
            heroEcon.Construct(runStats, bus, stats);

            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, heroHealth, slotService);

            // Create 6 active abilities for live PlayMode frames
            var orbital = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            orbital.id = "orbital_small_sword";
            orbital.type = AbilityType.Active;
            orbital.count = 4;
            orbital.baseDamage = 18f;
            orbital.orbitSpeed = 180f;
            orbital.orbitDistance = 2.5f;
            orbital.baseArea = 1f;
            orbital.targetLayer = 1 << 8;

            var chain = Track(ScriptableObject.CreateInstance<ChainLightningAbilityData>());
            chain.id = "ChainLightning";
            chain.type = AbilityType.Active;
            chain.baseDamage = 20f;
            chain.baseCooldown = 0.6f;
            chain.bounceCount = 4;
            chain.searchRadius = 10f;
            chain.bounceRadius = 5f;
            chain.targetLayer = 1 << 8;

            var poison = Track(ScriptableObject.CreateInstance<PoisonFlaskAbilityData>());
            poison.id = "PoisonAlchemist";
            poison.type = AbilityType.Active;
            poison.baseDamage = 10f;
            poison.baseCooldown = 0.8f;
            poison.puddleRadius = 3f;
            poison.puddleDuration = 3f;
            poison.tickInterval = 0.35f;
            poison.flaskCount = 2;
            poison.throwRange = 10f;
            poison.targetLayer = 1 << 8;

            var shield = Track(ScriptableObject.CreateInstance<GuardianShieldAbilityData>());
            shield.id = "GuardianShield";
            shield.type = AbilityType.Active;
            shield.baseDamage = 15f;
            shield.shieldCount = 3;
            shield.orbitSpeed = 160f;
            shield.orbitRadius = 1.8f;
            shield.knockbackDistance = 1.2f;
            shield.targetLayer = 1 << 8;

            var dragon = Track(ScriptableObject.CreateInstance<MiniDragonAbilityData>());
            dragon.id = "MiniDragon";
            dragon.type = AbilityType.Active;
            dragon.baseDamage = 14f;
            dragon.baseCooldown = 0.7f;
            dragon.dragonCount = 2;
            dragon.coneProjectileCount = 3;
            dragon.searchRadius = 10f;
            dragon.targetLayer = 1 << 8;

            var hammer = Track(ScriptableObject.CreateInstance<SeismicHammerAbilityData>());
            hammer.id = "EarthquakeSlam";
            hammer.type = AbilityType.Active;
            hammer.baseDamage = 45f;
            hammer.baseCooldown = 1.0f;
            hammer.slamRadius = 5.5f;
            hammer.slowFactor = 0.25f;
            hammer.slowDuration = 1.2f;
            hammer.targetLayer = 1 << 8;

            Assert.IsTrue(heroAbilities.AddAbility(orbital));
            Assert.IsTrue(heroAbilities.AddAbility(chain));
            Assert.IsTrue(heroAbilities.AddAbility(poison));
            Assert.IsTrue(heroAbilities.AddAbility(shield));
            Assert.IsTrue(heroAbilities.AddAbility(dragon));
            Assert.IsTrue(heroAbilities.AddAbility(hammer));
            Assert.AreEqual(6, heroAbilities.ActiveAbilities.Count);

            var xpGo = Track(new GameObject("PlayModeXpMgr", typeof(XpManager)));
            var xpMgr = xpGo.GetComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);
            xpMgr.PrewarmPool(120);

            var coinGo = Track(new GameObject("PlayModeCoinMgr", typeof(CoinManager)));
            var coinMgr = coinGo.GetComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);
            coinMgr.PrewarmPool(120);

            var enemyTemplate = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            enemyTemplate.name = "PlayModeEnemyTemplate";
            enemyTemplate.layer = 8;
            enemyTemplate.AddComponent<EnemyBase>();
            enemyTemplate.SetActive(false);

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.name = "PlayModeSwarmEnemy";
            enemyData.hp = 400;
            enemyData.speed = 3.2f;
            enemyData.contactDamage = 8;
            enemyData.xpDrop = 2;
            enemyData.coinDrop = 1;
            enemyData.coinDropChance = 1f;
            enemyData.modelPrefab = enemyTemplate;

            var waveConfig = Track(ScriptableObject.CreateInstance<WaveConfig>());
            waveConfig.waveDelay = 999f; // Prevent automatic background wave coroutine from interfering with manual 105-enemy stress test
            waveConfig.difficultyPerWave = 0.1f;
            waveConfig.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = enemyData, minWave = 1, maxWave = 0, spawnWeight = 1f, packSize = 1 }
            };

            var waveGo = Track(new GameObject("PlayModeWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            typeof(WaveManager).GetField("waveConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(waveMgr, waveConfig);
            waveMgr.Construct(heroHealth, null, bus, null, null);
            waveMgr.PrewarmPools(110);

            for (int i = 0; i < 105; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 105f;
                float radius = 2.5f + (i % 10) * 0.8f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                waveMgr.SpawnEnemyForWave(enemyData, pos, waveNumber: 40, makeElite: (i % 7) == 0);
            }

            Assert.AreEqual(105, waveMgr.EnemiesAlive);

            // Let 10 live PlayMode frames run and measure elapsed frame timing
            yield return null;
            var sw = Stopwatch.StartNew();
            for (int f = 0; f < 10; f++)
            {
                yield return null;
            }
            sw.Stop();

            Assert.Less(sw.Elapsed.TotalMilliseconds / 10.0, 33.0);

            // Spawn and magnetize 60 XP crystals + 40 coins in PlayMode
            for (int i = 0; i < 60; i++)
            {
                xpMgr.SpawnXp(new Vector3(8f + (i % 10), 0f, -6f + (i / 10)), 3);
            }
            for (int i = 0; i < 40; i++)
            {
                coinMgr.SpawnCoin(new Vector3(-8f - (i % 10), 0f, 6f - (i / 10)), 2);
            }

            Assert.AreEqual(60, xpMgr.MagnetizeAll(35f));
            Assert.AreEqual(40, coinMgr.MagnetizeAll(35f));
        }
    }
}
