using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Readability & interaction of explosive barrels: kick + fuse, early detonation on enemy contact,
    /// chain depth for feedback, BOOM labels, BarrelConfig data and the first-run tutorial barrel.
    /// </summary>
    public class ExplosiveBarrelInteractionTests
    {
        private const string BarrelPrefabPath = "Assets/_Prefabs/Arena/ExplosiveBarrel.prefab";
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private HeroHealth CreateHero(Vector3 position)
        {
            var heroGo = Track(new GameObject("BarrelTestHero"));
            heroGo.transform.position = position;
            var hero = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hero, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hero, 200);
            return hero;
        }

        private ExplosiveBarrel CreateBarrel(Vector3 position, HeroHealth hero, GameEventBus bus, BarrelConfig config)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
            Assert.IsNotNull(prefab);
            var go = Track(Object.Instantiate(prefab));
            go.transform.position = position;
            var barrel = go.GetComponent<ExplosiveBarrel>();
            barrel.Initialize(hero, bus, null, config);
            Physics.SyncTransforms();
            return barrel;
        }

        private EnemyBase CreateEnemy(Vector3 position, Transform hero, GameEventBus bus, int hp = 500)
        {
            var data = Track(ScriptableObject.CreateInstance<EnemyData>());
            data.hp = hp;
            data.contactDamage = 5;
            var go = Track(new GameObject("BarrelTestEnemy"));
            go.layer = 8;
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Initialize(data, hero, null, bus, 1f);
            Physics.SyncTransforms();
            return enemy;
        }

        private BarrelConfig CreateConfig()
        {
            var cfg = Track(ScriptableObject.CreateInstance<BarrelConfig>());
            cfg.fuseDuration = 0.7f;
            cfg.kickSpeed = 7f;
            cfg.kickDeceleration = 10f;
            cfg.detonateOnEnemyContact = true;
            return cfg;
        }

        [Test]
        public void BarrelConfig_ResourceAsset_ExistsWithSaneDefaults()
        {
            var cfg = Resources.Load<BarrelConfig>(BarrelConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(cfg, "Resources/BarrelConfig.asset must exist (registered in GameLifetimeScope)");
            Assert.Greater(cfg.fuseDuration, 0.2f);
            Assert.Less(cfg.fuseDuration, 2f, "Fuse must stay short so the kick feels responsive");
            Assert.Greater(cfg.kickSpeed, 0f);
            Assert.GreaterOrEqual(cfg.boomLabelMinEnemies, 1);
            Assert.Greater(cfg.GetShakeIntensity(3), cfg.GetShakeIntensity(0), "Chain explosions must shake harder");
            Assert.LessOrEqual(cfg.GetShakeIntensity(100), cfg.maxShakeIntensity + 0.0001f);
        }

        [Test]
        public void BarrelPrefab_HasFuseSparkEmitter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
            var barrel = prefab.GetComponent<ExplosiveBarrel>();
            var so = new SerializedObject(barrel);
            var spark = so.FindProperty("fuseSpark").objectReferenceValue as ParticleSystem;
            Assert.IsNotNull(spark, "Barrel prefab must reference a FuseSpark particle system");
            Assert.IsTrue(spark.transform.IsChildOf(prefab.transform.Find("Visuals")), "FuseSpark must live under the Visuals child");
        }

        [Test]
        public void HeroTouch_KicksBarrelAway_LightsFuse_ThenExplodesAfterFuse()
        {
            var bus = new GameEventBus();
            int ignited = 0, exploded = 0;
            bus.Subscribe<BarrelIgnitedEvent>(_ => ignited++);
            bus.Subscribe<BarrelExplodedEvent>(_ => exploded++);

            var cfg = CreateConfig();
            var hero = CreateHero(new Vector3(-1f, 0f, 0f));
            var barrel = CreateBarrel(Vector3.zero, hero, bus, cfg);

            barrel.Tick(0.016f);

            Assert.IsTrue(barrel.IsLit, "Hero touch must light the fuse instead of detonating instantly");
            Assert.IsFalse(barrel.IsDead);
            Assert.AreEqual(1, ignited);
            Assert.AreEqual(0, exploded);
            Assert.Greater(barrel.KickVelocity.x, 0f, "Barrel must be kicked away from the hero (+X)");

            float startX = barrel.transform.position.x;
            for (int i = 0; i < 20; i++) barrel.Tick(0.016f);
            Assert.Greater(barrel.transform.position.x, startX + 0.5f, "Kicked barrel must slide away");
            Assert.IsFalse(barrel.IsDead, "Fuse must still be burning after 0.34s");

            for (int i = 0; i < 40 && !barrel.IsDead; i++) barrel.Tick(0.016f);
            Assert.IsTrue(barrel.IsDead, "Barrel must explode once the fuse burns out");
            Assert.AreEqual(1, exploded);
            Assert.AreEqual(200, hero.CurrentHp, "Barrels never hurt the hero");
        }

        [Test]
        public void KickedBarrel_DetonatesEarly_OnEnemyContact()
        {
            var bus = new GameEventBus();
            var cfg = CreateConfig();
            var hero = CreateHero(new Vector3(-1f, 0f, 0f));
            var barrel = CreateBarrel(Vector3.zero, hero, bus, cfg);
            var enemy = CreateEnemy(new Vector3(1.6f, 0f, 0f), hero.transform, bus);

            Assert.IsTrue(barrel.Kick(Vector3.right));
            float elapsed = 0f;
            while (!barrel.IsDead && elapsed < cfg.fuseDuration)
            {
                barrel.Tick(0.016f);
                Physics.SyncTransforms();
                elapsed += 0.016f;
            }

            Assert.IsTrue(barrel.IsDead);
            Assert.Less(elapsed, cfg.fuseDuration - 0.1f, "Rolling into an enemy must detonate before the fuse ends");
            Assert.Less(enemy.CurrentHp, 500, "Enemy must take explosion damage");
        }

        [Test]
        public void Damage_DetonatesLitBarrelInstantly_AndChainReportsDepth()
        {
            var bus = new GameEventBus();
            var depths = new List<int>();
            var hits = new List<int>();
            bus.Subscribe<BarrelExplodedEvent>(e => { depths.Add(e.ChainDepth); hits.Add(e.EnemiesHit); });

            var cfg = CreateConfig();
            var hero = CreateHero(new Vector3(-20f, 0f, 0f));
            var b1 = CreateBarrel(Vector3.zero, hero, bus, cfg);
            var b2 = CreateBarrel(new Vector3(2.5f, 0f, 0f), hero, bus, cfg);
            var b3 = CreateBarrel(new Vector3(5.0f, 0f, 0f), hero, bus, cfg);
            CreateEnemy(new Vector3(1.2f, 0f, 0f), hero.transform, bus);

            b1.Kick(Vector3.forward);
            Assert.IsTrue(b1.IsLit);
            b1.TakeDamage(1);

            Assert.IsTrue(b1.IsDead && b2.IsDead && b3.IsDead, "Chain reaction must reach all barrels");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, depths, "Chain depth must grow along the chain, root first");
            Assert.AreEqual(1, hits[0], "Root barrel hits the enemy (adjacent barrels are not counted)");
        }

        [Test]
        public void ReinitializedBarrel_ResetsFuseAndKickState()
        {
            var bus = new GameEventBus();
            var cfg = CreateConfig();
            var hero = CreateHero(new Vector3(-30f, 0f, 0f));
            var barrel = CreateBarrel(Vector3.zero, hero, bus, cfg);

            barrel.Kick(Vector3.right);
            barrel.Detonate();
            Assert.IsTrue(barrel.IsDead);

            barrel.Initialize(hero, bus, null, cfg);
            Assert.IsFalse(barrel.IsDead);
            Assert.IsFalse(barrel.IsLit);
            Assert.AreEqual(Vector3.zero, barrel.KickVelocity);
            Assert.IsTrue(barrel.gameObject.activeSelf);
        }

        [Test]
        public void BoomLabel_IsCachedPerCount()
        {
            string a = VfxFeedbackManager.GetBoomLabel(7);
            string b = VfxFeedbackManager.GetBoomLabel(7);
            Assert.AreEqual("BOOM! x7", a);
            Assert.AreSame(a, b, "BOOM labels must be cached (Zero-GC)");
            Assert.IsNotNull(VfxFeedbackManager.GetBoomLabel(999), "Out-of-range counts must clamp instead of throwing");
        }

        [Test]
        public void FtueTutorialBarrel_SpawnsTowardsEnemies_AndHintCompletesOnKick()
        {
            var bus = new GameEventBus();
            var hero = CreateHero(Vector3.zero);
            var heroMovement = hero.gameObject.AddComponent<HeroMovement>();

            ArenaConfig realConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(realConfig);
            var arenaCfg = Track(ScriptableObject.CreateInstance<ArenaConfig>());
            arenaCfg.ArenaSize = 100f;
            arenaCfg.ExplosiveBarrelPrefab = realConfig.ExplosiveBarrelPrefab;

            var genGo = Track(new GameObject("FtueArenaGenerator"));
            var gen = genGo.AddComponent<ArenaGenerator>();
            var genSo = new SerializedObject(gen);
            genSo.FindProperty("_config").objectReferenceValue = arenaCfg;
            genSo.FindProperty("_generateOnStart").boolValue = false;
            genSo.ApplyModifiedPropertiesWithoutUndo();
            var barrelCfg = CreateConfig();
            barrelCfg.ftueBarrelDistance = 3.5f;
            gen.Construct(null, bus, hero, null, null, null, null, barrelCfg);

            var ftueGo = Track(new GameObject("FtueUI"));
            var ftue = ftueGo.AddComponent<GameplayFtueUI>();
            ftue.Construct(null, null, null, null, null, heroMovement, null, null, bus, null, null, gen, barrelCfg);
            typeof(GameplayFtueUI).GetField("isFtueActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ftue, true);
            typeof(GameplayFtueUI).GetMethod("CacheDelegates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ftue, null);
            typeof(GameplayFtueUI).GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ftue, null);

            var barrel = ftue.SpawnTutorialBarrel(new Vector3(0f, 0f, 5f));
            Assert.IsNotNull(barrel);
            Track(barrel.gameObject);
            Assert.IsTrue(ftue.IsBarrelHintVisible);
            Assert.AreEqual(3.5f, barrel.transform.position.z, 0.05f, "Tutorial barrel goes on the path towards enemies");
            Assert.AreEqual(0f, barrel.transform.position.x, 0.05f);

            barrel.Kick(Vector3.forward);
            Assert.IsTrue(ftue.HasCompletedBarrelHint, "Kicking a barrel completes the hint");
            Assert.IsFalse(ftue.IsBarrelHintVisible);
            Assert.IsNull(ftue.SpawnTutorialBarrel(Vector3.forward), "Tutorial barrel is spawned only once");
        }
    }
}
