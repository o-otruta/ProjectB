using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class WaveAndPickupLimitsTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }
            spawned.Clear();
            Physics.SyncTransforms();
        }

        private T Track<T>(T obj) where T : Object
        {
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        [Test]
        public void Configs_WaveConfigHas300MaxActiveEnemies_AndPickupConfigLoadsFromResources()
        {
            WaveConfig waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(waveConfig);
            Assert.AreEqual(300, waveConfig.maxActiveEnemies);

            PickupConfig pickupConfig = Resources.Load<PickupConfig>(PickupConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(pickupConfig, "Resources/PickupConfig.asset must exist");
            Assert.AreEqual(300, pickupConfig.maxActiveXpCrystals);
            Assert.AreEqual(300, pickupConfig.maxActiveCoins);
        }

        [Test]
        public void WaveManager_TwentyWavesWithoutKills_NeverExceedsMaxActiveEnemies_AndDrainsDebtOnKills()
        {
            WaveConfig assetConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(assetConfig);

            WaveConfig testConfig = Track(Object.Instantiate(assetConfig));
            testConfig.maxActiveEnemies = 300;
            testConfig.waveDelay = 0.01f;
            testConfig.spawnDelay = 0.01f;
            testConfig.maxWaveDuration = 0f; // Prevent StartCoroutine(WaveDurationCoroutine) during EditMode stepping

            EnemyData dummyEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
            dummyEnemy.name = "LimitTestEnemy";
            dummyEnemy.hp = 100;
            dummyEnemy.speed = 2f;
            dummyEnemy.contactDamage = 5;

            testConfig.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry
                {
                    enemy = dummyEnemy,
                    minWave = 1,
                    maxWave = 0,
                    spawnWeight = 1f,
                    packSize = 1,
                    triggerIntroWave = false
                }
            };

            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("LimitTestHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(new HeroRuntimeStats(), bus, null);

            var waveGo = Track(new GameObject("LimitTestWaveManager", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", testConfig);
            waveMgr.Construct(heroHealth, null, bus, null, null);
            waveMgr.EnsurePoolsInitialized();

            MethodInfo spawnWaveMethod = typeof(WaveManager).GetMethod("SpawnWaveCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(spawnWaveMethod);

            IEnumerator lastWaveEnumerator = null;
            for (int wave = 1; wave <= 20; wave++)
            {
                SetPrivateField(waveMgr, "currentWave", wave);
                lastWaveEnumerator = (IEnumerator)spawnWaveMethod.Invoke(waveMgr, null);

                for (int step = 0; step < 60; step++)
                {
                    bool hasMore = lastWaveEnumerator.MoveNext();
                    Assert.LessOrEqual(waveMgr.EnemiesAlive, testConfig.maxActiveEnemies,
                        $"Active enemies exceeded cap during wave {wave}");
                    if (!hasMore) break;
                }
            }

            // 20 waves without kills request ~596 enemies total, so the arena must sit cleanly at the 300 cap
            Assert.AreEqual(300, waveMgr.EnemiesAlive);
            Assert.Greater(waveMgr.PendingWaveEnemies, 0, "Wave 20 should retain pending debt while capped");

            // Event, elite, and summon paths must also refuse spawning past the global cap
            Assert.IsNull(waveMgr.SpawnEventEnemy(dummyEnemy, Vector3.right * 5f));
            Assert.IsNull(waveMgr.SpawnChallengeElite(Vector3.left * 5f));
            MethodInfo summonMethod = typeof(WaveManager).GetMethod("SpawnSummonedMinion", BindingFlags.Instance | BindingFlags.NonPublic);
            summonMethod.Invoke(waveMgr, new object[] { dummyEnemy, Vector3.forward * 5f });
            Assert.AreEqual(300, waveMgr.EnemiesAlive);

            // Despawning 10 enemies frees slots; stepping wave 20 drains pending wave debt back to 300
            var toDespawn = new List<EnemyBase>(10);
            foreach (EnemyBase active in waveMgr.ActiveEnemies)
            {
                toDespawn.Add(active);
                if (toDespawn.Count == 10) break;
            }
            for (int i = 0; i < toDespawn.Count; i++)
            {
                waveMgr.DespawnTrackedEnemy(toDespawn[i]);
            }
            Assert.AreEqual(290, waveMgr.EnemiesAlive);

            for (int step = 0; step < 15 && waveMgr.EnemiesAlive < 300; step++)
            {
                lastWaveEnumerator.MoveNext();
            }

            Assert.AreEqual(300, waveMgr.EnemiesAlive, "Pending wave debt should refill freed slots up to maxActiveEnemies");
        }

        [Test]
        public void WaveManager_GetValidPositionNear_FallbackPlacesPointOnMinRadRing()
        {
            int obsLayer = LayerMask.NameToLayer("Obstacles");
            Assert.GreaterOrEqual(obsLayer, 0, "Obstacles layer must exist");

            var blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            blocker.layer = obsLayer;
            blocker.transform.position = Vector3.zero;
            blocker.transform.localScale = new Vector3(80f, 10f, 80f);
            Physics.SyncTransforms();

            var waveGo = Track(new GameObject("FallbackWaveManager", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            waveMgr.EnsurePoolsInitialized();

            const float minRad = 12f;
            for (int i = 0; i < 50; i++)
            {
                Vector3 pos = waveMgr.GetValidPositionNear(Vector3.zero, minRad, 18f);
                Assert.AreEqual(0f, pos.y, 0.001f);
                float dist = new Vector2(pos.x, pos.z).magnitude;
                Assert.AreEqual(minRad, dist, 0.01f, "Fallback spawn must sit on the minRad ring, not inside the center circle");
            }
        }

        [Test]
        public void XpManager_ExceedingLimit_MergesIntoNearestCrystal_PreservesTotalXp_AndResetsScaleOnReuse()
        {
            var smallCfg = Track(ScriptableObject.CreateInstance<PickupConfig>());
            smallCfg.maxActiveXpCrystals = 10;
            smallCfg.maxMergedScaleMultiplier = 2.0f;
            smallCfg.mergeScaleStep = 0.2f;

            var heroGo = Track(new GameObject("XpLimitHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroExp = heroGo.AddComponent<HeroExperience>();

            var xpGo = Track(new GameObject("LimitXpManager", typeof(XpManager)));
            var xpMgr = xpGo.GetComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);
            xpMgr.SetConfig(smallCfg);
            xpMgr.EnsureInitialized();

            for (int i = 0; i < 9; i++)
            {
                xpMgr.SpawnXp(new Vector3(i * 5f, 0f, 0f), 10);
            }
            xpMgr.SpawnXp(new Vector3(100f, 0f, 0f), 10);
            Assert.AreEqual(10, xpMgr.ActiveCount);

            XpCrystal targetCrystal = null;
            foreach (XpCrystal c in xpMgr.ActiveCrystals)
            {
                if (Mathf.Approximately(c.transform.position.x, 100f))
                {
                    targetCrystal = c;
                    break;
                }
            }
            Assert.IsNotNull(targetCrystal);
            float baseScaleX = targetCrystal.transform.localScale.x;

            // Spawn 5 more near (100, 0, 0) -> all merge into targetCrystal
            for (int i = 0; i < 5; i++)
            {
                xpMgr.SpawnXp(new Vector3(100.5f, 0f, 0.2f * i), 4);
            }

            Assert.AreEqual(10, xpMgr.ActiveCount, "Active crystals must not exceed configured limit");
            Assert.AreEqual(5, targetCrystal.MergeCount);
            Assert.AreEqual(30, targetCrystal.XpAmount);
            Assert.Greater(targetCrystal.transform.localScale.x, baseScaleX, "Merged crystal must grow visually");

            int totalXp = 0;
            foreach (XpCrystal c in xpMgr.ActiveCrystals) totalXp += c.XpAmount;
            Assert.AreEqual(120, totalXp);

            // Returning merged crystal to pool and reusing it resets scale and merge count
            MethodInfo returnMethod = typeof(XpCrystal).GetMethod("ReturnToPool", BindingFlags.Instance | BindingFlags.NonPublic);
            returnMethod.Invoke(targetCrystal, null);
            Assert.AreEqual(9, xpMgr.ActiveCount);

            xpMgr.SpawnXp(new Vector3(50f, 0f, 0f), 7);
            Assert.AreEqual(10, xpMgr.ActiveCount);
            Assert.AreEqual(0, targetCrystal.MergeCount);
            Assert.AreEqual(baseScaleX, targetCrystal.transform.localScale.x, 0.001f);

            // Default Resources/PickupConfig test (350 drops -> 300 active, total XP preserved)
            var xpGo2 = Track(new GameObject("DefaultLimitXpManager", typeof(XpManager)));
            var xpMgr2 = xpGo2.GetComponent<XpManager>();
            xpMgr2.Construct(heroHealth, heroExp);
            xpMgr2.EnsureInitialized();

            for (int i = 0; i < 350; i++)
            {
                xpMgr2.SpawnXp(new Vector3((i % 20) * 1.5f, 0f, (i / 20) * 1.5f), 3);
            }

            Assert.AreEqual(300, xpMgr2.ActiveCount);
            int totalDefaultXp = 0;
            foreach (XpCrystal c in xpMgr2.ActiveCrystals) totalDefaultXp += c.XpAmount;
            Assert.AreEqual(1050, totalDefaultXp);
        }

        [Test]
        public void CoinManager_ExceedingLimit_MergesIntoNearestCoin_PreservesTotalCoins_AndResetsScaleOnReuse()
        {
            var smallCfg = Track(ScriptableObject.CreateInstance<PickupConfig>());
            smallCfg.maxActiveCoins = 10;
            smallCfg.maxMergedScaleMultiplier = 2.0f;
            smallCfg.mergeScaleStep = 0.2f;

            var heroGo = Track(new GameObject("CoinLimitHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();

            var coinGo = Track(new GameObject("LimitCoinManager", typeof(CoinManager)));
            var coinMgr = coinGo.GetComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);
            coinMgr.SetConfig(smallCfg);
            coinMgr.EnsureInitialized();

            for (int i = 0; i < 9; i++)
            {
                coinMgr.SpawnCoin(new Vector3(i * 5f, 0f, 0f), 5);
            }
            coinMgr.SpawnCoin(new Vector3(100f, 0f, 0f), 5);
            Assert.AreEqual(10, coinMgr.ActiveCount);

            CoinPickup targetCoin = null;
            foreach (CoinPickup c in coinMgr.ActiveCoins)
            {
                if (Mathf.Approximately(c.transform.position.x, 100f))
                {
                    targetCoin = c;
                    break;
                }
            }
            Assert.IsNotNull(targetCoin);
            float baseScaleX = targetCoin.transform.localScale.x;

            for (int i = 0; i < 5; i++)
            {
                coinMgr.SpawnCoin(new Vector3(100.4f, 0f, 0.2f * i), 2);
            }

            Assert.AreEqual(10, coinMgr.ActiveCount);
            Assert.AreEqual(5, targetCoin.MergeCount);
            Assert.AreEqual(15, targetCoin.CoinAmount);
            Assert.Greater(targetCoin.transform.localScale.x, baseScaleX);

            int totalCoins = 0;
            foreach (CoinPickup c in coinMgr.ActiveCoins) totalCoins += c.CoinAmount;
            Assert.AreEqual(60, totalCoins);

            MethodInfo returnMethod = typeof(CoinPickup).GetMethod("ReturnToPool", BindingFlags.Instance | BindingFlags.NonPublic);
            returnMethod.Invoke(targetCoin, null);
            Assert.AreEqual(9, coinMgr.ActiveCount);

            coinMgr.SpawnCoin(new Vector3(50f, 0f, 0f), 3);
            Assert.AreEqual(10, coinMgr.ActiveCount);
            Assert.AreEqual(0, targetCoin.MergeCount);
            Assert.AreEqual(baseScaleX, targetCoin.transform.localScale.x, 0.001f);

            var coinGo2 = Track(new GameObject("DefaultLimitCoinManager", typeof(CoinManager)));
            var coinMgr2 = coinGo2.GetComponent<CoinManager>();
            coinMgr2.Construct(heroHealth, heroEcon);
            coinMgr2.EnsureInitialized();

            for (int i = 0; i < 350; i++)
            {
                coinMgr2.SpawnCoin(new Vector3((i % 20) * 1.5f, 0f, (i / 20) * 1.5f), 2);
            }

            Assert.AreEqual(300, coinMgr2.ActiveCount);
            int totalDefaultCoins = 0;
            foreach (CoinPickup c in coinMgr2.ActiveCoins) totalDefaultCoins += c.CoinAmount;
            Assert.AreEqual(700, totalDefaultCoins);
        }
    }
}
