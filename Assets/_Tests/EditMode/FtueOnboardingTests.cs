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
using UnityEngine.TestTools;
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
            foreach (string key in new[]
            {
                LocalizationService.KEY_FTUE_MOVE_HINT_KEYBOARD, LocalizationService.KEY_FTUE_ULTIMATE_HINT_KEYBOARD,
                LocalizationService.KEY_FTUE_SHARD_HINT, LocalizationService.KEY_FTUE_UPGRADE_BUY_HINT,
                LocalizationService.KEY_FTUE_CONSTELLATIONS_HINT, LocalizationService.KEY_FTUE_PLAY_TAB_HINT
            })
            {
                Assert.IsTrue(loc.HasKey(key), key);
            }

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
            Assert.IsTrue(lvlRoot.TryGetComponent(out FtueHintBubble lvlBubble), "LevelUp hint uses the shared bubble look");
            Assert.AreEqual(FtuePointerSide.Down, lvlBubble.PointerSide, "LevelUp hint points down at the first card");
            Assert.GreaterOrEqual(lvlTxt.fontSize, 44f);

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
        public void MainMenuUI_FtueMenuFlow_GrantsStarterCoins_ThenPointsUpgrades_Constellations_Play_AndStart()
        {
            SaveManager sm = CreateSaveManager();
            var upgradeManager = new MetaUpgradeManager(sm);
            var bonusManager = new DailyBonusManager(sm);
            var loc = new LocalizationService();

            int cheapestCost = upgradeManager.GetCheapestAvailableUpgradeCost();
            Assert.Greater(cheapestCost, 0, "MetaUpgrades in Resources must have positive upgrade costs");
            string cheapestId = null;
            foreach (MetaUpgradeData data in upgradeManager.GetAllUpgrades())
            {
                if (data != null && upgradeManager.GetUpgradeCost(data.id) == cheapestCost)
                {
                    cheapestId = data.id;
                    break;
                }
            }
            Assert.IsNotNull(cheapestId);

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

            // Before the first run: no onboarding, no free coins.
            Assert.IsFalse(menu.BeginFtueMenuFlow());
            menu.OnPlayClicked();
            Assert.AreEqual(MenuFtueStep.None, menu.FtueStep);
            Assert.AreEqual(0, sm.Data.coins);
            Assert.IsFalse(glow.activeSelf);
            Assert.IsFalse(hintRoot.activeSelf);

            // First death with too few coins -> topped up exactly to the cheapest upgrade, only once.
            sm.RecordRunResult(new RunResult { waveReached = 3, coinsEarned = 10 });
            Assert.IsTrue(menu.BeginFtueMenuFlow());
            Assert.AreEqual(cheapestCost, sm.Data.coins);
            Assert.IsTrue(sm.Data.ftueStarterCoinsGranted);
            Assert.IsTrue(menu.BeginFtueMenuFlow());
            Assert.AreEqual(cheapestCost, sm.Data.coins, "Starter coins are granted only once");

            // Step 1: pointer on the Upgrades tab (glow + bubble + badge dot).
            menu.OnPlayClicked();
            Assert.AreEqual(MenuFtueStep.OpenUpgrades, menu.FtueStep);
            Assert.AreSame(tabs[MainMenuUI.TAB_UPGRADES].transform, menu.FtueSpotlight.Target);
            Assert.IsTrue(glow.activeSelf);
            Assert.IsTrue(hintRoot.activeSelf);
            Assert.IsTrue(badges[MainMenuUI.TAB_UPGRADES].IsVisible);
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_UPGRADES_HINT), hintTxt.text);

            // Step 2: inside Upgrades the pointer moves to the buy button.
            menu.OnUpgradesClicked();
            Assert.AreEqual(MenuFtueStep.BuyUpgrade, menu.FtueStep);

            // Purchase without shards -> straight to the Play tab.
            Assert.IsTrue(upgradeManager.TryBuyUpgrade(cheapestId));
            Assert.AreEqual(MenuFtueStep.OpenPlayTab, menu.FtueStep);
            Assert.AreSame(tabs[MainMenuUI.TAB_PLAY].transform, menu.FtueSpotlight.Target);
            Assert.IsFalse(badges[MainMenuUI.TAB_UPGRADES].IsVisible);
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_PLAY_TAB_HINT), hintTxt.text);

            // Owning Boss Shards inserts the Constellations step; opening the tab dismisses it for good.
            sm.AddBossShards(1, saveImmediately: false);
            Assert.AreEqual(MenuFtueStep.OpenConstellations, menu.FtueStep);
            Assert.AreSame(tabs[MainMenuUI.TAB_CONSTELLATIONS].transform, menu.FtueSpotlight.Target);
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_CONSTELLATIONS_HINT), hintTxt.text);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ConstellationTreeUI reference is missing"));
            menu.OnConstellationClicked();
            Assert.IsTrue(sm.Data.ftueConstellationsHintDismissed);
            Assert.AreEqual(MenuFtueStep.OpenPlayTab, menu.FtueStep);

            // Play tab -> pointer on START; starting the run completes the chain.
            menu.OnPlayClicked();
            Assert.AreEqual(MenuFtueStep.PressStart, menu.FtueStep);
            sm.MarkFtueMenuFlowCompleted(saveImmediately: false);
            menu.RefreshFtueGuide();
            Assert.AreEqual(MenuFtueStep.None, menu.FtueStep);
            Assert.IsFalse(glow.activeSelf);
            Assert.IsFalse(hintRoot.activeSelf);
        }

        [Test]
        public void MenuFtueFlow_FirstShardLater_PointsConstellationsOnce_AndSkipsBuyStepWhenUpgradesOwned()
        {
            SaveManager sm = CreateSaveManager();
            sm.RecordRunResult(new RunResult { waveReached = 3 });
            sm.Data.ftueMenuFlowCompleted = true;

            Assert.AreEqual(MenuFtueStep.None, MenuFtueFlow.Evaluate(sm, MainMenuUI.TAB_PLAY, canAffordAnyUpgrade: true));

            sm.AddBossShards(1, saveImmediately: false);
            Assert.AreEqual(MenuFtueStep.OpenConstellations, MenuFtueFlow.Evaluate(sm, MainMenuUI.TAB_PLAY, true));
            Assert.AreEqual(MenuFtueStep.None, MenuFtueFlow.Evaluate(sm, MainMenuUI.TAB_CONSTELLATIONS, true));
            sm.MarkFtueConstellationsHintDismissed(saveImmediately: false);
            Assert.AreEqual(MenuFtueStep.None, MenuFtueFlow.Evaluate(sm, MainMenuUI.TAB_PLAY, true));

            // A pending chain with an upgrade already bought skips the buy step.
            SaveManager sm2 = CreateSaveManager();
            sm2.ReplaceDataInMemory(new SaveData { totalRuns = 1 }, saveToActiveStorage: false);
            sm2.SetMetaUpgradeLevel("hero_hp", 1, saveImmediately: false);
            Assert.AreEqual(MenuFtueStep.OpenPlayTab, MenuFtueFlow.Evaluate(sm2, MainMenuUI.TAB_UPGRADES, true));
            Assert.AreEqual(MenuFtueStep.PressStart, MenuFtueFlow.Evaluate(sm2, MainMenuUI.TAB_PLAY, true));
            Assert.AreEqual(0, sm2.GrantFtueStarterCoins(0, saveImmediately: false));
        }

        [Test]
        public void BootRouter_FirstLaunchGoesToGameplay_ThenMainMenu_AndBootIsFirstBuildScene()
        {
            SaveManager sm = CreateSaveManager();
            Assert.AreEqual(BootRouter.GAMEPLAY_SCENE, BootRouter.ResolveStartScene(sm));
            sm.RecordRunResult(new RunResult { waveReached = 2 });
            Assert.AreEqual(BootRouter.MAIN_MENU_SCENE, BootRouter.ResolveStartScene(sm));
            Assert.AreEqual(BootRouter.MAIN_MENU_SCENE, BootRouter.ResolveStartScene(null));

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 3);
            Assert.AreEqual("Assets/_Scenes/Boot.unity", scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);

            WithScene("Assets/_Scenes/Boot.unity", scene =>
            {
                Assert.AreEqual(1, FindInScene<BootRouter>(scene).Count, "Boot.unity must contain one BootRouter");
            });
        }

        [Test]
        public void GameplayFtueUI_FirstShardEver_ShowsConstellationsHintOnce_EvenOutsideFirstRun()
        {
            SaveManager sm = CreateSaveManager();
            sm.RecordRunResult(new RunResult { waveReached = 3 });
            var loc = new LocalizationService();
            var bus = new GameEventBus();

            var ftueGo = Track(new GameObject("GameplayFtueShard", typeof(RectTransform), typeof(GameplayFtueUI)));
            var moveRoot = new GameObject("MoveHint", typeof(RectTransform), typeof(CanvasGroup));
            moveRoot.transform.SetParent(ftueGo.transform, false);
            var moveTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            moveTxt.transform.SetParent(moveRoot.transform, false);

            var ftue = ftueGo.GetComponent<GameplayFtueUI>();
            SetPrivate(ftue, "moveHintRoot", moveRoot);
            SetPrivate(ftue, "moveHintGroup", moveRoot.GetComponent<CanvasGroup>());
            SetPrivate(ftue, "moveHintText", moveTxt);
            ftue.Construct(sm, loc, eventBus: bus);
            ftue.InitializeFtue();

            Assert.IsFalse(ftue.IsFtueActive);
            Assert.IsFalse(moveRoot.activeSelf, "Move hint stays hidden outside the first run");
            Assert.IsTrue(ftue.IsShardHintPending);

            bus.Publish(new BossShardsGainedEvent(Vector3.zero, 1, 1));
            Assert.IsTrue(ftue.IsShardHintVisible);
            Assert.IsFalse(ftue.IsShardHintPending);
            Assert.IsTrue(sm.Data.ftueShardHintShown);
            Assert.IsNotNull(ftue.ShardHintText);
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_SHARD_HINT), ftue.ShardHintText.text);

            // A player who already earned shards never sees it.
            SaveManager veteran = CreateSaveManager();
            veteran.ReplaceDataInMemory(new SaveData { totalRuns = 4, totalBossShardsEarned = 3 }, saveToActiveStorage: false);
            var ftueGo2 = Track(new GameObject("GameplayFtueShard2", typeof(RectTransform), typeof(GameplayFtueUI)));
            var ftue2 = ftueGo2.GetComponent<GameplayFtueUI>();
            ftue2.Construct(veteran, loc, eventBus: bus);
            ftue2.InitializeFtue();
            Assert.IsFalse(ftue2.IsShardHintPending);
        }

        [Test]
        public void GameplayFtueUI_KeyboardMode_UsesKeyboardMoveWording_AndSwitchesBackOnTouch()
        {
            SaveManager sm = CreateSaveManager();
            var loc = new LocalizationService();
            var inputMode = new InputModeService(InputMode.KeyboardMouse, listenToInputSystem: false);

            var ftueGo = Track(new GameObject("GameplayFtueKeyboard", typeof(RectTransform), typeof(GameplayFtueUI)));
            var moveRoot = new GameObject("MoveHint", typeof(RectTransform), typeof(CanvasGroup));
            moveRoot.transform.SetParent(ftueGo.transform, false);
            var moveTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            moveTxt.transform.SetParent(moveRoot.transform, false);

            var ftue = ftueGo.GetComponent<GameplayFtueUI>();
            SetPrivate(ftue, "moveHintRoot", moveRoot);
            SetPrivate(ftue, "moveHintText", moveTxt);
            ftue.Construct(sm, loc, inputModeService: inputMode);
            ftue.InitializeFtue();

            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT_KEYBOARD), moveTxt.text);
            Assert.IsTrue(inputMode.RegisterActivity(InputMode.Touch, 100.0));
            Assert.AreEqual(loc.Get(LocalizationService.KEY_FTUE_MOVE_HINT), moveTxt.text);
        }

        [Test]
        public void FtueHintBubble_UsesLargeText_FitsPillToText_AndAimsPointer()
        {
            var parentGo = Track(new GameObject("BubbleParent", typeof(RectTransform)));
            ((RectTransform)parentGo.transform).sizeDelta = new Vector2(1080f, 1920f);
            var root = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parentGo.transform, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(380f, 68f);
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMax = 26f;
            label.text = "Spend Boss Shards on Constellation stars";

            FtueHintBubble bubble = FtueHintBubble.Ensure(root, label);
            Assert.IsNotNull(bubble);
            Assert.IsFalse(label.enableAutoSizing);
            Assert.GreaterOrEqual(label.fontSize, 44f, "Onboarding text must be large");
            Assert.Greater(rect.sizeDelta.x, 380f, "Pill grows to fit the text");
            Assert.LessOrEqual(rect.sizeDelta.x, 1080f);
            Assert.Greater(rect.sizeDelta.y, label.fontSize, "Pill is taller than one text line");
            Assert.AreSame(bubble, FtueHintBubble.Ensure(root, label), "Ensure is idempotent");

            Assert.IsNotNull(bubble.Pointer);
            Assert.IsFalse(bubble.Pointer.gameObject.activeSelf, "No pointer until a side is set");
            bubble.SetPointer(FtuePointerSide.Down);
            Assert.IsTrue(bubble.Pointer.gameObject.activeSelf);
            Assert.Less(bubble.Pointer.anchoredPosition.y, 0f, "Down pointer sits on the bottom edge");
            Assert.AreEqual(0f, bubble.Pointer.anchoredPosition.x, 0.01f);
            Assert.IsFalse(bubble.Pointer.GetComponent<Image>().raycastTarget);

            bubble.SetPointerSlide(80f);
            bubble.SetPointer(FtuePointerSide.Down);
            Assert.AreEqual(80f, bubble.Pointer.anchoredPosition.x, 0.01f, "Pointer slides towards an off-centre target");

            bubble.SetPointer(FtuePointerSide.Left);
            Assert.Less(bubble.Pointer.anchoredPosition.x, 0f);
            bubble.SetPointer(FtuePointerSide.None);
            Assert.IsFalse(bubble.Pointer.gameObject.activeSelf);
        }

        [Test]
        public void GameOverUI_FirstRun_HidesRestart_AndPulsesMenu_LaterRunsKeepRestart()
        {
            MethodInfo apply = typeof(GameOverUI).GetMethod("ApplyFirstRunNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(apply);

            GameOverUI Build(SaveManager save, out Button restart, out Button menuBtn)
            {
                var go = Track(new GameObject("GameOverFtue", typeof(RectTransform), typeof(GameOverUI)));
                restart = new GameObject("Restart", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                restart.transform.SetParent(go.transform, false);
                menuBtn = new GameObject("Menu", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                menuBtn.transform.SetParent(go.transform, false);
                var ui = go.GetComponent<GameOverUI>();
                SetPrivate(ui, "restartButton", restart);
                SetPrivate(ui, "menuButton", menuBtn);
                ui.Construct(null, null, saveManager: save);
                return ui;
            }

            SaveManager first = CreateSaveManager();
            GameOverUI firstUi = Build(first, out Button firstRestart, out Button firstMenu);
            var menuLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            menuLabel.transform.SetParent(firstMenu.transform, false);
            menuLabel.text = "MENU";
            Assert.IsTrue(firstUi.IsFirstRunGameOver);
            apply.Invoke(firstUi, null);
            Assert.IsFalse(firstRestart.gameObject.activeSelf);
            Assert.IsTrue(firstMenu.TryGetComponent(out PulseUI pulse) && pulse.enabled);

            // Auto return to the menu after 3 s with a countdown on the Menu button.
            Assert.IsTrue(firstUi.IsAutoMenuPending);
            Assert.AreEqual("MENU (3)", menuLabel.text);
            Assert.IsFalse(firstUi.TickAutoMenu(1.2f));
            Assert.AreEqual("MENU (2)", menuLabel.text);

            // Pressing any button (here: revive) cancels it and restores the label.
            firstUi.OnReviveClicked();
            Assert.IsFalse(firstUi.IsAutoMenuPending);
            Assert.AreEqual("MENU", menuLabel.text);
            Assert.IsFalse(firstUi.TickAutoMenu(5f));

            // Untouched, it fires once the delay runs out.
            apply.Invoke(firstUi, null);
            Assert.IsFalse(firstUi.TickAutoMenu(2.5f));
            Assert.IsTrue(firstUi.TickAutoMenu(0.6f));
            Assert.IsFalse(firstUi.IsAutoMenuPending);
            Assert.AreEqual("MENU", menuLabel.text);

            SaveManager later = CreateSaveManager();
            later.ReplaceDataInMemory(new SaveData { totalRuns = 2 }, saveToActiveStorage: false);
            GameOverUI laterUi = Build(later, out Button laterRestart, out _);
            Assert.IsFalse(laterUi.IsFirstRunGameOver);
            apply.Invoke(laterUi, null);
            Assert.IsTrue(laterRestart.gameObject.activeSelf);
            Assert.IsFalse(laterUi.IsAutoMenuPending, "Later runs keep the normal Game Over without auto exit");
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
