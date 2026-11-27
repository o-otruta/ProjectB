using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class VisualAndPassiveFixesTests
    {
        private readonly List<UnityEngine.Object> tracked = new List<UnityEngine.Object>();
        private bool savedFogEnabled;
        private FogMode savedFogMode;
        private Color savedFogColor;
        private float savedFogStart;
        private float savedFogEnd;
        private float savedFogDensity;

        [SetUp]
        public void SetUp()
        {
            savedFogEnabled = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;
            savedFogDensity = RenderSettings.fogDensity;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (tracked[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(tracked[i]);
                }
            }
            tracked.Clear();
            RenderSettings.fog = savedFogEnabled;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            RenderSettings.fogDensity = savedFogDensity;
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) tracked.Add(obj);
            return obj;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        [Test]
        public void DenseFog_GameplaySceneUsesDistantLinearFog_AndShaderWarmupCollectionIsPreloaded()
        {
            ShaderVariantCollection warmup = Resources.Load<ShaderVariantCollection>(GameplayShaderWarmup.RESOURCE_PATH);
            Assert.IsNotNull(warmup, "Gameplay shader warmup collection must exist in Resources");
            Assert.Greater(warmup.variantCount, 0, "Warmup collection must contain shader variants");

            UnityEngine.Object gs = GraphicsSettings.GetGraphicsSettings();
            Assert.IsNotNull(gs);
            var so = new SerializedObject(gs);
            SerializedProperty preloaded = so.FindProperty("m_PreloadedShaders");
            bool foundInPreloaded = false;
            for (int i = 0; i < preloaded.arraySize; i++)
            {
                if (preloaded.GetArrayElementAtIndex(i).objectReferenceValue == warmup)
                {
                    foundInPreloaded = true;
                    break;
                }
            }
            Assert.IsTrue(foundInPreloaded, "ShaderVariantCollection must be registered in GraphicsSettings.m_PreloadedShaders");

            // Check Gameplay.unity baseline fog settings
            string activeScenePath = SceneManager.GetActiveScene().path;
            Scene gpScene = EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(RenderSettings.fog, "Gameplay.unity must keep FOG_LINEAR active at baseline");
                Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
                Assert.GreaterOrEqual(RenderSettings.fogStartDistance, 200f, "Baseline fog must be distant/invisible during normal play");
            }
            finally
            {
                if (!string.IsNullOrEmpty(activeScenePath) && activeScenePath != gpScene.path)
                {
                    EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
                }
            }

            // Verify WaveEventManager blends smoothly from distant baseline fog (500..600m) and restores cleanly
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.1f, 0.13f, 0.21f, 1f);
            RenderSettings.fogStartDistance = 500f;
            RenderSettings.fogEndDistance = 600f;

            var bus = new GameEventBus();
            var dummyEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
            var cfg = Track(ScriptableObject.CreateInstance<WaveConfig>());
            var fogMutator = AssetDatabase.LoadAssetAtPath<WaveMutatorData>("Assets/_ScriptableObjects/WaveMutators/Mutator_Fog.asset");
            Assert.IsNotNull(fogMutator);
            cfg.waveMutatorsEnabled = true;
            cfg.waveMutators = new List<WaveMutatorData> { fogMutator };
            cfg.enemySchedule = new List<EnemyWaveEntry> { new EnemyWaveEntry { enemy = dummyEnemy, minWave = 1 } };

            var waveGo = Track(new GameObject("FogWarmupWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);

            var evtGo = Track(new GameObject("FogWarmupEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.Construct(waveMgr, null, null, null, null, bus, null, new EnemyRuntimeModifiers());

            Assert.IsTrue(evtMgr.StartMutator(WaveMutatorType.Fog));
            evtMgr.TickEvent(0.25f); // fogBlend = 0.5
            Assert.AreEqual(0.5f, evtMgr.FogBlend, 0.001f);
            Assert.Less(RenderSettings.fogStartDistance, 60f, "Fog start must blend from clamped near-range, not 500m");

            evtMgr.EndCurrentMutator(completed: true);
            evtMgr.TickEvent(1.0f);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(500f, RenderSettings.fogStartDistance, 0.1f);
            Assert.AreEqual(600f, RenderSettings.fogEndDistance, 0.1f);
        }

        [Test]
        public void FrostWispZone_UsesStylizedFrostShader_NoFlatCyanCubes_AndZeroGcTick()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/FrostWispZoneVisual.prefab");
            Assert.IsNotNull(prefab);

            Transform patch = prefab.transform.Find("FrostPatch");
            Assert.IsNotNull(patch, "FrostWispZoneVisual must have FrostPatch child");
            var patchRenderer = patch.GetComponent<MeshRenderer>();
            Assert.IsNotNull(patchRenderer);
            Assert.IsNotNull(patchRenderer.sharedMaterial);
            Assert.AreEqual("ProjectB/FrostZoneGround", patchRenderer.sharedMaterial.shader.name);
            Assert.AreNotEqual("Universal Render Pipeline/Unlit", patchRenderer.sharedMaterial.shader.name);

            foreach (MeshRenderer mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode);
                Assert.IsFalse(mr.receiveShadows);
            }

            var zoneGo = Track(new GameObject("TestFrostSlowZone"));
            var zone = zoneGo.AddComponent<FrostSlowZone>();
            var wispData = AssetDatabase.LoadAssetAtPath<FrostWispEnemyData>("Assets/_ScriptableObjects/Enemies/FrostWispData.asset");
            Assert.IsNotNull(wispData);

            zone.Initialize(
                Vector3.zero,
                wispData.frostZoneRadius,
                wispData.frostZoneDuration,
                wispData.heroSlowFactor,
                wispData.slowRefreshDuration,
                wispData.zoneTickInterval,
                null,
                null,
                patchRenderer.sharedMaterial,
                null,
                prefab);

            // Warmup + Zero-GC check
            zone.TickZone(0.05f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                zone.TickZone(0.02f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "FrostSlowZone.TickZone must be Zero-GC");
        }

        [Test]
        public void IceAura_UsesFrostFieldShader_PerimeterCrystalsDoNotStretch_AndPulsesOnTick()
        {
            var iceData = AssetDatabase.LoadAssetAtPath<IceAuraAbilityData>("Assets/_ScriptableObjects/Abilities/IceAuraData.asset");
            Assert.IsNotNull(iceData);

            var heroGo = Track(new GameObject("IceAuraTestHero"));
            heroGo.transform.position = new Vector3(0f, 1f, 0f);
            var ice = (IceAuraAbility)iceData.CreateAbility(heroGo.transform);
            ice.Initialize(iceData);

            Transform visual = ice.VisualTransform;
            Assert.IsNotNull(visual);
            Assert.AreEqual(IceAuraAbility.VISUAL_Y_OFFSET, visual.position.y, 0.001f);

            Transform groundField = visual.Find("GroundField");
            Assert.IsNotNull(groundField);
            var gfRenderer = groundField.GetComponent<MeshRenderer>();
            Assert.IsNotNull(gfRenderer);
            Assert.AreEqual("ProjectB/IceAuraGround", gfRenderer.sharedMaterial.shader.name);

            Transform crystalRing = visual.Find("CrystalRing");
            Assert.IsNotNull(crystalRing);
            Assert.AreEqual(8, crystalRing.childCount);

            var snowPs = visual.GetComponentInChildren<ParticleSystem>(true);
            Assert.IsNotNull(snowPs);

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode);
                Assert.IsFalse(r.receiveShadows);
            }

            float baseRadius = ice.EffectiveRadius;
            Assert.AreEqual(baseRadius * 2f, groundField.localScale.x, 0.001f);

            ice.ApplyModifier(ModifierType.Radius, 3f);
            float expandedRadius = ice.EffectiveRadius;
            Assert.Greater(expandedRadius, baseRadius);
            Assert.AreEqual(expandedRadius * 2f, groundField.localScale.x, 0.001f);

            for (int i = 0; i < crystalRing.childCount; i++)
            {
                Transform c = crystalRing.GetChild(i);
                Assert.AreEqual(Vector3.one, c.localScale, "Perimeter crystals must never stretch when radius grows");
                float dist = new Vector2(c.localPosition.x, c.localPosition.z).magnitude;
                Assert.AreEqual(expandedRadius * 0.95f, dist, 0.01f);
            }
        }

        [Test]
        public void HpRegenPassive_HasNoDuplicate_AndOverdriveAchievementGrantsCoins()
        {
            var abCatalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>("Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset");
            Assert.IsNotNull(abCatalog);

            int regenCount = 0;
            string regenId = null;
            foreach (AbilityData ab in abCatalog.abilities)
            {
                if (ab == null) continue;
                Assert.AreNotEqual("VitalityRegen", ab.id, "Duplicate VitalityRegen passive must be removed");
                if (ab is StatPassiveAbilityData sp && sp.effectType == PassiveEffectType.HealthRegenPerSecond)
                {
                    regenCount++;
                    regenId = sp.id;
                }
            }
            Assert.AreEqual(1, regenCount, "Exactly one HP regen passive should exist");
            Assert.AreEqual("TitanHeart", regenId);

            CardCatalog cardCatalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(cardCatalog);
            foreach (CardData card in cardCatalog.cards)
            {
                Assert.IsNotNull(card);
                if (card is AbilityCardData ac)
                {
                    Assert.AreNotEqual("VitalityRegen", ac.abilityData != null ? ac.abilityData.id : null);
                }
                else if (card is AbilityModifierCardData mc)
                {
                    Assert.AreNotEqual("VitalityRegen", mc.targetAbilityId);
                }
            }

            var lvl10 = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/ability_lvl_10.asset");
            Assert.IsNotNull(lvl10);
            Assert.AreEqual(AchievementRewardType.GiveCoins, lvl10.rewardType);
            Assert.AreEqual(800, lvl10.rewardValue);
            Assert.IsTrue(string.IsNullOrEmpty(lvl10.rewardId));

            var allAbilities = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/all_abilities.asset");
            Assert.IsNotNull(allAbilities);
            Assert.AreEqual(10, allAbilities.targetValue);
        }
    }
}
