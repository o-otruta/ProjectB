using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class VoxelIntegrationTests
    {
        private static readonly string[] ExpectedMaterialAssets =
        {
            "Knight_Mat", "Mage_Mat", "Mage_Glow_Mat", "Goblin_Mat",
            "Golem_Mat", "Golem_Glow_Mat", "Necromancer_Mat", "Necromancer_Glow_Mat",
            "OakTree_Mat", "PineTree_Mat", "TreeStump_Mat",
            "BoulderLarge_Mat", "BoulderSmall_Mat", "ForestBush_Mat", "FallenLog_Mat",
            "SecretChest_Mat", "SecretChest_Glow_Mat",
            "SecretScroll_Mat", "SecretScroll_Glow_Mat",
            "SecretCrystal_Mat", "SecretCrystal_Glow_Mat",
            "XpCrystal_Glow_Mat", "CoinPickup_Mat", "CoinPickup_Glow_Mat",
            "OrbitalSword_Mat", "OrbitalSword_Glow_Mat",
            "BoomerangProjectile_Mat", "BoomerangProjectile_Glow_Mat",
            "FireballProjectile_Mat", "FireballProjectile_Glow_Mat",
            "ShooterProjectile_Mat", "ShooterProjectile_Glow_Mat",
            "EnemyProjectile_Mat", "EnemyProjectile_Glow_Mat",
            "Projectile_Mat", "Projectile_Glow_Mat"
        };

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

        [Test]
        public void VoxelMaterials_All36ExistWithInstancingAndUrpLitProperties()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader voxelLit = Shader.Find("ProjectB/VoxelLit");
            Assert.IsNotNull(urpLit);
            Assert.IsNotNull(voxelLit);

            var characterMaterials = new HashSet<string>
            {
                "Knight_Mat", "Mage_Mat", "Mage_Glow_Mat", "Goblin_Mat",
                "Golem_Mat", "Golem_Glow_Mat", "Necromancer_Mat", "Necromancer_Glow_Mat",
                // Sprint 11: pickups use VoxelLit for the _PICKUP_MOTION bob & spin
                "XpCrystal_Glow_Mat", "CoinPickup_Mat", "CoinPickup_Glow_Mat"
            };

            foreach (string matName in ExpectedMaterialAssets)
            {
                string path = $"Assets/_Materials/Voxels/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"Missing material: {path}");
                Shader expectedShader = characterMaterials.Contains(matName) ? voxelLit : urpLit;
                Assert.AreEqual(expectedShader, mat.shader, $"{matName} must use {expectedShader.name}");
                Assert.IsTrue(mat.enableInstancing, $"{matName} must have enableInstancing = true");
                Assert.IsTrue(mat.HasProperty("_BaseColor"), $"{matName} must have _BaseColor");
                Assert.AreEqual(Color.white, mat.GetColor("_BaseColor"), $"{matName} _BaseColor must be white");
                Assert.IsNotNull(mat.GetTexture("_BaseMap"), $"{matName} must have _BaseMap");

                if (matName.EndsWith("_Glow_Mat"))
                {
                    Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"), $"{matName} must enable _EMISSION");
                    Assert.IsNotNull(mat.GetTexture("_EmissionMap"), $"{matName} must have _EmissionMap");
                }
            }
        }

        [Test]
        public void HeroAndMagePrefabs_AreConfiguredWithKnightAndMageModels()
        {
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Assert.IsNotNull(heroPrefab);

            Transform visuals = heroPrefab.transform.Find("Visuals");
            Assert.IsNotNull(visuals);
            Assert.IsNull(visuals.Find("hero"), "Legacy HeroTest child must be removed");

            Transform knightModel = visuals.Find("KnightModel");
            Assert.IsNotNull(knightModel);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), knightModel.localPosition);
            Assert.AreEqual(Vector3.one, knightModel.localScale);

            var mf = knightModel.GetComponent<MeshFilter>();
            var mr = knightModel.GetComponent<MeshRenderer>();
            var anim = knightModel.GetComponent<VoxelMeshAnimator>();
            Assert.IsNotNull(mf);
            Assert.IsNotNull(mr);
            Assert.IsNotNull(anim);
            Assert.AreEqual(1, mf.sharedMesh.subMeshCount);
            Assert.AreEqual(1, mr.sharedMaterials.Length);
            Assert.IsFalse(anim.PlayOnEnable);
            Assert.IsNotNull(anim.IdleMesh);
            Assert.AreEqual(4, anim.Frames.Length);

            var movement = heroPrefab.GetComponent<HeroMovement>();
            Assert.IsNotNull(movement);
            var soMov = new SerializedObject(movement);
            Assert.AreSame(anim, soMov.FindProperty("meshAnimator").objectReferenceValue);

            GameObject magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Heroes/MageVisual.prefab");
            Assert.IsNotNull(magePrefab);
            Transform mageModel = magePrefab.transform.Find("MageModel");
            Assert.IsNotNull(mageModel);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), mageModel.localPosition);
            var mageMr = mageModel.GetComponent<MeshRenderer>();
            var mageAnim = mageModel.GetComponent<VoxelMeshAnimator>();
            Assert.AreEqual(2, mageMr.sharedMaterials.Length);
            Assert.AreEqual(4, mageAnim.Frames.Length);
        }

        [Test]
        public void EnemiesAndBosses_UseVoxelPrefabsAndSupportEnemyTint()
        {
            string[] enemyPaths =
            {
                "Assets/_Prefabs/Enemies/GoblinShooter.prefab",
                "Assets/_Prefabs/Enemies/GolemBoss.prefab",
                "Assets/_Prefabs/Enemies/NecromancerBoss.prefab"
            };

            foreach (string path in enemyPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing {path}");
                Transform visuals = prefab.transform.Find("Visuals");
                Assert.IsNotNull(visuals, $"{path} missing Visuals");
                var mr = visuals.GetComponent<MeshRenderer>();
                var mf = visuals.GetComponent<MeshFilter>();
                Assert.IsNotNull(mr, $"{path} Visuals missing MeshRenderer");
                Assert.IsNotNull(mf, $"{path} Visuals missing MeshFilter");
                Assert.IsNotNull(mf.sharedMesh, $"{path} Visuals missing mesh");
                Assert.AreEqual(mf.sharedMesh.subMeshCount, mr.sharedMaterials.Length, $"{path} material count mismatch");

                // Runtime tint verification via EnemyBase.Initialize + TakeDamage hit flash (+ MakeElite for non-bosses)
                GameObject instance = Object.Instantiate(prefab);
                spawned.Add(instance);
                var enemy = instance.GetComponent<EnemyBase>();
                var data = ScriptableObject.CreateInstance<EnemyData>();
                spawned.Add(data);
                data.hp = 50;
                data.contactDamage = 5;
                enemy.Initialize(data, null, null, new GameEventBus(), 1f);

                var instMr = instance.transform.Find("Visuals").GetComponent<MeshRenderer>();
                Assert.IsFalse(instMr.HasPropertyBlock(), $"{path} should clear tint on Initialize");

                enemy.TakeDamage(1);
                Assert.IsTrue(instMr.HasPropertyBlock(), $"{path} should apply hit-flash MaterialPropertyBlock on TakeDamage");

                if (!enemy.IsBoss)
                {
                    enemy.MakeElite();
                    Assert.IsTrue(enemy.IsElite, $"{path} failed MakeElite");
                    var pb = new MaterialPropertyBlock();
                    instMr.GetPropertyBlock(pb);
                    Color tinted = pb.GetColor("_BaseColor");
                    Assert.Greater(tinted.b, 0.5f, $"{path} elite tint _BaseColor missing");
                }
            }

            // Check Golem SlamTelegraph preserved & Necromancer animator wired
            GameObject golem = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/GolemBoss.prefab");
            Assert.IsNotNull(golem.transform.Find("SlamTelegraph"));
            var golemSo = new SerializedObject(golem.GetComponent<GolemBoss>());
            Assert.IsNotNull(golemSo.FindProperty("idleMesh").objectReferenceValue);
            Assert.IsNotNull(golemSo.FindProperty("slamWindupMesh").objectReferenceValue);
            Assert.IsNotNull(golemSo.FindProperty("slamImpactMesh").objectReferenceValue);

            GameObject necro = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/NecromancerBoss.prefab");
            var necroAnim = necro.transform.Find("Visuals").GetComponent<VoxelMeshAnimator>();
            Assert.IsNotNull(necroAnim);
            Assert.AreEqual(4, necroAnim.Frames.Length);
            var necroSo = new SerializedObject(necro.GetComponent<NecromancerBoss>());
            Assert.IsNotNull(necroSo.FindProperty("castMesh").objectReferenceValue);
        }

        [Test]
        public void ForestObstaclesAndArenaConfig_HaveAll7PrefabsWithValidColliders()
        {
            string[] names = { "OakTree", "PineTree", "TreeStump", "BoulderLarge", "BoulderSmall", "ForestBush", "FallenLog" };
            ArenaConfig config = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.ObstaclePrefabs);
            Assert.AreEqual(7, config.ObstaclePrefabs.Length);

            int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
            for (int i = 0; i < names.Length; i++)
            {
                string path = $"Assets/_Prefabs/Obstacles/{names[i]}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing {path}");
                Assert.AreEqual(obstaclesLayer, prefab.layer);
                Assert.AreSame(prefab, config.ObstaclePrefabs[i], $"ArenaConfig slot {i} mismatch");

                Transform visuals = prefab.transform.Find("Visuals");
                Assert.IsNotNull(visuals);
                Assert.AreEqual(Vector3.one, visuals.localScale);
                Assert.IsNotNull(visuals.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(1, visuals.GetComponent<MeshRenderer>().sharedMaterials.Length);

                if (names[i] == "OakTree" || names[i] == "PineTree")
                {
                    var cap = prefab.GetComponent<CapsuleCollider>();
                    Assert.IsNotNull(cap);
                    Assert.LessOrEqual(cap.radius, 0.6f);
                }
            }
        }

        [Test]
        public void SecretsPickupsAndProjectiles_AreConfiguredAndLinkedInGameplayScene()
        {
            string[] secretPaths =
            {
                "Assets/_Prefabs/Secrets/SecretChest.prefab",
                "Assets/_Prefabs/Secrets/SecretScroll.prefab",
                "Assets/_Prefabs/Secrets/SecretCrystal.prefab"
            };
            foreach (string path in secretPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab);
                Assert.IsNotNull(prefab.transform.Find("BeaconBeam"));
                Transform mesh = prefab.transform.Find("Visuals/Mesh");
                Assert.IsNotNull(mesh);
                Assert.AreEqual(Vector3.one, mesh.localScale);
                Assert.AreEqual(Quaternion.identity, mesh.localRotation);
                var mf = mesh.GetComponent<MeshFilter>();
                var mr = mesh.GetComponent<MeshRenderer>();
                Assert.AreEqual(2, mf.sharedMesh.subMeshCount);
                Assert.AreEqual(2, mr.sharedMaterials.Length);
            }

            string[] projectilePaths =
            {
                "Assets/_Prefabs/Projectiles/OrbitalSword.prefab",
                "Assets/_Prefabs/Projectiles/BoomerangProjectile.prefab",
                "Assets/_Prefabs/Projectiles/FireballProjectile.prefab",
                "Assets/_Prefabs/Projectiles/ShooterProjectile.prefab",
                "Assets/_Prefabs/Projectiles/EnemyProjectile.prefab",
                "Assets/_Prefabs/Projectile.prefab"
            };
            foreach (string path in projectilePaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing {path}");
                Assert.AreEqual(Vector3.one, prefab.transform.localScale, $"{path} root scale must be 1");
                Assert.IsNull(prefab.GetComponent<MeshRenderer>(), $"{path} root must not have MeshRenderer");
                Transform visuals = prefab.transform.Find("Visuals");
                Assert.IsNotNull(visuals, $"{path} missing Visuals");
                var mf = visuals.GetComponent<MeshFilter>();
                var mr = visuals.GetComponent<MeshRenderer>();
                Assert.AreEqual(2, mf.sharedMesh.subMeshCount);
                Assert.AreEqual(2, mr.sharedMaterials.Length);
            }

            GameObject xpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Pickups/XpCrystal.prefab");
            GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Pickups/CoinPickup.prefab");
            Assert.IsNotNull(xpPrefab);
            Assert.IsNotNull(coinPrefab);

            // Verify Projectile.Initialize orients toward target
            var projGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Projectile.prefab"));
            spawned.Add(projGo);
            var targetGo = new GameObject("Target");
            spawned.Add(targetGo);
            targetGo.transform.position = new Vector3(10f, 0f, 0f);
            projGo.transform.position = Vector3.zero;
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            spawned.Add(weapon);
            projGo.GetComponent<Projectile>().Initialize(weapon, targetGo.transform, null, 1f);
            Assert.Greater(Vector3.Dot(projGo.transform.forward, Vector3.right), 0.99f);

            // Verify Gameplay.unity links XpCrystal and CoinPickup
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene gameplayScene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;
            try
            {
                XpManager xpMgr = null;
                CoinManager coinMgr = null;
                foreach (GameObject root in gameplayScene.GetRootGameObjects())
                {
                    if (xpMgr == null) xpMgr = root.GetComponentInChildren<XpManager>(true);
                    if (coinMgr == null) coinMgr = root.GetComponentInChildren<CoinManager>(true);
                }
                Assert.IsNotNull(xpMgr);
                Assert.IsNotNull(coinMgr);
                Assert.AreSame(xpPrefab, new SerializedObject(xpMgr).FindProperty("xpCrystalPrefab").objectReferenceValue);
                Assert.AreSame(coinPrefab, new SerializedObject(coinMgr).FindProperty("coinPrefab").objectReferenceValue);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(gameplayScene, true);
                }
            }
        }

        [Test]
        public void ArenaGenerator_InGameplayScene_HasInstancedFloorMaterial()
        {
            Material expectedFloorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/FloorMat.mat");
            Assert.IsNotNull(expectedFloorMat, "Assets/_Materials/FloorMat.mat must exist");
            Assert.IsTrue(expectedFloorMat.enableInstancing, "FloorMat must have enableInstancing = true");

            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene gameplayScene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;
            try
            {
                ArenaGenerator arenaGen = null;
                foreach (GameObject root in gameplayScene.GetRootGameObjects())
                {
                    arenaGen = root.GetComponentInChildren<ArenaGenerator>(true);
                    if (arenaGen != null) break;
                }

                Assert.IsNotNull(arenaGen, "Gameplay.unity must contain ArenaGenerator");
                var assignedMat = new SerializedObject(arenaGen).FindProperty("_floorMaterial").objectReferenceValue as Material;
                Assert.AreSame(expectedFloorMat, assignedMat, "ArenaGenerator._floorMaterial must reference FloorMat.mat");
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(gameplayScene, true);
                }
            }
        }

        [Test]
        public void MenuHeroRotator_ConfigureAsPreviewDummy_DisablesPhysicsAndGameplayBehaviours()
        {
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Assert.IsNotNull(heroPrefab);

            GameObject instance = Object.Instantiate(heroPrefab);
            spawned.Add(instance);

            var rotator = instance.AddComponent<MenuHeroRotator>();
            rotator.ConfigureAsPreviewDummy();

            var rb = instance.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb);
            Assert.IsTrue(rb.isKinematic, "Preview hero Rigidbody must be kinematic");
            Assert.AreEqual(RigidbodyInterpolation.None, rb.interpolation, "Preview hero Rigidbody must disable interpolation");
            Assert.IsFalse(rb.detectCollisions, "Preview hero Rigidbody must disable collision detection");

            var col = instance.GetComponent<Collider>();
            Assert.IsNotNull(col);
            Assert.IsFalse(col.enabled, "Preview hero Collider must be disabled");

            var rootBehaviours = instance.GetComponents<MonoBehaviour>();
            for (int i = 0; i < rootBehaviours.Length; i++)
            {
                if (rootBehaviours[i] == rotator)
                {
                    Assert.IsTrue(rotator.enabled);
                }
                else
                {
                    Assert.IsFalse(rootBehaviours[i].enabled, $"{rootBehaviours[i].GetType().Name} must be disabled on menu preview");
                }
            }
        }

        [Test]
        public void EnemyDeathHandler_NormalEnemyLootDrop_SpawnsXpAndCoinWithoutOverlapping()
        {
            var bus = new GameEventBus();
            var heroGo = new GameObject("DummyHero");
            spawned.Add(heroGo);
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroExp = heroGo.AddComponent<HeroExperience>();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();

            var xpGo = new GameObject("TestXpManager");
            spawned.Add(xpGo);
            var xpMgr = xpGo.AddComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);

            var coinGo = new GameObject("TestCoinManager");
            spawned.Add(coinGo);
            var coinMgr = coinGo.AddComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);

            XpCrystal spawnedCrystal = null;
            CoinPickup spawnedCoin = null;
            IObjectPool<XpCrystal> xpPool = null;
            xpPool = new ObjectPool<XpCrystal>(() =>
            {
                var go = new GameObject("TestXpCrystal");
                spawned.Add(go);
                spawnedCrystal = go.AddComponent<XpCrystal>();
                return spawnedCrystal;
            });
            IObjectPool<CoinPickup> coinPool = null;
            coinPool = new ObjectPool<CoinPickup>(() =>
            {
                var go = new GameObject("TestCoinPickup");
                spawned.Add(go);
                spawnedCoin = go.AddComponent<CoinPickup>();
                return spawnedCoin;
            });

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(XpManager).GetField("xpPool", flags).SetValue(xpMgr, xpPool);
            typeof(CoinManager).GetField("coinPool", flags).SetValue(coinMgr, coinPool);

            var enemyData = ScriptableObject.CreateInstance<EnemyData>();
            spawned.Add(enemyData);
            enemyData.xpDrop = 1;
            enemyData.coinDrop = 1;
            enemyData.coinDropChance = 1f;

            var handler = new EnemyDeathHandler(xpMgr, coinMgr, null, null, bus);
            handler.Start();
            try
            {
                Vector3 deathPos = new Vector3(4f, 0f, -2f);
                bus.Publish(new EnemyDiedEvent(deathPos, enemyData, wasElite: false, wasMiniBoss: false, wasBoss: false));

                Assert.IsNotNull(spawnedCrystal, "Expected XpCrystal to be spawned");
                Assert.IsNotNull(spawnedCoin, "Expected CoinPickup to be spawned");

                Vector2 crystalXZ = new Vector2(spawnedCrystal.transform.position.x, spawnedCrystal.transform.position.z);
                Vector2 coinXZ = new Vector2(spawnedCoin.transform.position.x, spawnedCoin.transform.position.z);
                float separation = Vector2.Distance(crystalXZ, coinXZ);
                Assert.GreaterOrEqual(separation, 0.7f, $"XP and coin drops overlapped (distance={separation})");
            }
            finally
            {
                handler.Dispose();
            }
        }
    }
}
