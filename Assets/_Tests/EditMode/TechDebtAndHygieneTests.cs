using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class TechDebtAndHygieneTests
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
        }

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
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        [Test]
        public void CardCatalog_ContainsEveryCardDataAssetInProject_AndNoLegacySynergyCardsExist()
        {
            CardCatalog catalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(catalog, "Resources/CardCatalog.asset must exist");
            Assert.IsNotNull(catalog.cards);
            Assert.Greater(catalog.cards.Count, 80);

            var catalogSet = new HashSet<CardData>();
            for (int i = 0; i < catalog.cards.Count; i++)
            {
                CardData c = catalog.cards[i];
                Assert.IsNotNull(c, $"Null entry in CardCatalog at index {i}");
                Assert.IsTrue(catalogSet.Add(c), $"Duplicate card '{c.name}' in CardCatalog");
            }

            string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/_ScriptableObjects/Cards" });
            Assert.AreEqual(guids.Length, catalog.cards.Count, "CardCatalog must match all CardData assets in Cards/");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Assert.IsFalse(Path.GetFileName(path).StartsWith("SynergyCard_"), $"Legacy synergy card still exists: {path}");
                CardData asset = AssetDatabase.LoadAssetAtPath<CardData>(path);
                Assert.IsTrue(catalogSet.Contains(asset), $"CardData '{path}' is missing from Resources/CardCatalog.asset");
            }
        }

        [Test]
        public void RunRulesConfig_And_ComboConfig_LoadFromResourcesAndDriveRuntimeSystems()
        {
            RunRulesConfig rules = Resources.Load<RunRulesConfig>(RunRulesConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(rules, "Resources/RunRulesConfig.asset must exist");
            Assert.AreEqual(1, rules.freeRerollsPerRun);
            Assert.AreEqual(0.18f, rules.postHitInvulnerabilityDuration, 0.001f);
            Assert.AreEqual(25, rules.crystalsPerGreedBonus);
            Assert.AreEqual(80, rules.maxActiveEnemiesForSummon);
            Assert.AreEqual(4.5f, rules.reviveSafeRadius, 0.001f);

            ComboConfig comboCfg = Resources.Load<ComboConfig>(ComboConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(comboCfg, "Resources/ComboConfig.asset must exist");
            Assert.AreEqual(3.0f, comboCfg.comboWindowSeconds, 0.001f);
            Assert.AreEqual(4, comboCfg.milestones.Length);

            // Custom ComboConfig drives RunComboTracker
            ComboConfig customCombo = Track(ScriptableObject.CreateInstance<ComboConfig>());
            customCombo.comboWindowSeconds = 5.0f;
            customCombo.milestones = new[]
            {
                new ComboMilestoneConfig { killsRequired = 3, rewardBonusPercent = 42f }
            };
            var tracker = new RunComboTracker(new GameEventBus(), new RunStatistics(), customCombo);
            for (int i = 0; i < 3; i++) tracker.RegisterKill();
            Assert.AreEqual(1, tracker.CurrentMilestoneTier);
            Assert.AreEqual(42f, tracker.RewardBonusPercent, 0.001f);
            Assert.AreEqual(5.0f, tracker.RemainingTime, 0.001f);

            // Custom RunRulesConfig drives UpgradeManager freeRerollsPerRun and HeroHealth i-frames
            RunRulesConfig customRules = Track(ScriptableObject.CreateInstance<RunRulesConfig>());
            customRules.freeRerollsPerRun = 4;
            customRules.postHitInvulnerabilityDuration = 0.45f;

            var mgrGo = Track(new GameObject("RulesUpgradeMgr"));
            var upgradeMgr = mgrGo.AddComponent<UpgradeManager>();
            upgradeMgr.SetRunRulesConfig(customRules);
            upgradeMgr.SetCardCatalog(Track(ScriptableObject.CreateInstance<CardCatalog>()));
            upgradeMgr.InitializeRunPool();
            Assert.AreEqual(4, upgradeMgr.FreeRerollsRemaining);

            var heroGo = Track(new GameObject("RulesHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.SetRunRulesConfig(customRules);
            Assert.AreEqual(0.45f, heroHealth.PostHitInvulnerabilityDuration, 0.001f);
        }

        [Test]
        public void EnemyRuntimeModifiers_AreScopedAndDoNotLeakAcrossInstances()
        {
            var bus = new GameEventBus();
            var data = Track(ScriptableObject.CreateInstance<EnemyData>());
            data.hp = 1000;
            data.speed = 4f;
            data.contactDamage = 10;

            var modsA = new EnemyRuntimeModifiers { EventSpeedMultiplier = 1.5f, BossDamageTakenMultiplier = 2.0f };
            var modsB = new EnemyRuntimeModifiers();

            var goA = Track(new GameObject("ScopedEnemyA"));
            var enemyA = goA.AddComponent<EnemyBase>();
            enemyA.SetRuntimeModifiers(modsA);
            enemyA.Initialize(data, null, null, bus, 1f);
            enemyA.MakeMiniBoss(1f, 1f, 1f);

            var goB = Track(new GameObject("ScopedEnemyB"));
            var enemyB = goB.AddComponent<EnemyBase>();
            enemyB.SetRuntimeModifiers(modsB);
            enemyB.Initialize(data, null, null, bus, 1f);
            enemyB.MakeMiniBoss(1f, 1f, 1f);

            Assert.AreEqual(6f, enemyA.EffectiveMoveSpeed, 0.001f);
            Assert.AreEqual(4f, enemyB.EffectiveMoveSpeed, 0.001f);

            int hpABefore = enemyA.CurrentHp;
            int hpBBefore = enemyB.CurrentHp;
            enemyA.TakeDamage(100);
            enemyB.TakeDamage(100);
            Assert.AreEqual(hpABefore - 200, enemyA.CurrentHp);
            Assert.AreEqual(hpBBefore - 100, enemyB.CurrentHp);
        }

        [Test]
        public void WaveManager_SpawnEnemyForWave_DoesNotMutateCurrentWave()
        {
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 3f;
            enemyData.contactDamage = 5;

            var waveGo = Track(new GameObject("G8WaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            Assert.AreEqual(1, waveMgr.CurrentWave);

            EnemyBase spawnedEnemy = waveMgr.SpawnEnemyForWave(enemyData, Vector3.right * 4f, waveNumber: 25);
            Assert.IsNotNull(spawnedEnemy);
            Assert.AreEqual(1, waveMgr.CurrentWave, "SpawnEnemyForWave must not mutate WaveManager.CurrentWave");
        }

        [Test]
        public void UrpSettings_UseSingleProjectBPostFxProfile_NoSampleSceneProfile_AndMobileShadowDistance30()
        {
            Assert.IsFalse(File.Exists("Assets/Settings/SampleSceneProfile.asset"), "SampleSceneProfile.asset must be deleted");

            var postFx = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/ProjectB_PostFX.asset");
            Assert.IsNotNull(postFx, "Assets/Settings/ProjectB_PostFX.asset must exist");

            var mobileRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var pcRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.IsNotNull(mobileRp);
            Assert.IsNotNull(pcRp);
            Assert.AreEqual(30f, mobileRp.shadowDistance, 0.01f);

            var mobileSo = new SerializedObject(mobileRp);
            var pcSo = new SerializedObject(pcRp);
            Assert.AreSame(postFx, mobileSo.FindProperty("m_VolumeProfile").objectReferenceValue);
            Assert.AreSame(postFx, pcSo.FindProperty("m_VolumeProfile").objectReferenceValue);
        }

        [Test]
        public void ScriptableObjectsFolder_IsOrganizedIntoSubfolders()
        {
            string[] expectedSubfolders = { "Heroes", "Enemies", "Bosses", "Weapons" };
            foreach (string folder in expectedSubfolders)
            {
                Assert.IsTrue(AssetDatabase.IsValidFolder($"Assets/_ScriptableObjects/{folder}"), $"Missing subfolder {folder}");
            }

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/KnightData.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/Enemies/BombSlimeData.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/Enemies/DefaultEnemyData.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/DemonLordBossData.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_ScriptableObjects/Weapons/KnightWeaponData.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_ScriptableObjects/Weapons/EnemyShooterWeaponData.asset"));

            // Only ArenaConfig.asset and WaveConfig.asset may live directly in Assets/_ScriptableObjects/
            foreach (string rootAsset in Directory.GetFiles("Assets/_ScriptableObjects", "*.asset", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(rootAsset);
                Assert.IsTrue(
                    fileName == "ArenaConfig.asset" || fileName == "WaveConfig.asset",
                    $"Unexpected root ScriptableObject asset: {rootAsset}");
            }
        }

        [Test]
        public void AchievementManager_UsesCompletedCacheAndHandlesReset()
        {
            using var save = new SaveManager();
            var achMgr = new AchievementManager(save);

            achMgr.OnWaveReached(10);
            Assert.IsTrue(save.Data.completedAchievements.Contains("reach_wave_10"));

            // Calling again with same condition must be a fast no-op via cache
            int completedCount = save.Data.completedAchievements.Count;
            achMgr.OnWaveReached(10);
            Assert.AreEqual(completedCount, save.Data.completedAchievements.Count);

            // ResetAll invalidates cache so achievement can trigger again
            save.ResetAll();
            Assert.IsFalse(save.Data.completedAchievements.Contains("reach_wave_10"));
            achMgr.OnWaveReached(10);
            Assert.IsTrue(save.Data.completedAchievements.Contains("reach_wave_10"));
        }
    }
}
