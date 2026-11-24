using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Player;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class Sprint4WavesEnemiesBossesTests
    {
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

        [Test]
        public void WaveConfig_PhasedSchedule_UnlocksEnemiesByWaveRespectsMaxWaveAndExcludesExplodersForMiniBoss()
        {
            var config = Track(ScriptableObject.CreateInstance<WaveConfig>());
            var slime = Track(ScriptableObject.CreateInstance<EnemyData>());
            slime.name = "Slime";
            var earlyGoblin = Track(ScriptableObject.CreateInstance<ShootingEnemyData>());
            earlyGoblin.name = "EarlyGoblin";
            var skeleton = Track(ScriptableObject.CreateInstance<EnemyData>());
            skeleton.name = "Skeleton";
            var bomb = Track(ScriptableObject.CreateInstance<ExploderEnemyData>());
            bomb.name = "BombBug";
            var orc = Track(ScriptableObject.CreateInstance<ChargerEnemyData>());
            orc.name = "OrcCharger";

            config.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = slime, minWave = 1, maxWave = 0, spawnWeight = 1f, packSize = 1 },
                new EnemyWaveEntry { enemy = earlyGoblin, minWave = 3, maxWave = 6, spawnWeight = 1f, packSize = 1 },
                new EnemyWaveEntry { enemy = skeleton, minWave = 5, maxWave = 0, spawnWeight = 1f, packSize = 4 },
                new EnemyWaveEntry { enemy = bomb, minWave = 10, maxWave = 0, spawnWeight = 50f, packSize = 1 },
                new EnemyWaveEntry { enemy = orc, minWave = 14, maxWave = 0, spawnWeight = 1f, packSize = 1 },
            };

            var eligible = new List<EnemyWaveEntry>();

            // Wave 1: Slime only
            Assert.AreEqual(1, config.GetEligibleEntriesForWave(1, eligible));
            Assert.AreSame(slime, eligible[0].enemy);
            Assert.AreSame(slime, config.SelectEnemyEntryForWave(1, 0.99f).enemy);

            // Wave 3: Slime + EarlyGoblin
            Assert.AreEqual(2, config.GetEligibleEntriesForWave(3, eligible));
            Assert.AreSame(earlyGoblin, config.SelectEnemyEntryForWave(3, 0.99f).enemy);

            // Wave 5: Slime + EarlyGoblin + Skeleton (packSize 4)
            Assert.AreEqual(3, config.GetEligibleEntriesForWave(5, eligible));
            var pickedWave5 = config.SelectEnemyEntryForWave(5, 0.95f);
            Assert.AreSame(skeleton, pickedWave5.enemy);
            Assert.AreEqual(4, pickedWave5.packSize);

            // Wave 7: EarlyGoblin expired (maxWave=6) -> Slime + Skeleton
            Assert.AreEqual(2, config.GetEligibleEntriesForWave(7, eligible));
            Assert.IsFalse(eligible.Exists(e => e.enemy == earlyGoblin));

            // Wave 10: BombBug eligible for regular wave, excluded for Mini-Boss when non-exploders exist
            Assert.AreEqual(3, config.GetEligibleEntriesForWave(10, eligible));
            Assert.AreSame(bomb, config.SelectEnemyEntryForWave(10, 0.5f).enemy);
            EnemyData miniBossEnemy = config.SelectMiniBossEnemyForWave(10, 0.95f);
            Assert.IsNotNull(miniBossEnemy);
            Assert.IsFalse(miniBossEnemy is ExploderEnemyData, "Mini-boss should avoid ExploderEnemyData when other enemies are eligible");

            // Wave 14: OrcCharger unlocked
            Assert.AreEqual(4, config.GetEligibleEntriesForWave(14, eligible));
            Assert.AreSame(orc, config.SelectEnemyEntryForWave(14, 0.999f).enemy);

            // Elite archetype distribution covers all 3 archetypes
            Assert.AreEqual(EliteArchetype.Berserker, WaveManager.PickEliteArchetype(0.05f));
            Assert.AreEqual(EliteArchetype.Juggernaut, WaveManager.PickEliteArchetype(0.5f));
            Assert.AreEqual(EliteArchetype.HasteAura, WaveManager.PickEliteArchetype(0.95f));
        }

        [Test]
        public void Sprint4Assets_PrefabsVoxelMaterialsAndBlenderSources_AreConfiguredAndWired()
        {
            // 1. Verify Blender source files exist
            string[] expectedBlends =
            {
                "ArtSources/Blender/Skeleton.blend",
                "ArtSources/Blender/BombSlime.blend",
                "ArtSources/Blender/OrcCharger.blend",
                "ArtSources/Blender/FrostLich.blend"
            };
            foreach (string blendPath in expectedBlends)
            {
                Assert.IsTrue(File.Exists(blendPath), $"Missing Blender source file: {blendPath}");
            }

            // 2. Verify 8 new voxel materials (ProjectB/VoxelLit + GPU Instancing + textures)
            Shader voxelLit = Shader.Find("ProjectB/VoxelLit");
            Assert.IsNotNull(voxelLit);
            string[] voxelPrefixes = { "Skeleton", "BombSlime", "OrcCharger", "FrostLich" };
            foreach (string prefix in voxelPrefixes)
            {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Mat.mat");
                Material glowMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Glow_Mat.mat");
                Assert.IsNotNull(baseMat, $"Missing {prefix}_Mat.mat");
                Assert.IsNotNull(glowMat, $"Missing {prefix}_Glow_Mat.mat");
                Assert.AreEqual(voxelLit, baseMat.shader);
                Assert.AreEqual(voxelLit, glowMat.shader);
                Assert.IsTrue(baseMat.enableInstancing);
                Assert.IsTrue(glowMat.enableInstancing);
                Assert.IsNotNull(baseMat.GetTexture("_BaseMap"));
                Assert.IsTrue(glowMat.IsKeywordEnabled("_EMISSION"));
                Assert.IsNotNull(glowMat.GetTexture("_EmissionMap"));
            }

            // 3. Verify 4 effect materials exist and have instancing enabled
            string[] fxMatPaths =
            {
                "Assets/_Materials/ChargeTelegraphMat.mat",
                "Assets/_Materials/FrostTelegraphMat.mat",
                "Assets/_Materials/FrostSpikeMat.mat",
                "Assets/_Materials/BlizzardRingMat.mat"
            };
            foreach (string path in fxMatPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"Missing effect material: {path}");
                Assert.IsTrue(mat.enableInstancing, $"{path} must enable GPU instancing");
            }

            // 4. Verify 4 enemy/boss prefabs
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            string[] prefabPaths =
            {
                "Assets/_Prefabs/Enemies/SkeletonRunner.prefab",
                "Assets/_Prefabs/Enemies/BombSlime.prefab",
                "Assets/_Prefabs/Enemies/OrcCharger.prefab",
                "Assets/_Prefabs/Enemies/FrostLichBoss.prefab"
            };
            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing prefab: {path}");
                Assert.AreEqual(enemyLayer, prefab.layer);
                Transform visuals = prefab.transform.Find("Visuals");
                Assert.IsNotNull(visuals, $"{path} missing Visuals");
                var mf = visuals.GetComponent<MeshFilter>();
                var mr = visuals.GetComponent<MeshRenderer>();
                var anim = visuals.GetComponent<VoxelMeshAnimator>();
                Assert.IsNotNull(mf?.sharedMesh, $"{path} missing mesh");
                Assert.AreEqual(mf.sharedMesh.subMeshCount, mr.sharedMaterials.Length, $"{path} material count mismatch");
                Assert.IsNotNull(anim);
                Assert.AreEqual(4, anim.Frames.Length, $"{path} must have 4 walk frames");
            }

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/BombSlime.prefab").GetComponent<ExploderEnemy>());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/OrcCharger.prefab").GetComponent<ChargerEnemy>());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/FrostLichBoss.prefab").GetComponent<FrostLichBoss>());

            // 5. Verify ScriptableObjects and WaveConfig wiring
            var skeletonData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/Enemies/SkeletonData.asset");
            var bombData = AssetDatabase.LoadAssetAtPath<ExploderEnemyData>("Assets/_ScriptableObjects/Enemies/BombSlimeData.asset");
            var orcData = AssetDatabase.LoadAssetAtPath<ChargerEnemyData>("Assets/_ScriptableObjects/Enemies/OrcChargerData.asset");
            var lichData = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/FrostLichBossData.asset");
            var slimeData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/Enemies/SlimeData.asset");
            var necroData = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/NecromancerBossData.asset");
            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");

            Assert.IsNotNull(skeletonData);
            Assert.IsNotNull(bombData);
            Assert.IsNotNull(orcData);
            Assert.IsNotNull(lichData);
            Assert.AreEqual(8, slimeData.contactDamage);
            Assert.AreSame(skeletonData, necroData.minionData);
            Assert.AreEqual("frost_lich", lichData.bossId);
            Assert.GreaterOrEqual(waveConfig.enemySchedule.Count, 6);
            Assert.GreaterOrEqual(waveConfig.bosses.Count, 3);
            Assert.AreSame(lichData, waveConfig.bosses[2]);

            // 6. Verify Gameplay scene WaveManager references WaveConfig.asset
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;
            try
            {
                WaveManager waveMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    waveMgr = root.GetComponentInChildren<WaveManager>(true);
                    if (waveMgr != null) break;
                }
                Assert.IsNotNull(waveMgr);
                Assert.AreSame(waveConfig, waveMgr.Config);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ExploderEnemy_ArmsDetonatesDamagesHeroAndAllies_CancelsOnEarlyKill_AndResetsOnReuse()
        {
            var bus = new GameEventBus();
            int explodeEvents = 0;
            bus.Subscribe<EnemyExplodedEvent>(_ => explodeEvents++);

            var heroGo = Track(new GameObject("TestHero"));
            heroGo.transform.position = new Vector3(1.2f, 0f, 0f);
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);

            var allyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            allyData.hp = 100;
            allyData.contactDamage = 5;
            var allyGo = Track(new GameObject("AllyEnemy"));
            allyGo.layer = 8;
            allyGo.transform.position = new Vector3(-1.5f, 0f, 0f);
            allyGo.AddComponent<BoxCollider>();
            var ally = allyGo.AddComponent<EnemyBase>();
            ally.Initialize(allyData, heroGo.transform, null, bus, 1f);

            var bombData = Track(ScriptableObject.CreateInstance<ExploderEnemyData>());
            bombData.hp = 40;
            bombData.speed = 3.8f;
            bombData.triggerDistance = 1.8f;
            bombData.fuseDuration = 0.75f;
            bombData.explosionRadius = 2.5f;
            bombData.explosionDamage = 28;
            bombData.friendlyFireMultiplier = 1.0f;

            var bombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/BombSlime.prefab");
            var bombGo = Track(Object.Instantiate(bombPrefab));
            bombGo.transform.position = Vector3.zero;
            var exploder = bombGo.GetComponent<ExploderEnemy>();
            Physics.SyncTransforms();

            // 1. Arming -> fuse completion -> detonation damages hero AND nearby ally
            exploder.Initialize(bombData, heroGo.transform, null, bus, 1f);
            exploder.BeginArming();
            Assert.IsTrue(exploder.IsArming);

            exploder.TickFuse(0.4f);
            Assert.IsTrue(exploder.IsArming);
            Assert.IsFalse(exploder.IsDead);

            exploder.TickFuse(0.4f);
            Assert.IsTrue(exploder.IsDead);
            Assert.AreEqual(1, explodeEvents);
            Assert.AreEqual(172, heroHealth.CurrentHp);
            Assert.AreEqual(72, ally.CurrentHp);

            // 2. Pool reuse resets arming state, scale, and early kill cancels detonation
            bombGo.SetActive(true);
            exploder.Initialize(bombData, heroGo.transform, null, bus, 1f);
            Assert.IsFalse(exploder.IsArming);
            Assert.AreEqual(1f, bombGo.transform.localScale.x, 0.001f);

            exploder.BeginArming();
            Assert.IsTrue(exploder.IsArming);
            exploder.TakeDamage(999);
            Assert.IsTrue(exploder.IsDead);
            Assert.AreEqual(1, explodeEvents, "Killing ExploderEnemy early must NOT trigger detonation");
            Assert.AreEqual(172, heroHealth.CurrentHp);
        }

        [Test]
        public void ChargerEnemy_WindsUpDashesStraightDamagesHeroOnceStopsAtObstacles_AndResetsOnReuse()
        {
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("ChargeHero"));
            heroGo.transform.position = new Vector3(0f, 0f, 5f);
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);

            var chargerData = Track(ScriptableObject.CreateInstance<ChargerEnemyData>());
            chargerData.hp = 85;
            chargerData.speed = 2.4f;
            chargerData.chargeTriggerRange = 7f;
            chargerData.chargeWindupDuration = 0.8f;
            chargerData.chargeSpeed = 10f;
            chargerData.chargeDistance = 10f;
            chargerData.chargeDamage = 18;
            chargerData.chargeHitRadius = 1.25f;
            chargerData.chargeCooldown = 4f;
            chargerData.recoveryDuration = 0.45f;

            var orcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/OrcCharger.prefab");
            var orcGo = Track(Object.Instantiate(orcPrefab));
            orcGo.transform.position = Vector3.zero;
            var charger = orcGo.GetComponent<ChargerEnemy>();

            charger.Initialize(chargerData, heroGo.transform, null, bus, 1f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Chasing, charger.CurrentState);

            // Begin windup aimed at +Z
            charger.BeginWindup();
            Assert.AreEqual(ChargerEnemy.ChargeState.WindingUp, charger.CurrentState);
            Assert.Greater(Vector3.Dot(charger.LockedChargeDirection, Vector3.forward), 0.99f);

            // Step past windup into dash -> hits hero once along +Z and finishes in Recovering
            charger.TickCharge(0.85f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Charging, charger.CurrentState);

            for (int i = 0; i < 12; i++)
            {
                charger.TickCharge(0.1f);
            }
            Assert.AreEqual(182, heroHealth.CurrentHp, "Charge must hit hero exactly once");
            Assert.Greater(orcGo.transform.position.z, 8f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Recovering, charger.CurrentState);

            charger.TickCharge(0.5f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Chasing, charger.CurrentState);

            // Test obstacle stop: place wall at z = 3 ahead of charger at (0,0,0)
            var wallGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wallGo.layer = LayerMask.NameToLayer("Obstacles");
            wallGo.transform.position = new Vector3(0f, 0.5f, 3.5f);
            wallGo.transform.localScale = new Vector3(4f, 2f, 1f);
            Physics.SyncTransforms();

            orcGo.transform.position = Vector3.zero;
            heroGo.transform.position = new Vector3(0f, 0f, 6f);
            charger.Initialize(chargerData, heroGo.transform, null, bus, 1f);
            charger.BeginWindup();
            charger.TickCharge(0.85f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Charging, charger.CurrentState);
            for (int i = 0; i < 10; i++)
            {
                charger.TickCharge(0.1f);
                if (charger.CurrentState == ChargerEnemy.ChargeState.Recovering)
                {
                    break;
                }
            }
            Assert.Less(orcGo.transform.position.z, 3.3f, "Charger must stop before wall obstacle");
            Assert.AreEqual(ChargerEnemy.ChargeState.Recovering, charger.CurrentState);

            charger.TickCharge(0.5f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Chasing, charger.CurrentState);

            // Pool reuse resets state cleanly
            charger.Initialize(chargerData, heroGo.transform, null, bus, 1f);
            Assert.AreEqual(ChargerEnemy.ChargeState.Chasing, charger.CurrentState);
        }

        [Test]
        public void EnemyBase_ThreeEliteArchetypes_ApplyDistinctStatsPullResistanceHasteAuraAndResetOnReuse()
        {
            var bus = new GameEventBus();
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 3.0f;
            enemyData.contactDamage = 10;

            var enemyGo = Track(new GameObject("EliteTarget"));
            enemyGo.layer = 8;
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();

            // 1. Berserker: x3 HP, x2 dmg, +25% speed, full pull
            enemy.Initialize(enemyData, null, null, bus, 1f);
            enemy.MakeElite(EliteArchetype.Berserker, config);
            Assert.AreEqual(EliteArchetype.Berserker, enemy.EliteType);
            Assert.AreEqual(300, enemy.MaxHp);
            Assert.AreEqual(3.75f, enemy.EffectiveMoveSpeed, 0.01f);
            Assert.AreEqual(1f, enemy.PullResistance, 0.001f);

            // 2. Juggernaut: x4.5 HP, 1.35x scale, 0.2 pull resistance
            enemyGo.transform.position = Vector3.zero;
            enemy.Initialize(enemyData, null, null, bus, 1f);
            enemy.MakeElite(EliteArchetype.Juggernaut, config);
            Assert.AreEqual(EliteArchetype.Juggernaut, enemy.EliteType);
            Assert.AreEqual(450, enemy.MaxHp);
            Assert.AreEqual(1.35f, enemyGo.transform.localScale.x, 0.01f);
            Assert.AreEqual(0.2f, enemy.PullResistance, 0.001f);
            enemy.ApplyPull(new Vector3(10f, 0f, 0f));
            Assert.AreEqual(2.0f, enemyGo.transform.position.x, 0.01f);

            // 3. HasteAura: buffs nearby normal enemy, ignores other elite
            enemyGo.transform.position = Vector3.zero;
            enemy.Initialize(enemyData, null, null, bus, 1f);
            enemy.MakeElite(EliteArchetype.HasteAura, config);
            Assert.AreEqual(EliteArchetype.HasteAura, enemy.EliteType);
            Assert.AreEqual(300, enemy.MaxHp);

            var normalAllyGo = Track(new GameObject("NormalAlly"));
            normalAllyGo.layer = 8;
            normalAllyGo.transform.position = new Vector3(2.0f, 0f, 0f);
            normalAllyGo.AddComponent<BoxCollider>();
            var normalAlly = normalAllyGo.AddComponent<EnemyBase>();
            normalAlly.Initialize(enemyData, null, null, bus, 1f);

            var otherEliteGo = Track(new GameObject("OtherElite"));
            otherEliteGo.layer = 8;
            otherEliteGo.transform.position = new Vector3(-2.0f, 0f, 0f);
            otherEliteGo.AddComponent<BoxCollider>();
            var otherElite = otherEliteGo.AddComponent<EnemyBase>();
            otherElite.Initialize(enemyData, null, null, bus, 1f);
            otherElite.MakeElite(EliteArchetype.Berserker, config);

            Physics.SyncTransforms();

            int buffed = enemy.PulseHasteAura();
            Assert.AreEqual(1, buffed, "HasteAura should buff normal ally and exclude other elites");
            Assert.IsTrue(normalAlly.HasExternalSpeedBuff);
            Assert.AreEqual(3.6f, normalAlly.EffectiveMoveSpeed, 0.01f);
            Assert.IsFalse(otherElite.HasExternalSpeedBuff);

            // 4. Pool reuse clears elite state cleanly
            enemy.Initialize(enemyData, null, null, bus, 1f);
            Assert.IsFalse(enemy.IsElite);
            Assert.AreEqual(EliteArchetype.None, enemy.EliteType);
            Assert.AreEqual(1f, enemy.PullResistance, 0.001f);
            Assert.AreEqual(1f, enemyGo.transform.localScale.x, 0.001f);
            Assert.AreEqual(3.0f, enemy.EffectiveMoveSpeed, 0.01f);
        }

        [Test]
        public void FrostLichBoss_Wave75Selection_CrossIceSpikesAndBlizzardRing_DamageAndSlowHeroCorrectly()
        {
            // Wave 75 -> index 2 (Frost Lich) in 3-boss rotation (25=Golem, 50=Necro, 75=Frost Lich, 100=Golem)
            Assert.AreEqual(0, WaveManager.GetBossIndexForWave(25, 25, 3));
            Assert.AreEqual(1, WaveManager.GetBossIndexForWave(50, 25, 3));
            Assert.AreEqual(2, WaveManager.GetBossIndexForWave(75, 25, 3));
            Assert.AreEqual(0, WaveManager.GetBossIndexForWave(100, 25, 3));

            var bus = new GameEventBus();
            int novaEvents = 0;
            bus.Subscribe<FrostLichNovaEvent>(_ => novaEvents++);

            var heroGo = Track(new GameObject("FrostTargetHero"));
            heroGo.transform.position = new Vector3(5f, 0f, 0f);
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 300);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 300);
            var heroMove = heroGo.AddComponent<HeroMovement>();

            var lichData = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/FrostLichBossData.asset");
            var lichPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/FrostLichBoss.prefab");
            var lichGo = Track(Object.Instantiate(lichPrefab));
            lichGo.transform.position = Vector3.zero;
            var lich = lichGo.GetComponent<FrostLichBoss>();

            lich.InitializeBoss(lichData, heroGo.transform, null, bus, null, null, null, 1f);

            // 1. Cross Ice Spikes: first ray aims directly at hero (+X at 5m) -> hits + slows hero
            lich.BeginIceSpikesTelegraph();
            Assert.AreEqual(FrostLichBoss.FrostLichState.TelegraphingSpikes, lich.CurrentState);
            Assert.AreEqual(6, lich.ActiveSpikeRayCount);

            bool spikeHit = lich.ExecuteIceSpikes();
            Assert.IsTrue(spikeHit);
            Assert.AreEqual(300 - lichData.spikeDamage, heroHealth.CurrentHp);
            Assert.IsTrue(heroMove.IsSlowed);
            Assert.AreEqual(lichData.spikeSlowFactor, heroMove.CurrentSlowFactor, 0.01f);
            Assert.AreEqual(1, novaEvents);

            // 2. Blizzard Ring: safe inside inner eye (< 3m), safe outside (> 9m), damaged inside [3..9m]
            heroHealth.TickInvulnerability(0.25f); // Expire post-hit i-frames from Ice Spikes before testing Blizzard tick
            heroMove.ClearSlow();
            int hpBeforeBlizzard = heroHealth.CurrentHp;

            // Inside safe eye (2.0m) -> 0 damage
            heroGo.transform.position = new Vector3(2.0f, 0f, 0f);
            Assert.IsFalse(lich.TickBlizzardDamage());
            Assert.AreEqual(hpBeforeBlizzard, heroHealth.CurrentHp);
            Assert.IsFalse(heroMove.IsSlowed);

            // Outside blizzard (> 9.0m) -> 0 damage
            heroGo.transform.position = new Vector3(11.0f, 0f, 0f);
            Assert.IsFalse(lich.TickBlizzardDamage());
            Assert.AreEqual(hpBeforeBlizzard, heroHealth.CurrentHp);

            // Inside active blizzard ring (5.5m) -> tick damage + slow
            heroGo.transform.position = new Vector3(5.5f, 0f, 0f);
            Assert.IsTrue(lich.TickBlizzardDamage());
            Assert.AreEqual(hpBeforeBlizzard - lichData.blizzardTickDamage, heroHealth.CurrentHp);
            Assert.IsTrue(heroMove.IsSlowed);
            Assert.AreEqual(lichData.blizzardSlowFactor, heroMove.CurrentSlowFactor, 0.01f);
        }
    }
}
