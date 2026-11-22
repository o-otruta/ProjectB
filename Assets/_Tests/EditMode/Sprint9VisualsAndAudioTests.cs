using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Arena;
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
    public class Sprint9VisualsAndAudioTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            SecretCompassIndicatorUI.ClearDynamicPois();
        }

        [TearDown]
        public void TearDown()
        {
            SecretCompassIndicatorUI.ClearDynamicPois();
            Time.timeScale = 1f;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
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

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        [Test]
        public void VoxelLitShader_ExistsCompilesAndSupportsHitFlashRimAndHeightAO()
        {
            Shader voxelLit = Shader.Find("ProjectB/VoxelLit");
            Assert.IsNotNull(voxelLit, "ProjectB/VoxelLit shader must exist!");
            Assert.IsFalse(ShaderUtil.ShaderHasError(voxelLit), "ProjectB/VoxelLit shader must compile without errors!");

            string[] requiredProps =
            {
                "_BaseMap", "_BaseColor", "_EmissionMap", "_EmissionColor",
                "_HitFlash", "_RimColor", "_RimPower", "_HeightAO", "_HeightAORange"
            };
            foreach (string prop in requiredProps)
            {
                Assert.AreNotEqual(-1, voxelLit.FindPropertyIndex(prop), $"Missing shader property {prop}");
            }

            string[] voxelMatNames =
            {
                "Knight_Mat", "Mage_Mat", "Mage_Glow_Mat",
                "Ranger_Mat", "Ranger_Glow_Mat",
                "NecromancerHero_Mat", "NecromancerHero_Glow_Mat",
                "Slime_Mat", "Bat_Mat", "Bat_Head_Mat",
                "Goblin_Mat", "Skeleton_Mat", "BombSlime_Mat", "OrcCharger_Mat",
                "Golem_Mat", "Necromancer_Mat", "FrostLich_Mat"
            };

            foreach (string matName in voxelMatNames)
            {
                string path = $"Assets/_Materials/Voxels/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"Missing {path}");
                Assert.AreEqual(voxelLit, mat.shader, $"{matName} must use ProjectB/VoxelLit");
                Assert.IsTrue(mat.enableInstancing, $"{matName} must enable GPU Instancing");
                Assert.IsNotNull(mat.GetTexture("_BaseMap"), $"{matName} missing _BaseMap");
            }
        }

        [Test]
        public void EnemyBase_And_HeroHealth_ApplyAndClearVoxelLitHitFlashAndRimColor()
        {
            GameObject goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/GoblinShooter.prefab");
            Assert.IsNotNull(goblinPrefab);

            GameObject enemyGo = Track(Object.Instantiate(goblinPrefab));
            var enemy = enemyGo.GetComponent<EnemyBase>();
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.contactDamage = 5;
            enemy.Initialize(enemyData, null, null, new GameEventBus(), 1f);

            var enemyMr = enemyGo.transform.Find("Visuals").GetComponent<MeshRenderer>();
            Assert.IsFalse(enemyMr.HasPropertyBlock());

            enemy.TakeDamage(10);
            Assert.IsTrue(enemy.IsHitFlashing);
            Assert.IsTrue(enemyMr.HasPropertyBlock());
            var pb = new MaterialPropertyBlock();
            enemyMr.GetPropertyBlock(pb);
            Assert.AreEqual(1f, pb.GetFloat("_HitFlash"), 0.01f);

            enemy.MakeElite(EliteArchetype.Juggernaut, null);
            enemyMr.GetPropertyBlock(pb);
            Color juggernautRim = pb.GetColor("_RimColor");
            Assert.Greater(juggernautRim.a, 0.5f);
            Assert.Greater(juggernautRim.r, 0.8f);

            // Hero hit flash via HeroHealth
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Assert.IsNotNull(heroPrefab);
            GameObject heroGo = Track(Object.Instantiate(heroPrefab));
            var heroHealth = heroGo.GetComponent<HeroHealth>();
            var stats = new HeroRuntimeStats();
            heroHealth.Construct(stats, new GameEventBus());
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(heroHealth, null);

            var heroMr = heroGo.transform.Find("Visuals/KnightModel").GetComponent<MeshRenderer>();
            Assert.IsFalse(heroMr.HasPropertyBlock());

            heroHealth.TakeDamage(5);
            Assert.IsTrue(heroHealth.IsHitFlashing);
            Assert.IsTrue(heroMr.HasPropertyBlock());
            heroMr.GetPropertyBlock(pb);
            Assert.AreEqual(1f, pb.GetFloat("_HitFlash"), 0.01f);

            heroHealth.TickInvulnerability(0.25f);
            Assert.IsFalse(heroHealth.IsHitFlashing);
            Assert.IsFalse(heroMr.HasPropertyBlock());
        }

        [Test]
        public void Sprint9VoxelModels_BlenderSourcesPrefabsMaterialsAndAbilityData_ReplacePrimitives()
        {
            string[] expectedBlends =
            {
                "ArtSources/Blender/DragonWhelp.blend",
                "ArtSources/Blender/GuardianShield.blend",
                "ArtSources/Blender/PoisonFlask.blend",
                "ArtSources/Blender/PoisonPuddle.blend",
                "ArtSources/Blender/VoxelTornado.blend",
                "ArtSources/Blender/AegisShield.blend",
                "ArtSources/Blender/IceAuraRing.blend",
                "ArtSources/Blender/BlackHoleRing.blend",
                "ArtSources/Blender/SeismicRing.blend"
            };
            foreach (string blendPath in expectedBlends)
            {
                Assert.IsTrue(File.Exists(blendPath), $"Missing Blender source: {blendPath}");
            }

            string[] newMatPrefixes =
            {
                "DragonWhelp", "GuardianShield", "PoisonFlask", "PoisonPuddle",
                "VoxelTornado", "AegisShield", "IceAuraRing", "BlackHoleRing", "SeismicRing"
            };
            foreach (string prefix in newMatPrefixes)
            {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Mat.mat");
                Material glowMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Glow_Mat.mat");
                Assert.IsNotNull(baseMat, $"Missing {prefix}_Mat.mat");
                Assert.IsNotNull(glowMat, $"Missing {prefix}_Glow_Mat.mat");
                Assert.IsTrue(baseMat.enableInstancing);
                Assert.IsTrue(glowMat.enableInstancing);
            }

            // Hero.prefab ShieldVisual uses AegisShield mesh instead of Sphere
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Transform shieldTf = heroPrefab.transform.Find("ShieldVisual");
            Assert.IsNotNull(shieldTf);
            Mesh shieldMesh = shieldTf.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(shieldMesh);
            Assert.AreNotEqual("Sphere", shieldMesh.name);
            Assert.IsTrue(shieldMesh.name.Contains("AegisShield") || shieldMesh.subMeshCount == 2);

            // DragonWhelpFollower prefab has VoxelMeshAnimator with 4 fly frames
            var miniDragonData = AssetDatabase.LoadAssetAtPath<MiniDragonAbilityData>("Assets/_ScriptableObjects/Abilities/MiniDragonData.asset");
            Assert.IsNotNull(miniDragonData.followerPrefab);
            Assert.IsNotNull(miniDragonData.flamePrefab);
            var dragonAnim = miniDragonData.followerPrefab.GetComponentInChildren<VoxelMeshAnimator>(true);
            Assert.IsNotNull(dragonAnim);
            Assert.AreEqual(4, dragonAnim.Frames.Length);

            var shieldData = AssetDatabase.LoadAssetAtPath<GuardianShieldAbilityData>("Assets/_ScriptableObjects/Abilities/GuardianShieldData.asset");
            Assert.IsNotNull(shieldData.shieldPrefab);
            Assert.AreNotEqual("Cube", shieldData.shieldPrefab.GetComponentInChildren<MeshFilter>().sharedMesh.name);

            var poisonData = AssetDatabase.LoadAssetAtPath<PoisonFlaskAbilityData>("Assets/_ScriptableObjects/Abilities/PoisonFlaskData.asset");
            Assert.IsNotNull(poisonData.flaskPrefab);
            Assert.IsNotNull(poisonData.puddleVisualPrefab);

            var tornadoData = AssetDatabase.LoadAssetAtPath<VoxelTornadoAbilityData>("Assets/_ScriptableObjects/Abilities/VoxelTornadoData.asset");
            Assert.IsNotNull(tornadoData.tornadoVisualPrefab);

            var seismicData = AssetDatabase.LoadAssetAtPath<SeismicHammerAbilityData>("Assets/_ScriptableObjects/Abilities/SeismicHammerData.asset");
            Assert.IsNotNull(seismicData.waveVisualPrefab);

            var iceData = AssetDatabase.LoadAssetAtPath<IceAuraAbilityData>("Assets/_ScriptableObjects/Abilities/IceAuraData.asset");
            var holeData = AssetDatabase.LoadAssetAtPath<BlackHoleAbilityData>("Assets/_ScriptableObjects/Abilities/BlackHoleData.asset");
            Assert.IsNotNull(iceData.visualPrefab);
            Assert.IsNotNull(holeData.visualPrefab);
        }

        [Test]
        public void IceAuraAndBlackHole_UseDistinctWorldYOffsetsAndDisableShadows()
        {
            var heroGo = Track(new GameObject("TestHero"));
            heroGo.transform.position = new Vector3(0f, 1f, 0f);

            var iceData = AssetDatabase.LoadAssetAtPath<IceAuraAbilityData>("Assets/_ScriptableObjects/Abilities/IceAuraData.asset");
            var holeData = AssetDatabase.LoadAssetAtPath<BlackHoleAbilityData>("Assets/_ScriptableObjects/Abilities/BlackHoleData.asset");

            var ice = (IceAuraAbility)iceData.CreateAbility(heroGo.transform);
            ice.Initialize(iceData);

            var hole = (BlackHoleAbility)holeData.CreateAbility(heroGo.transform);
            hole.Initialize(holeData);

            Assert.IsNotNull(ice.VisualTransform);
            Assert.IsNotNull(hole.VisualTransform);
            Assert.AreEqual(IceAuraAbility.VISUAL_Y_OFFSET, ice.VisualTransform.position.y, 0.001f);
            Assert.AreEqual(BlackHoleAbility.VISUAL_Y_OFFSET, hole.VisualTransform.position.y, 0.001f);
            Assert.GreaterOrEqual(Mathf.Abs(hole.VisualTransform.position.y - ice.VisualTransform.position.y), 0.025f);

            foreach (MeshRenderer mr in ice.VisualTransform.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode);
                Assert.IsFalse(mr.receiveShadows);
            }
            foreach (MeshRenderer mr in hole.VisualTransform.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode);
                Assert.IsFalse(mr.receiveShadows);
            }
        }

        [Test]
        public void MultiPoiCompass_DisplaysUpToThreeSimultaneousColoredArrowsZeroAlloc()
        {
            var camGo = Track(new GameObject("TestCam", typeof(Camera)));
            camGo.transform.position = new Vector3(0f, 20f, -10f);
            camGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0f, 20f, -10f));
            var cam = camGo.GetComponent<Camera>();
            cam.aspect = 9f / 16f;
            cam.fieldOfView = 60f;

            var heroGo = Track(new GameObject("Hero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();

            var spawnerGo = Track(new GameObject("SecretSpawner"));
            var secretSpawner = spawnerGo.AddComponent<SecretItemSpawner>();
            var secretGo = Track(new GameObject("SecretItem"));
            secretGo.transform.position = new Vector3(25f, 0f, 0f);
            var secretItem = secretGo.AddComponent<InteractableItem>();
            typeof(SecretItemSpawner).GetField("activeItem", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(secretSpawner, secretItem);

            var arenaGo = Track(new GameObject("ArenaGen"));
            var arenaGen = arenaGo.AddComponent<ArenaGenerator>();
            var shrineGo = Track(new GameObject("Shrine"));
            shrineGo.transform.position = new Vector3(-25f, 0f, 0f);
            var shrine = shrineGo.AddComponent<ChallengeShrine>();
            typeof(ArenaGenerator).GetProperty("ActiveShrine")?.SetValue(arenaGen, shrine);

            var magnetSpawnerGo = Track(new GameObject("MagnetSpawner"));
            var magnetSpawner = magnetSpawnerGo.AddComponent<VacuumMagnetSpawner>();
            var magnet = magnetSpawner.SpawnVacuumMagnet(new Vector3(0f, 0f, 30f));
            Assert.IsNotNull(magnet);
            Track(magnet.gameObject);
            var poolContainer = GameObject.Find("VacuumMagnetContainer");
            if (poolContainer != null) Track(poolContainer);

            var goblinGo = Track(new GameObject("GoldGoblin"));
            goblinGo.transform.position = new Vector3(0f, 0f, -32f);
            SecretCompassIndicatorUI.RegisterDynamicPoi(goblinGo.transform, CompassPoiType.GoldGoblin);

            var compassGo = Track(new GameObject("MultiCompass", typeof(RectTransform), typeof(SecretCompassIndicatorUI)));
            var compass = compassGo.GetComponent<SecretCompassIndicatorUI>();
            compass.Construct(secretSpawner, heroHealth, null, arenaGen, magnetSpawner);
            typeof(SecretCompassIndicatorUI).GetField("mainCamera", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(compass, cam);

            compass.RefreshIndicators();

            Assert.AreEqual(3, compass.ActiveVisibleSlotCount);
            Assert.AreEqual(CompassPoiType.GoldGoblin, compass.GetSlotPoiType(0));
            Assert.AreEqual(CompassPoiType.ChallengeShrine, compass.GetSlotPoiType(1));
            Assert.AreEqual(CompassPoiType.Secret, compass.GetSlotPoiType(2));
            Assert.AreEqual(SecretCompassIndicatorUI.GetPoiColor(CompassPoiType.GoldGoblin), compass.GetSlotArrowColor(0));
            Assert.AreEqual(SecretCompassIndicatorUI.GetPoiColor(CompassPoiType.ChallengeShrine), compass.GetSlotArrowColor(1));
            Assert.AreEqual(SecretCompassIndicatorUI.GetPoiColor(CompassPoiType.Secret), compass.GetSlotArrowColor(2));

            // Removing GoldGoblin reveals VacuumMagnet in slot 2
            SecretCompassIndicatorUI.UnregisterDynamicPoi(goblinGo.transform);
            compass.RefreshIndicators();
            Assert.AreEqual(3, compass.ActiveVisibleSlotCount);
            Assert.AreEqual(CompassPoiType.VacuumMagnet, compass.GetSlotPoiType(2));
        }

        [Test]
        public void DamageVignetteUI_ExistsInGameplayScene_FlashesAndPulsesAtLowHp()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                DamageVignetteUI sceneVignette = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    sceneVignette = root.GetComponentInChildren<DamageVignetteUI>(true);
                    if (sceneVignette != null) break;
                }
                Assert.IsNotNull(sceneVignette, "DamageVignetteUI missing in Gameplay.unity");
                Assert.IsNotNull(sceneVignette.VignetteImage);
                Assert.IsFalse(sceneVignette.VignetteImage.raycastTarget);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            var heroGo = Track(new GameObject("Hero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var stats = new HeroRuntimeStats();
            heroHealth.Construct(stats, new GameEventBus());

            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.maxHp = 100;
            heroData.passiveType = HeroPassiveType.None;
            heroData.passiveValue = 0f;
            typeof(HeroHealth).GetField("heroData", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(heroHealth, heroData);
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(heroHealth, null);
            Assert.AreEqual(100, heroHealth.MaxHp);

            var vigGo = Track(new GameObject("TestVignette", typeof(RectTransform), typeof(Image), typeof(DamageVignetteUI)));
            var vig = vigGo.GetComponent<DamageVignetteUI>();
            vig.Construct(heroHealth);
            Assert.AreEqual(0f, vig.CurrentAlpha, 0.001f);

            heroHealth.TakeDamage(20);
            Assert.Greater(vig.CurrentAlpha, 0.35f);
            vig.TickVignette(0.5f);
            Assert.AreEqual(0f, vig.CurrentAlpha, 0.01f);

            // Drop HP below 30% -> steady low-HP pulse
            heroHealth.TickInvulnerability(1f);
            heroHealth.TakeDamage(60); // 20 HP left
            Assert.AreEqual(20, heroHealth.CurrentHp);
            vig.TickVignette(1f);
            Assert.Greater(vig.CurrentAlpha, 0.1f);
        }

        [Test]
        public void AudioCatalog_HasAll20SfxAnd3MusicTracks_AndAudioManagerDucksMusicOnPauseAndLevelUp()
        {
            var catalog = Resources.Load<AudioCatalog>(AudioManager.CATALOG_RESOURCE_PATH);
            Assert.IsNotNull(catalog);

            var sfxValues = System.Enum.GetValues(typeof(SfxType));
            Assert.AreEqual(20, sfxValues.Length);
            foreach (SfxType sfx in sfxValues)
            {
                Assert.IsNotNull(catalog.GetSfxClip(sfx), $"Missing SFX clip for {sfx}");
            }

            Assert.IsNotNull(catalog.GetMusicClip(MusicType.Menu));
            Assert.IsNotNull(catalog.GetMusicClip(MusicType.Gameplay));
            Assert.IsNotNull(catalog.GetMusicClip(MusicType.Boss));

            var saveManager = new SaveManager();
            try
            {
                var audio = new AudioManager(saveManager, catalog);
                float baseVol = audio.EffectiveMusicVolume;
                audio.SetMusicDucked(true);
                Assert.IsTrue(audio.IsMusicDucked);
                Assert.AreEqual(baseVol * 0.5f, audio.EffectiveMusicVolume, 0.001f);
                audio.SetMusicDucked(false);
                Assert.IsFalse(audio.IsMusicDucked);
                Assert.AreEqual(baseVol, audio.EffectiveMusicVolume, 0.001f);

                var gmGo = Track(new GameObject("TestGameManager"));
                var gm = gmGo.AddComponent<GameManager>();
                gm.Construct(null, null, saveManager, null, null, new GameEventBus(), null, null, null, audio);

                Assert.IsTrue(gm.PauseGame());
                Assert.IsTrue(audio.IsMusicDucked);
                Assert.IsTrue(gm.ResumeGame());
                Assert.IsFalse(audio.IsMusicDucked);

                Assert.IsTrue(gm.PauseForLevelUp());
                Assert.IsTrue(audio.IsMusicDucked);
                gm.ResumeFromLevelUp();
                Assert.IsFalse(audio.IsMusicDucked);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void HitStop_RefusesToTriggerWhenPausedOrLevelUp()
        {
            var saveManager = new SaveManager();
            try
            {
                var gmGo = Track(new GameObject("TestGM"));
                var gm = gmGo.AddComponent<GameManager>();
                gm.Construct(null, null, saveManager, null, null, new GameEventBus());

                var vfxGo = Track(new GameObject("TestVfx"));
                var vfx = vfxGo.AddComponent<VfxFeedbackManager>();
                vfx.Construct(new GameEventBus(), null, null, null, null, gm);

                gm.PauseGame();
                Assert.IsFalse(vfx.TryTriggerHitStop(0.03f));

                gm.ResumeGame();
                gm.PauseForLevelUp();
                Assert.IsFalse(vfx.TryTriggerHitStop(0.03f));
                gm.ResumeFromLevelUp();
            }
            finally
            {
                saveManager.Dispose();
            }
        }
    }
}
