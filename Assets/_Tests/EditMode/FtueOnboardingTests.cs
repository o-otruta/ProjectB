using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Sprint 12.2: First-Time User Experience (FTUE) — LocalizationService minimal EN fallbacks,
    /// lighter first-run waves 1–2 (no elites/events), GameplayFtueUI (move, first LevelUp, first ready ultimate),
    /// MainMenuUI Upgrades tab hint using dynamic MetaUpgradeData costs, and scene wiring hygiene.
    /// </summary>
    public class FtueOnboardingTests
    {
        private const string MainMenuPath = "Assets/_Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/_Scenes/Gameplay.unity";
        private const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<SaveManager> saveManagers = new List<SaveManager>();
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
            for (int i = saveManagers.Count - 1; i >= 0; i--)
            {
                saveManagers[i]?.Dispose();
            }
            saveManagers.Clear();

            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
            created.Clear();

            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
            Physics.SyncTransforms();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) created.Add(obj);
            return obj;
        }

        private SaveManager CreateSaveManager()
        {
            var sm = new SaveManager();
            saveManagers.Add(sm);
            return sm;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{fieldName} missing");
            f.SetValue(target, value);
        }

        private static object GetPrivate(object target, string fieldName)
        {
            FieldInfo f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{fieldName} missing");
            return f.GetValue(target);
        }

        private static void WithScene(string path, Action<Scene> action)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : loaded;
            try
            {
                action(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return result;
        }

        [Test]
        public void LocalizationService_ReturnsFtueFallbacks_HandlesMissing_AndIsZeroGc()
        {
            var loc = new LocalizationService();

            Assert.IsTrue(loc.HasKey(LocalizationService.KEY_FTUE_MOVE_HINT));
            Assert.IsTrue(loc.HasKey(LocalizationService.KEY_FTUE_LEVELUP_HINT));
            Assert.IsTrue(loc.HasKey(LocalizationService.KEY_FTUE_ULTIMATE_HINT));
            Assert.IsTrue(loc.HasKey(LocalizationService.KEY_FTUE_UPGRADES_HINT));

            Assert.AreEqual("Drag to move — attack is automatic", loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT));
            Assert.AreEqual("Choose an upgrade", loc.Get(LocalizationService.KEY_FTUE_LEVELUP_HINT));
            Assert.AreEqual("Ultimate ready! Tap to unleash", loc.Get(LocalizationService.KEY_FTUE_ULTIMATE_HINT));
            Assert.AreEqual("Upgrade your hero!", loc.Get(LocalizationService.KEY_FTUE_UPGRADES_HINT));
            Assert.AreEqual("missing.key", loc.Get("missing.key"));
            Assert.AreEqual("Fallback", loc.Get("missing.key", "Fallback"));

            // Warm-up
            for (int i = 0; i < 5; i++)
            {
                _ = loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                string val = loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT);
                Assert.IsNotNull(val);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "LocalizationService.Get must not allocate on cached lookup");
        }

        [Test]
        public void WaveConfigAsset_HasTwoFtueWavesConfigured_WithExpectedLighterStats()
        {
            WaveConfig waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(waveConfig);
            Assert.AreEqual(2, waveConfig.FtueWaveCount, "WaveConfig.asset must define 2 FTUE waves");

            Assert.AreEqual(4, waveConfig.ftueWaves[0].enemyCount);
            Assert.AreEqual(0.7f, waveConfig.ftueWaves[0].spawnDelay, 1e-4f);
            Assert.AreEqual(0.75f, waveConfig.ftueWaves[0].hpMultiplier, 1e-4f);
            Assert.AreEqual(0.85f, waveConfig.ftueWaves[0].damageMultiplier, 1e-4f);

            Assert.AreEqual(5, waveConfig.ftueWaves[1].enemyCount);
            Assert.AreEqual(0.65f, waveConfig.ftueWaves[1].spawnDelay, 1e-4f);
            Assert.AreEqual(0.85f, waveConfig.ftueWaves[1].hpMultiplier, 1e-4f);
            Assert.AreEqual(0.90f, waveConfig.ftueWaves[1].damageMultiplier, 1e-4f);
        }

        [Test]
        public void WaveConfig_FtueWaves_ApplyOnlyOnFirstRunWaves1And2_AndRevertFromWave3()
        {
            WaveConfig assetConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(assetConfig);

            Assert.IsTrue(assetConfig.IsFtueProtectedWave(1, isFtueRun: true));
            Assert.IsTrue(assetConfig.IsFtueProtectedWave(2, isFtueRun: true));
            Assert.IsFalse(assetConfig.IsFtueProtectedWave(3, isFtueRun: true));
            Assert.IsFalse(assetConfig.IsFtueProtectedWave(1, isFtueRun: false));

            Assert.AreEqual(4, assetConfig.GetWaveEnemyCount(1, isFtueRun: true));
            Assert.AreEqual(5, assetConfig.GetWaveEnemyCount(2, isFtueRun: true));
            Assert.AreEqual(assetConfig.GetWaveEnemyCount(3, isFtueRun: false), assetConfig.GetWaveEnemyCount(3, isFtueRun: true));

            Assert.AreEqual(6, assetConfig.GetWaveEnemyCount(1, isFtueRun: false));
            Assert.AreEqual(0.75f, assetConfig.GetHpDifficultyMultiplier(1, isFtueRun: true), 1e-4f);
            Assert.AreEqual(1.00f, assetConfig.GetHpDifficultyMultiplier(1, isFtueRun: false), 1e-4f);
            Assert.Less(assetConfig.GetHpDifficultyMultiplier(2, isFtueRun: true), assetConfig.GetHpDifficultyMultiplier(2, isFtueRun: false));
            Assert.AreEqual(assetConfig.GetHpDifficultyMultiplier(3, isFtueRun: false), assetConfig.GetHpDifficultyMultiplier(3, isFtueRun: true), 1e-4f);
        }

        [Test]
        public void WaveManager_AndWaveEventManager_FtueRun_UsesLighterCounts_DisablesElitesAndEvents()
        {
            WaveConfig assetConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            WaveConfig testConfig = Track(UnityEngine.Object.Instantiate(assetConfig));
            testConfig.waveDelay = 0.01f;
            testConfig.maxWaveDuration = 0f;
            testConfig.minWaveForElites = 1;
            testConfig.eliteChancePerWave = 1f;
            testConfig.maxEliteChance = 1f;
            testConfig.firstWaveEventWave = 1;
            testConfig.waveEventsEnabled = true;

            EnemyData dummyEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
            dummyEnemy.name = "FtueDummySlime";
            dummyEnemy.hp = 100;
            dummyEnemy.speed = 2f;
            dummyEnemy.contactDamage = 10;

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

            Assert.IsFalse(WaveEventManager.CanTriggerEventOnWave(testConfig, 1, isFtueRun: true));
            Assert.IsFalse(WaveEventManager.CanTriggerEventOnWave(testConfig, 2, isFtueRun: true));
            Assert.IsTrue(WaveEventManager.CanTriggerEventOnWave(testConfig, 3, isFtueRun: true));
            Assert.IsTrue(WaveEventManager.CanTriggerEventOnWave(testConfig, 1, isFtueRun: false));

            SaveManager sm = CreateSaveManager();
            Assert.IsTrue(sm.IsFirstRun);

            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("FtueTestHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(new HeroRuntimeStats(), bus, null);

            var waveGo = Track(new GameObject("FtueWaveManager", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivate(waveMgr, "waveConfig", testConfig);
            waveMgr.Construct(heroHealth, null, bus, null, null, null, null, null, null, sm);
            waveMgr.EnsurePoolsInitialized();
            Assert.IsTrue(waveMgr.IsFtueRun);

            MethodInfo spawnWaveMethod = typeof(WaveManager).GetMethod("SpawnWaveCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(spawnWaveMethod);

            SetPrivate(waveMgr, "currentWave", 1);
            IEnumerator wave1Enum = (IEnumerator)spawnWaveMethod.Invoke(waveMgr, null);
            for (int step = 0; step < 10; step++)
            {
                if (!wave1Enum.MoveNext()) break;
            }

            Assert.AreEqual(4, waveMgr.EnemiesAlive, "FTUE wave 1 should spawn 4 enemies");
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Assert.IsFalse(e.IsElite, "FTUE wave 1 must not spawn elites even when minWaveForElites=1");
                Assert.AreEqual(75, e.MaxHp, "FTUE wave 1 HP multiplier 0.75 should yield 75 HP on 100-HP enemy");
            }
        }

        [Test]
        public void GameplayFtueUI_FirstRun_ShowsMoveHint_DismissesOnMove_ShowsLevelUpHint_AndShowsUltimateHintAfterLevelUpOrWave2()
        {
            SaveManager sm = CreateSaveManager();
            Assert.IsTrue(sm.IsFirstRun);
            var loc = new LocalizationService();
            var bus = new GameEventBus();

            var heroGo = Track(new GameObject("FtueHero", typeof(Rigidbody)));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(new HeroRuntimeStats(), bus, null);
            var heroMovement = heroGo.AddComponent<HeroMovement>();
            var heroUltimate = heroGo.AddComponent<HeroUltimate>();
            heroUltimate.Construct(heroHealth, null, null, bus, new HeroRuntimeStats(), null);

            var joyGo = Track(new GameObject("Joystick", typeof(RectTransform), typeof(VirtualJoystick)));
            var joystick = joyGo.GetComponent<VirtualJoystick>();

            var cardSelGo = Track(new GameObject("CardSelection", typeof(RectTransform), typeof(CardSelectionUI)));
            var selPanel = new GameObject("SelectionPanel", typeof(RectTransform));
            selPanel.transform.SetParent(cardSelGo.transform, false);
            var cardSelection = cardSelGo.GetComponent<CardSelectionUI>();
            SetPrivate(cardSelection, "selectionPanel", selPanel);

            var ftueGo = Track(new GameObject("GameplayFtue", typeof(RectTransform), typeof(GameplayFtueUI)));
            var moveRoot = new GameObject("MoveHint", typeof(RectTransform), typeof(CanvasGroup));
            moveRoot.transform.SetParent(ftueGo.transform, false);
            var moveTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            moveTxt.transform.SetParent(moveRoot.transform, false);

            var lvlRoot = new GameObject("LevelUpHint", typeof(RectTransform), typeof(CanvasGroup));
            lvlRoot.transform.SetParent(ftueGo.transform, false);
            var lvlTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            lvlTxt.transform.SetParent(lvlRoot.transform, false);

            var ultRoot = new GameObject("UltimateHint", typeof(RectTransform), typeof(CanvasGroup));
            ultRoot.transform.SetParent(ftueGo.transform, false);
            var ultTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            ultTxt.transform.SetParent(ultRoot.transform, false);
            var ultRing = new GameObject("UltRing", typeof(RectTransform));
            ultRing.transform.SetParent(ftueGo.transform, false);

            var ftue = ftueGo.GetComponent<GameplayFtueUI>();
            SetPrivate(ftue, "moveHintRoot", moveRoot);
            SetPrivate(ftue, "moveHintGroup", moveRoot.GetComponent<CanvasGroup>());
            SetPrivate(ftue, "moveHintText", moveTxt);
            SetPrivate(ftue, "levelUpHintRoot", lvlRoot);
            SetPrivate(ftue, "levelUpHintGroup", lvlRoot.GetComponent<CanvasGroup>());
            SetPrivate(ftue, "levelUpHintText", lvlTxt);
            SetPrivate(ftue, "ultimateHintRoot", ultRoot);
            SetPrivate(ftue, "ultimateHintGroup", ultRoot.GetComponent<CanvasGroup>());
            SetPrivate(ftue, "ultimateHintText", ultTxt);
            SetPrivate(ftue, "ultimateHighlightRing", ultRing);

            ftue.Construct(sm, loc, null, null, joystick, heroMovement, heroUltimate, cardSelection, bus);
            ftue.InitializeFtue();

            Assert.IsTrue(ftue.IsFtueActive);
            Assert.IsTrue(ftue.IsMoveHintVisible);
            Assert.IsTrue(moveRoot.activeSelf);
            Assert.IsFalse(ftue.IsLevelUpHintVisible);
            Assert.IsFalse(ftue.IsUltimateHintVisible, "Ultimate hint must NOT appear on frame 1 before LevelUp or Wave 2");
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT), moveTxt.text);

            // Simulate hero displacement -> movement hint disappears
            heroGo.transform.position = new Vector3(1.5f, 0f, 0f);
            ftue.TickFtue();
            Assert.IsTrue(ftue.HasDismissedMoveHint);
            Assert.IsFalse(ftue.IsMoveHintVisible);
            Assert.IsFalse(moveRoot.activeSelf);
            Assert.IsFalse(ftue.IsUltimateHintVisible, "Ultimate hint still waits for first LevelUp or Wave 2");

            // Simulate first LevelUp -> LevelUp hint appears, then disappears on card selection, then Ultimate hint appears
            var dummyCard = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            dummyCard.cardName = "Max HP";
            bool selected = false;
            cardSelection.ShowCards(new List<CardData> { dummyCard }, _ => selected = true);
            Assert.IsTrue(ftue.IsLevelUpHintVisible);
            Assert.IsTrue(lvlRoot.activeSelf);

            MethodInfo onCardClicked = typeof(CardSelectionUI).GetMethod("OnCardClicked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onCardClicked);
            onCardClicked.Invoke(cardSelection, new object[] { 0, dummyCard });
            Assert.IsTrue(selected);
            Assert.IsTrue(ftue.HasCompletedFirstLevelUp);
            Assert.IsFalse(ftue.IsLevelUpHintVisible);
            Assert.IsFalse(lvlRoot.activeSelf);

            Assert.IsTrue(ftue.IsUltimateHintVisible, "Ultimate hint appears once first LevelUp completes and ultimate is ready");
            Assert.IsTrue(ultRoot.activeSelf);
            Assert.IsTrue(ultRing.activeSelf);

            // Second LevelUp must NOT show LevelUp hint again
            cardSelection.ShowCards(new List<CardData> { dummyCard }, _ => { });
            Assert.IsFalse(ftue.IsLevelUpHintVisible);

            // Activating ultimate dismisses the ultimate hint permanently for this run
            Assert.IsTrue(heroUltimate.TryActivate());
            Assert.IsTrue(ftue.HasDismissedUltimateHint);
            Assert.IsFalse(ftue.IsUltimateHintVisible);
            Assert.IsFalse(ultRoot.activeSelf);
            Assert.IsFalse(ultRing.activeSelf);
        }

        [Test]
        public void GameplayFtueUI_FirstRun_DoesNotShowUltimateHint_IfUltimateAlreadyUsedEarly()
        {
            SaveManager sm = CreateSaveManager();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("FtueEarlyUltHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(new HeroRuntimeStats(), bus, null);
            var heroUltimate = heroGo.AddComponent<HeroUltimate>();
            heroUltimate.Construct(heroHealth, null, null, bus, new HeroRuntimeStats(), null);

            var ftueGo = Track(new GameObject("GameplayFtueEarly", typeof(RectTransform), typeof(GameplayFtueUI)));
            var ultRoot = new GameObject("UltimateHint", typeof(RectTransform));
            ultRoot.transform.SetParent(ftueGo.transform, false);
            var ftue = ftueGo.GetComponent<GameplayFtueUI>();
            SetPrivate(ftue, "ultimateHintRoot", ultRoot);
            ftue.Construct(sm, new LocalizationService(), null, null, null, null, heroUltimate, null, bus);
            ftue.InitializeFtue();

            Assert.IsTrue(heroUltimate.TryActivate());
            Assert.IsTrue(ftue.HasDismissedUltimateHint);

            bus.Publish(new WaveCompletedEvent(1, 2));
            heroUltimate.ResetCooldown();
            ftue.TickFtue();

            Assert.IsFalse(ftue.IsUltimateHintVisible, "Already-used ultimate must not re-trigger the FTUE hint on wave 2");
            Assert.IsFalse(ultRoot.activeSelf);
        }

        [Test]
        public void GameplayFtueUI_SubsequentRun_KeepsAllHintsHidden()
        {
            SaveManager sm = CreateSaveManager();
            sm.RecordRunResult(new RunResult { waveReached = 5, coinsEarned = 20 });
            Assert.IsFalse(sm.IsFirstRun);

            var ftueGo = Track(new GameObject("GameplayFtueSecondRun", typeof(RectTransform), typeof(GameplayFtueUI)));
            var moveRoot = new GameObject("MoveHint", typeof(RectTransform));
            moveRoot.transform.SetParent(ftueGo.transform, false);
            var ftue = ftueGo.GetComponent<GameplayFtueUI>();
            SetPrivate(ftue, "moveHintRoot", moveRoot);
            ftue.Construct(sm, new LocalizationService());
            ftue.InitializeFtue();

            Assert.IsFalse(ftue.IsFtueActive);
            Assert.IsFalse(ftue.IsMoveHintVisible);
            Assert.IsFalse(moveRoot.activeSelf);
        }

        [Test]
        public void MainMenuUI_FtueUpgradesHint_UsesDynamicUpgradePrices_WaitsUntilAffordable_AndDismissesOnTabClick()
        {
            SaveManager sm = CreateSaveManager();
            var upgradeManager = new MetaUpgradeManager(sm);
            var bonusManager = new DailyBonusManager(sm);
            var loc = new LocalizationService();

            int cheapestCost = upgradeManager.GetCheapestAvailableUpgradeCost();
            Assert.Greater(cheapestCost, 0, "MetaUpgrades in Resources must have positive upgrade costs");

            var menuGo = Track(new GameObject("Menu", typeof(RectTransform), typeof(MainMenuUI)));
            var glow = new GameObject("FtueGlow", typeof(RectTransform));
            glow.transform.SetParent(menuGo.transform, false);
            var hintRoot = new GameObject("FtueHint", typeof(RectTransform));
            hintRoot.transform.SetParent(menuGo.transform, false);
            var hintTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            hintTxt.transform.SetParent(hintRoot.transform, false);

            var tabBarGo = Track(new GameObject("TabBar", typeof(RectTransform), typeof(TabBarUI)));
            var tabBar = tabBarGo.GetComponent<TabBarUI>();
            var tabs = new BottomTabUI[5];
            var badges = new BadgeUI[5];
            for (int i = 0; i < 5; i++)
            {
                var tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(BottomTabUI));
                tabGo.transform.SetParent(tabBarGo.transform, false);
                tabs[i] = tabGo.GetComponent<BottomTabUI>();

                var badgeGo = new GameObject($"Badge_{i}", typeof(RectTransform), typeof(Image), typeof(BadgeUI));
                badgeGo.transform.SetParent(tabGo.transform, false);
                badges[i] = badgeGo.GetComponent<BadgeUI>();
                badgeGo.SetActive(false);
            }
            tabBar.EditorSetup(tabs, badges, null);

            var menu = menuGo.GetComponent<MainMenuUI>();
            SetPrivate(menu, "tabBar", tabBar);
            SetPrivate(menu, "tabs", tabs);
            SetPrivate(menu, "ftueUpgradesGlow", glow);
            SetPrivate(menu, "ftueUpgradesHintRoot", hintRoot);
            SetPrivate(menu, "ftueUpgradesHintText", hintTxt);
            menu.Construct(sm, bonusManager, null, null, upgradeManager, loc);

            // Before first run -> hidden even if coins exist
            sm.AddCoins(cheapestCost, saveImmediately: false);
            Assert.IsFalse(menu.IsFtueUpgradesHintActive);
            Assert.IsFalse(glow.activeSelf);
            Assert.IsFalse(hintRoot.activeSelf);

            // After first death with fewer coins than cheapest upgrade -> still hidden & NOT dismissed
            sm.ResetAll();
            sm.RecordRunResult(new RunResult { waveReached = 3, coinsEarned = Mathf.Max(0, cheapestCost - 1) });
            Assert.IsFalse(menu.IsFtueUpgradesHintActive);
            Assert.IsFalse(sm.IsFtueUpgradesHintDismissed);

            // Clicking Upgrades before having enough coins does NOT dismiss the FTUE hint
            menu.OnUpgradesClicked();
            Assert.IsFalse(sm.IsFtueUpgradesHintDismissed);
            menu.OnPlayClicked();

            // Once coins reach the cheapest MetaUpgradeData cost -> badge, glow, and hint appear
            sm.AddCoins(1, saveImmediately: false);
            Assert.IsTrue(menu.IsFtueUpgradesHintActive);
            Assert.IsTrue(glow.activeSelf);
            Assert.IsTrue(hintRoot.activeSelf);
            Assert.IsTrue(badges[MainMenuUI.TAB_UPGRADES].IsVisible);
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_UPGRADES_HINT), hintTxt.text);

            // Opening Upgrades tab dismisses the FTUE hint permanently
            menu.OnUpgradesClicked();
            Assert.IsTrue(sm.IsFtueUpgradesHintDismissed);
            Assert.IsFalse(menu.IsFtueUpgradesHintActive);
            Assert.IsFalse(glow.activeSelf);
            Assert.IsFalse(hintRoot.activeSelf);
            Assert.IsFalse(badges[MainMenuUI.TAB_UPGRADES].IsVisible);
        }

        [Test]
        public void Scenes_GameplayAndMainMenu_HaveFtueObjectsWired_WithThemeHygiene()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);

            WithScene(GameplayPath, scene =>
            {
                List<GameplayFtueUI> ftues = FindInScene<GameplayFtueUI>(scene);
                Assert.AreEqual(1, ftues.Count, "Gameplay.unity must have exactly one GameplayFtueUI");
                GameplayFtueUI ftue = ftues[0];

                foreach (string field in new[]
                {
                    "moveHintRoot", "moveHintGroup", "moveHintText",
                    "levelUpHintRoot", "levelUpHintGroup", "levelUpHintText",
                    "ultimateHintRoot", "ultimateHintGroup", "ultimateHintText", "ultimateHighlightRing"
                })
                {
                    Assert.IsNotNull(GetPrivate(ftue, field), $"GameplayFtueUI.{field} must be wired");
                }

                var moveRoot = (GameObject)GetPrivate(ftue, "moveHintRoot");
                var lvlRoot = (GameObject)GetPrivate(ftue, "levelUpHintRoot");
                var ultRoot = (GameObject)GetPrivate(ftue, "ultimateHintRoot");
                var ultRing = (GameObject)GetPrivate(ftue, "ultimateHighlightRing");

                Assert.IsFalse(moveRoot.activeSelf);
                Assert.IsFalse(lvlRoot.activeSelf);
                Assert.IsFalse(ultRoot.activeSelf);
                Assert.IsFalse(ultRing.activeSelf);

                foreach (GameObject go in new[] { moveRoot, lvlRoot, ultRoot, ultRing })
                {
                    foreach (Graphic g in go.GetComponentsInChildren<Graphic>(true))
                    {
                        Assert.IsFalse(g.raycastTarget, $"{go.name}/{g.name} must not block raycasts");
                    }
                    foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Assert.IsNotNull(t.font);
                        Assert.AreNotEqual(liberation, t.font);
                    }
                }
            });

            WithScene(MainMenuPath, scene =>
            {
                MainMenuUI menu = FindInScene<MainMenuUI>(scene)[0];
                var glow = (GameObject)GetPrivate(menu, "ftueUpgradesGlow");
                var hintRoot = (GameObject)GetPrivate(menu, "ftueUpgradesHintRoot");
                var hintText = (TMP_Text)GetPrivate(menu, "ftueUpgradesHintText");

                Assert.IsNotNull(glow, "MainMenuUI.ftueUpgradesGlow must be wired");
                Assert.IsNotNull(hintRoot, "MainMenuUI.ftueUpgradesHintRoot must be wired");
                Assert.IsNotNull(hintText, "MainMenuUI.ftueUpgradesHintText must be wired");
                Assert.IsFalse(glow.activeSelf);
                Assert.IsFalse(hintRoot.activeSelf);
                Assert.AreNotEqual(liberation, hintText.font);
            });
        }
    }
}
