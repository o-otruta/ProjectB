using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Ads;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class Sprint2DepthAndUxTests
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
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
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
        public void AllElevenNewModifierCards_ExistHaveIconsAndWiredIntoParentAbilityCards()
        {
            var expected = new[]
            {
                ("OrbitalSword_SpeedAndRadius", "OrbitalSmallSwordCard", "orbital_small_sword", ModifierType.Speed, 40f, true, ModifierType.Radius, 0.6f),
                ("ShooterMod_Count", "ShooterCard", "Shooter", ModifierType.Count, 1f, false, default(ModifierType), 0f),
                ("ShooterMod_Cooldown", "ShooterCard", "Shooter", ModifierType.Cooldown, 0.18f, false, default(ModifierType), 0f),
                ("FireballMod_Count", "FireballCard", "Fireball", ModifierType.Count, 1f, false, default(ModifierType), 0f),
                ("FireballMod_Area", "FireballCard", "Fireball", ModifierType.Area, 1.2f, false, default(ModifierType), 0f),
                ("BoomerangMod_Count", "BoomerangCard", "Boomerang", ModifierType.Count, 1f, false, default(ModifierType), 0f),
                ("BoomerangMod_Pierce", "BoomerangCard", "Boomerang", ModifierType.PierceCount, 3f, true, ModifierType.Radius, 2f),
                ("IceAuraMod_Radius", "IceAuraCard", "IceAura", ModifierType.Radius, 1.0f, false, default(ModifierType), 0f),
                ("IceAuraMod_Slow", "IceAuraCard", "IceAura", ModifierType.SlowAmount, 0.15f, false, default(ModifierType), 0f),
                ("BlackHoleMod_Radius", "BlackHoleCard", "BlackHole", ModifierType.Radius, 1.2f, false, default(ModifierType), 0f),
                ("BlackHoleMod_Force", "BlackHoleCard", "BlackHole", ModifierType.Speed, 1.5f, false, default(ModifierType), 0f),
            };

            foreach (var spec in expected)
            {
                string modPath = $"Assets/_ScriptableObjects/Cards/{spec.Item1}.asset";
                var modCard = AssetDatabase.LoadAssetAtPath<AbilityModifierCardData>(modPath);
                Assert.IsNotNull(modCard, $"Missing modifier card asset: {modPath}");
                Assert.IsFalse(modCard.isConsumable, $"{spec.Item1} must not be consumable");
                Assert.IsNotNull(modCard.icon, $"{spec.Item1} must have a generated icon");
                Assert.AreEqual(spec.Item3, modCard.targetAbilityId, spec.Item1);
                Assert.AreEqual(spec.Item4, modCard.modifierType, spec.Item1);
                Assert.AreEqual(spec.Item5, modCard.value, 0.001f, spec.Item1);
                Assert.AreEqual(spec.Item6, modCard.hasSecondaryModifier, spec.Item1);
                if (spec.Item6)
                {
                    Assert.AreEqual(spec.Item7, modCard.secondaryModifierType, spec.Item1);
                    Assert.AreEqual(spec.Item8, modCard.secondaryValue, 0.001f, spec.Item1);
                }

                string parentPath = $"Assets/_ScriptableObjects/Cards/{spec.Item2}.asset";
                var parentCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>(parentPath);
                Assert.IsNotNull(parentCard, $"Missing parent AbilityCardData: {parentPath}");
                Assert.IsNotNull(parentCard.unlockedModifiers);
                Assert.Contains(modCard, parentCard.unlockedModifiers, $"{spec.Item1} must be in {spec.Item2}.unlockedModifiers");
            }
        }

        [Test]
        public void HeroAbilities_HasNoMaxLevelCap_AndDualModifierAppliesBothWithSingleLevelIncrement()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null);

            Assert.AreEqual(0, heroAbilities.GetAbilityLevel("orbital_small_sword"));

            var orbitalData = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            orbitalData.id = "orbital_small_sword";
            orbitalData.abilityName = "Orbital Sword";
            orbitalData.type = AbilityType.Active;
            orbitalData.count = 2;
            orbitalData.baseDamage = 15;
            orbitalData.orbitSpeed = 120f;
            orbitalData.orbitDistance = 2.5f;
            orbitalData.baseArea = 1f;
            orbitalData.prefab = Track(new GameObject("OrbitalDummyPrefab"));

            Assert.IsTrue(heroAbilities.AddAbility(orbitalData));
            Assert.AreEqual(1, heroAbilities.GetAbilityLevel("orbital_small_sword"));

            var dualOrbitalCard = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            dualOrbitalCard.targetAbilityId = "orbital_small_sword";
            dualOrbitalCard.modifierType = ModifierType.Speed;
            dualOrbitalCard.value = 40f;
            dualOrbitalCard.hasSecondaryModifier = true;
            dualOrbitalCard.secondaryModifierType = ModifierType.Radius;
            dualOrbitalCard.secondaryValue = 0.6f;

            var applier = new UpgradeApplier(null, stats, heroAbilities);
            applier.ApplyCard(dualOrbitalCard, null);

            Assert.AreEqual(2, heroAbilities.GetAbilityLevel("orbital_small_sword"), "Dual modifier must increment level exactly once");
            var orbitalComp = heroGo.GetComponentInChildren<OrbitalCompanionAbility>();
            Assert.IsNotNull(orbitalComp);
            float speedVal = (float)typeof(OrbitalCompanionAbility).GetField("currentSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(orbitalComp);
            float radiusVal = (float)typeof(OrbitalCompanionAbility).GetField("currentRadius", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(orbitalComp);
            Assert.AreEqual(160f, speedVal, 0.01f);
            Assert.AreEqual(3.1f, radiusVal, 0.01f);

            // Also verify Boomerang dual modifier (Pierce + Range)
            var boomerangData = Track(ScriptableObject.CreateInstance<BoomerangAbilityData>());
            boomerangData.id = "Boomerang";
            boomerangData.type = AbilityType.Active;
            boomerangData.baseDamage = 10;
            boomerangData.baseCooldown = 1f;
            boomerangData.throwRange = 8f;
            boomerangData.travelSpeed = 10f;
            boomerangData.projectileCount = 1;
            boomerangData.pierceCount = 5;

            Assert.IsTrue(heroAbilities.AddAbility(boomerangData));
            var dualBoomCard = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            dualBoomCard.targetAbilityId = "Boomerang";
            dualBoomCard.modifierType = ModifierType.PierceCount;
            dualBoomCard.value = 3f;
            dualBoomCard.hasSecondaryModifier = true;
            dualBoomCard.secondaryModifierType = ModifierType.Radius;
            dualBoomCard.secondaryValue = 2f;

            applier.ApplyCard(dualBoomCard, null);
            Assert.AreEqual(2, heroAbilities.GetAbilityLevel("Boomerang"));
            var boomComp = heroGo.GetComponentInChildren<BoomerangAbility>();
            int pierceVal = (int)typeof(ProjectileAbilityBase<BoomerangAbilityData>).GetField("currentPierceCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boomComp);
            float rangeVal = (float)typeof(ProjectileAbilityBase<BoomerangAbilityData>).GetField("currentRange", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boomComp);
            Assert.AreEqual(8, pierceVal);
            Assert.AreEqual(10f, rangeVal, 0.01f);

            // Verify NO maxLevel cap: upgrade orbital well past Lv 5 (to Lv 8) and ensure UpgradeManager STILL offers its modifier card
            for (int i = 0; i < 6; i++)
            {
                applier.ApplyCard(dualOrbitalCard, null);
            }
            Assert.AreEqual(8, heroAbilities.GetAbilityLevel("orbital_small_sword"));

            var mgrGo = Track(new GameObject("NoCapUpgradeManager"));
            var mgr = mgrGo.AddComponent<UpgradeManager>();
            mgr.cardPool = new List<CardData> { dualOrbitalCard };
            mgr.Construct(null, null, null, heroAbilities, applier, null);
            mgr.InitializeRunPool();

            var offered = mgr.GetRandomCards(3);
            Assert.AreEqual(1, offered.Count);
            Assert.AreSame(dualOrbitalCard, offered[0], "Abilities have no maxLevel cap so modifier cards stay eligible at Lv 8+");
        }

        [Test]
        public void CardSelectionUI_FormatsLevelBadges_EvaluatesBounceCurve_AndHandlesFreeAndAdReroll()
        {
            var stats = new HeroRuntimeStats();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, new GameEventBus(), null, null);

            var shooterData = Track(ScriptableObject.CreateInstance<ShooterAbilityData>());
            shooterData.id = "Shooter";
            shooterData.abilityName = "Shooter";
            shooterData.type = AbilityType.Active;

            var abilityCard = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            abilityCard.cardName = "Shooter";
            abilityCard.abilityData = shooterData;

            var passiveData = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            passiveData.id = "BattleFury";
            passiveData.abilityName = "Battle Fury";
            passiveData.type = AbilityType.Passive;

            var passiveAbilityCard = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            passiveAbilityCard.cardName = "Battle Fury";
            passiveAbilityCard.abilityData = passiveData;

            var modCard = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            modCard.cardName = "Shooter: +1 Projectile";
            modCard.targetAbilityId = "Shooter";
            modCard.modifierType = ModifierType.Count;
            modCard.value = 1f;

            var statCard = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            statCard.cardName = "Max HP";
            statCard.statType = StatType.MaxHP;
            statCard.value = 20f;

            Assert.AreEqual("NEW ACTIVE", CardSelectionUI.FormatLevelBadge(abilityCard, heroAbilities, out _));
            Assert.AreEqual("NEW PASSIVE", CardSelectionUI.FormatLevelBadge(passiveAbilityCard, heroAbilities, out _));
            Assert.AreEqual("STAT BOOST", CardSelectionUI.FormatLevelBadge(statCard, heroAbilities, out _));

            heroAbilities.AddAbility(shooterData);
            Assert.AreEqual("Lv 1 -> Lv 2", CardSelectionUI.FormatLevelBadge(modCard, heroAbilities, out _));
            for (int i = 0; i < 4; i++) heroAbilities.UpgradeAbility("Shooter", ModifierType.Damage, 5f);
            Assert.AreEqual("Lv 5 -> Lv 6", CardSelectionUI.FormatLevelBadge(modCard, heroAbilities, out _));

            // Bounce scale curve
            Assert.AreEqual(0f, CardSelectionUI.EvaluateBounceScale(0f), 0.001f);
            Assert.Greater(CardSelectionUI.EvaluateBounceScale(0.72f, 1.08f), 1.05f);
            Assert.AreEqual(1f, CardSelectionUI.EvaluateBounceScale(1f), 0.001f);

            // Reroll flow + UpgradeManager.GetRerolledCards non-overlap
            var c1 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c1.cardName = "C1";
            var c2 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c2.cardName = "C2";
            var c3 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c3.cardName = "C3";
            var c4 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c4.cardName = "C4";
            var c5 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c5.cardName = "C5";
            var c6 = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            c6.cardName = "C6";

            var saveManager = new SaveManager();
            try
            {
                var adConfig = Track(ScriptableObject.CreateInstance<AdConfig>());
                adConfig.maxRewardedPerSession = 5;
                adConfig.maxRewardedPerDay = 10;
                var adProvider = new MockAdProvider();
                var adManager = new AdManager(saveManager, adConfig, adProvider);

                var mgrGo = Track(new GameObject("RerollMgr"));
                var mgr = mgrGo.AddComponent<UpgradeManager>();
                mgr.cardPool = new List<CardData> { c1, c2, c3, c4, c5, c6 };
                mgr.Construct(null, null, null, heroAbilities, null, saveManager);
                mgr.InitializeRunPool();

                var initial = new List<CardData> { c1, c2, c3 };
                var rerolled = mgr.GetRerolledCards(initial, 3);
                Assert.AreEqual(3, rerolled.Count);
                foreach (var r in rerolled)
                {
                    Assert.IsFalse(initial.Contains(r), "Rerolled cards should avoid initial cards when alternatives exist");
                }

                // UI reroll: free reroll first, then ad reroll on 2nd offer
                var uiRoot = Track(new GameObject("SelectionUIRoot"));
                var panel = Track(new GameObject("Panel"));
                panel.transform.SetParent(uiRoot.transform, false);
                var slot1 = CreateCardSlot(panel.transform, "Card_1");
                var slot2 = CreateCardSlot(panel.transform, "Card_2");
                var slot3 = CreateCardSlot(panel.transform, "Card_3");
                var rerollBtnGo = Track(new GameObject("RerollBtn", typeof(RectTransform), typeof(Image), typeof(Button)));
                rerollBtnGo.transform.SetParent(panel.transform, false);
                var rerollTxtGo = Track(new GameObject("RerollTxt", typeof(RectTransform), typeof(TextMeshProUGUI)));
                rerollTxtGo.transform.SetParent(rerollBtnGo.transform, false);

                var selUi = uiRoot.AddComponent<CardSelectionUI>();
                SetPrivateField(selUi, "selectionPanel", panel);
                SetPrivateField(selUi, "cardSlots", new[] { slot1, slot2, slot3 });
                SetPrivateField(selUi, "rerollButton", rerollBtnGo.GetComponent<Button>());
                SetPrivateField(selUi, "rerollButtonText", rerollTxtGo.GetComponent<TextMeshProUGUI>());
                selUi.Construct(adManager, heroAbilities, null);

                int freeRemaining = 1;
                List<CardData> DoReroll(List<CardData> prev, bool usedFree)
                {
                    if (usedFree && freeRemaining > 0) freeRemaining--;
                    return mgr.GetRerolledCards(prev, 3);
                }

                // Offer 1: free reroll -> 0 ad calls, label becomes "Rerolled" (Sprint 13 labels), second click blocked
                selUi.ShowCards(initial, _ => { }, DoReroll, freeRemaining);
                Assert.IsTrue(selUi.CanRerollCurrentOffer);
                Assert.AreEqual("Reroll · Free ×1", selUi.RerollButtonLabel);
                Assert.AreEqual("STAT BOOST", slot1.LevelBadgeLabel);

                selUi.TriggerReroll();
                Assert.AreEqual(0, adProvider.RewardedShowCalls);
                Assert.AreEqual(0, freeRemaining);
                Assert.AreEqual("Rerolled", selUi.RerollButtonLabel);
                Assert.IsFalse(selUi.CanRerollCurrentOffer);

                // Offer 2: no free rerolls left -> uses rewarded ad
                selUi.ShowCards(initial, _ => { }, DoReroll, freeRemaining);
                Assert.IsTrue(selUi.CanRerollCurrentOffer);
                Assert.AreEqual("<sprite name=\"ad\"> Reroll", selUi.RerollButtonLabel);
                selUi.TriggerReroll();
                Assert.AreEqual(1, adProvider.RewardedShowCalls);
                Assert.AreEqual("Rerolled", selUi.RerollButtonLabel);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void GameManagerAndPauseMenuUI_PauseResumeAudioSlidersAndQuitWorkProperly()
        {
            var gmGo = Track(new GameObject("TestGameManager"));
            var gm = gmGo.AddComponent<GameManager>();
            var saveManager = new SaveManager();
            var audioManager = new AudioManager(saveManager, null);

            try
            {
                Assert.AreEqual(GameState.Playing, gm.CurrentState);
                Assert.IsTrue(gm.CanPause);

                Assert.IsTrue(gm.PauseGame());
                Assert.AreEqual(GameState.Paused, gm.CurrentState);
                Assert.IsTrue(gm.IsPaused);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
                Assert.IsFalse(gm.PauseForLevelUp(), "Cannot enter LevelUp while paused");

                Assert.IsTrue(gm.ResumeGame());
                Assert.AreEqual(GameState.Playing, gm.CurrentState);
                Assert.AreEqual(1f, Time.timeScale, 0.001f);

                // Pause during LevelUp should be blocked
                Assert.IsTrue(gm.PauseForLevelUp());
                Assert.IsFalse(gm.PauseGame());
                gm.ResumeFromLevelUp();

                // Test PauseMenuUI wiring & slider updates
                var pauseRoot = Track(new GameObject("PauseMenu", typeof(RectTransform)));
                var pausePanel = Track(new GameObject("PausePanel", typeof(RectTransform)));
                pausePanel.transform.SetParent(pauseRoot.transform, false);
                pausePanel.SetActive(false);

                var musicSlider = Track(new GameObject("MusicSlider", typeof(RectTransform), typeof(Slider))).GetComponent<Slider>();
                var sfxSlider = Track(new GameObject("SfxSlider", typeof(RectTransform), typeof(Slider))).GetComponent<Slider>();
                var musicValTxt = Track(new GameObject("MusicTxt", typeof(RectTransform), typeof(TextMeshProUGUI))).GetComponent<TextMeshProUGUI>();
                var sfxValTxt = Track(new GameObject("SfxTxt", typeof(RectTransform), typeof(TextMeshProUGUI))).GetComponent<TextMeshProUGUI>();

                var pauseUi = pauseRoot.AddComponent<PauseMenuUI>();
                SetPrivateField(pauseUi, "pausePanel", pausePanel);
                SetPrivateField(pauseUi, "musicSlider", musicSlider);
                SetPrivateField(pauseUi, "sfxSlider", sfxSlider);
                SetPrivateField(pauseUi, "musicValueText", musicValTxt);
                SetPrivateField(pauseUi, "sfxValueText", sfxValTxt);
                pauseUi.Construct(gm, audioManager);

                typeof(PauseMenuUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pauseUi, null);

                audioManager.SetMusicVolume(0.4f);
                audioManager.SetSfxVolume(0.75f);

                pauseUi.OnPauseClicked();
                Assert.IsTrue(pauseUi.IsPanelOpen);
                Assert.IsTrue(gm.IsPaused);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
                Assert.AreEqual(0.4f, musicSlider.value, 0.01f);
                Assert.AreEqual("40%", musicValTxt.text);
                Assert.AreEqual("75%", sfxValTxt.text);

                musicSlider.value = 0.6f;
                sfxSlider.value = 0.25f;
                Assert.AreEqual(0.6f, audioManager.MusicVolume, 0.01f);
                Assert.AreEqual(0.25f, audioManager.SfxVolume, 0.01f);
                Assert.AreEqual("60%", musicValTxt.text);
                Assert.AreEqual("25%", sfxValTxt.text);

                pauseUi.OnResumeClicked();
                Assert.IsFalse(pauseUi.IsPanelOpen);
                Assert.AreEqual(GameState.Playing, gm.CurrentState);
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
            }
            finally
            {
                audioManager.Dispose();
                saveManager.Dispose();
            }
        }

        [Test]
        public void GameplayScene_HasSprint2CardPolishAndPauseMenuWired()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                Canvas canvas = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    canvas = root.GetComponentInChildren<Canvas>(true);
                    if (canvas != null) break;
                }

                Assert.IsNotNull(canvas, "Canvas missing in Gameplay.unity");

                var selectionUi = canvas.GetComponent<CardSelectionUI>();
                Assert.IsNotNull(selectionUi, "CardSelectionUI missing on Canvas");
                Assert.IsNotNull(GetPrivateField(selectionUi, "rerollButton"), "CardSelectionUI.rerollButton must be wired");
                Assert.IsNotNull(GetPrivateField(selectionUi, "rerollButtonText"), "CardSelectionUI.rerollButtonText must be wired");

                var slots = canvas.GetComponentsInChildren<CardUI>(true);
                Assert.AreEqual(3, slots.Length, "Expected 3 CardUI slots under CardSelectionPanel");
                foreach (var slot in slots)
                {
                    Assert.IsNotNull(GetPrivateField(slot, "iconImage"), $"{slot.name}.iconImage must be wired");
                    Assert.IsNotNull(GetPrivateField(slot, "levelBadgeObject"), $"{slot.name}.levelBadgeObject must be wired");
                    Assert.IsNotNull(GetPrivateField(slot, "levelBadgeText"), $"{slot.name}.levelBadgeText must be wired");
                    Assert.IsNotNull(GetPrivateField(slot, "levelBadgeBg"), $"{slot.name}.levelBadgeBg must be wired");
                }

                Transform safeArea = canvas.transform.Find("SafeAreaHUD");
                Assert.IsNotNull(safeArea);
                Transform pauseBtnTf = safeArea.Find("PauseButton");
                Assert.IsNotNull(pauseBtnTf, "PauseButton missing under SafeAreaHUD");

                PauseMenuUI pauseMenu = canvas.GetComponentInChildren<PauseMenuUI>(true);
                Assert.IsNotNull(pauseMenu, "PauseMenuUI missing under Canvas");
                Assert.IsTrue(pauseMenu.gameObject.activeSelf, "PauseMenu root GameObject must stay active");

                var pausePanel = GetPrivateField(pauseMenu, "pausePanel") as GameObject;
                Assert.IsNotNull(pausePanel, "PauseMenuUI.pausePanel must be wired");
                Assert.IsFalse(pausePanel.activeSelf, "PausePanel child must start inactive");
                Assert.IsNotNull(GetPrivateField(pauseMenu, "pauseButton"), "PauseMenuUI.pauseButton must be wired");
                // Sprint 13: volume sliders moved to the shared SettingsPopupUI opened from Pause.
                Assert.IsNotNull(GetPrivateField(pauseMenu, "settingsButton"), "PauseMenuUI.settingsButton must be wired");
                Assert.IsNotNull(GetPrivateField(pauseMenu, "settingsPopup"), "PauseMenuUI.settingsPopup must be wired");
                Assert.IsNotNull(GetPrivateField(pauseMenu, "resumeButton"), "PauseMenuUI.resumeButton must be wired");
                Assert.IsNotNull(GetPrivateField(pauseMenu, "quitToMenuButton"), "PauseMenuUI.quitToMenuButton must be wired");

                var abilityHud = safeArea.GetComponentInChildren<AbilityHudUI>(true);
                Assert.IsNotNull(abilityHud, "AbilityHudUI missing under SafeAreaHUD");
                // Sprint 13: two compact rows (Active / Passive) stacked vertically under the top HUD block.
                Assert.IsNotNull(abilityHud.GetComponent<VerticalLayoutGroup>(), "AbilityHud must stack its rows vertically");
                Assert.IsNull(abilityHud.transform.Find("ActiveRow"), "ACT row label must be removed");
                Assert.IsNull(abilityHud.transform.Find("PassiveRow"), "PAS row label must be removed");
                Assert.IsNotNull(abilityHud.transform.Find("ActiveSlots"));
                Assert.IsNotNull(abilityHud.transform.Find("PassiveSlots"));
                Assert.IsNotNull(GetPrivateField(abilityHud, "activeSlotsContainer"), "AbilityHudUI.activeSlotsContainer must be wired");
                Assert.IsNotNull(GetPrivateField(abilityHud, "passiveSlotsContainer"), "AbilityHudUI.passiveSlotsContainer must be wired");
                // Ability rows live at the bottom-left so the top band stays free for boss HP and wave events.
                var abilityRect = (RectTransform)abilityHud.transform;
                Assert.AreEqual(Vector2.zero, abilityRect.anchorMin, "AbilityHud must be anchored to the bottom-left corner");
                Assert.AreEqual(Vector2.zero, abilityRect.anchorMax, "AbilityHud must be anchored to the bottom-left corner");
                var bossBar = safeArea.Find("BossHealthBar") as RectTransform;
                Assert.IsNotNull(bossBar);
                Assert.LessOrEqual(bossBar.anchoredPosition.y, -225f, "BossHealthBar must sit cleanly below the HP bar");
                var combo = safeArea.GetComponentInChildren<ComboHudUI>(true);
                Assert.IsNotNull(combo);
                Assert.IsNull(GetPrivateField(combo, "backgroundImage"), "Combo HUD is drawn without a background panel");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AbilityHudUI_SeparatesActivesAndPassivesIntoDedicatedRowsWithEmptySockets()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null);

            var passive1 = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            passive1.id = "BattleFury";
            passive1.abilityName = "Battle Fury";
            passive1.type = AbilityType.Passive;
            passive1.effectType = PassiveEffectType.DamagePercent;
            passive1.baseValue = 15f;

            var active1 = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            active1.id = "orbital_small_sword";
            active1.abilityName = "Orbital Sword";
            active1.type = AbilityType.Active;
            active1.count = 1;
            active1.baseDamage = 10;
            active1.orbitSpeed = 100f;
            active1.orbitDistance = 2f;
            active1.baseArea = 1f;
            active1.prefab = Track(new GameObject("OrbitalDummy"));

            var passive2 = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            passive2.id = "IronSkin";
            passive2.abilityName = "Iron Skin";
            passive2.type = AbilityType.Passive;
            passive2.effectType = PassiveEffectType.ArmorFlat;
            passive2.baseValue = 20f;

            var active2 = Track(ScriptableObject.CreateInstance<ShooterAbilityData>());
            active2.id = "Shooter";
            active2.abilityName = "Shooter";
            active2.type = AbilityType.Active;
            active2.baseDamage = 10;
            active2.baseCooldown = 1f;
            active2.projectileCount = 1;
            active2.projectileSpeed = 12f;
            active2.searchRadius = 10f;

            // Build AbilityHudUI with dedicated Active and Passive row containers
            var prefabGo = Track(new GameObject("SlotPrefab", typeof(RectTransform), typeof(Image), typeof(AbilityHudSlotUI)));
            var slotPrefab = prefabGo.GetComponent<AbilityHudSlotUI>();
            var bgGo = Track(new GameObject("Bg", typeof(RectTransform), typeof(Image)));
            bgGo.transform.SetParent(prefabGo.transform, false);
            var lvlBadgeGo = Track(new GameObject("LevelBadge", typeof(RectTransform), typeof(Image)));
            lvlBadgeGo.transform.SetParent(prefabGo.transform, false);
            var lvlTmp = Track(new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI)));
            lvlTmp.transform.SetParent(lvlBadgeGo.transform, false);
            SetPrivateField(slotPrefab, "borderImage", prefabGo.GetComponent<Image>());
            SetPrivateField(slotPrefab, "backgroundImage", bgGo.GetComponent<Image>());
            SetPrivateField(slotPrefab, "levelText", lvlTmp.GetComponent<TextMeshProUGUI>());

            var hudGo = Track(new GameObject("AbilityHud", typeof(RectTransform), typeof(AbilityHudUI)));
            var activeRow = Track(new GameObject("ActiveSlots", typeof(RectTransform)));
            activeRow.transform.SetParent(hudGo.transform, false);
            var passiveRow = Track(new GameObject("PassiveSlots", typeof(RectTransform)));
            passiveRow.transform.SetParent(hudGo.transform, false);

            var hud = hudGo.GetComponent<AbilityHudUI>();
            SetPrivateField(hud, "activeSlotsContainer", activeRow.transform);
            SetPrivateField(hud, "passiveSlotsContainer", passiveRow.transform);
            SetPrivateField(hud, "slotPrefab", slotPrefab);
            hud.Construct(heroAbilities);

            // Pick in interleaved order: Passive1 -> Active1 -> Passive2 -> Active2
            Assert.IsTrue(heroAbilities.AddAbility(passive1));
            Assert.IsTrue(heroAbilities.AddAbility(active1));
            Assert.IsTrue(heroAbilities.AddAbility(passive2));
            Assert.IsTrue(heroAbilities.AddAbility(active2));

            var shooterContainer = GameObject.Find("Projectiles_Shooter");
            if (shooterContainer != null) Track(shooterContainer);

            hud.Refresh();

            Assert.AreEqual(HeroAbilities.MAX_ACTIVE_SLOTS, hud.ActiveSlots.Count);
            Assert.AreEqual(HeroAbilities.MAX_PASSIVE_SLOTS, hud.PassiveSlots.Count);

            // Active row has Active1, Active2, 1 empty unlocked socket (slot 2), and 3 locked sockets (slots 3..5)
            Assert.AreSame(active1, hud.ActiveSlots[0].CurrentData);
            Assert.AreSame(active2, hud.ActiveSlots[1].CurrentData);
            Assert.IsTrue(hud.ActiveSlots[2].IsEmpty, "Active slot 2 should be empty and unlocked");
            Assert.IsFalse(hud.ActiveSlots[2].IsLocked, "Active slot 2 should not be locked");
            Assert.IsTrue(hud.ActiveSlots[2].gameObject.activeSelf);
            Assert.IsFalse(hud.ActiveSlots[2].transform.Find("LevelBadge").gameObject.activeSelf);
            for (int i = 3; i < HeroAbilities.MAX_ACTIVE_SLOTS; i++)
            {
                Assert.IsTrue(hud.ActiveSlots[i].IsLocked, $"Active slot {i} should be locked by default");
                Assert.IsFalse(hud.ActiveSlots[i].IsEmpty, $"Locked active slot {i} must not report IsEmpty");
                Assert.IsTrue(hud.ActiveSlots[i].gameObject.activeSelf, $"Locked socket {i} must stay visible");
                Assert.IsFalse(hud.ActiveSlots[i].transform.Find("LevelBadge").gameObject.activeSelf);
            }

            // Passive row has Passive1, Passive2, 1 empty unlocked socket (slot 2), and 3 locked sockets (slots 3..5)
            Assert.AreSame(passive1, hud.PassiveSlots[0].CurrentData);
            Assert.AreSame(passive2, hud.PassiveSlots[1].CurrentData);
            Assert.IsTrue(hud.PassiveSlots[2].IsEmpty, "Passive slot 2 should be empty and unlocked");
            Assert.IsFalse(hud.PassiveSlots[2].IsLocked, "Passive slot 2 should not be locked");
            Assert.IsTrue(hud.PassiveSlots[2].gameObject.activeSelf);
            Assert.IsFalse(hud.PassiveSlots[2].transform.Find("LevelBadge").gameObject.activeSelf);
            for (int i = 3; i < HeroAbilities.MAX_PASSIVE_SLOTS; i++)
            {
                Assert.IsTrue(hud.PassiveSlots[i].IsLocked, $"Passive slot {i} should be locked by default");
                Assert.IsFalse(hud.PassiveSlots[i].IsEmpty, $"Locked passive slot {i} must not report IsEmpty");
                Assert.IsTrue(hud.PassiveSlots[i].gameObject.activeSelf, $"Locked passive socket {i} must stay visible");
                Assert.IsFalse(hud.PassiveSlots[i].transform.Find("LevelBadge").gameObject.activeSelf);
            }

            // Upgrading updates levels in place within dedicated rows without shifting slots
            heroAbilities.UpgradeAbility("Shooter", ModifierType.Damage, 5f);
            heroAbilities.UpgradeAbility("BattleFury", ModifierType.Damage, 10f);
            hud.Refresh();

            Assert.AreSame(active1, hud.ActiveSlots[0].CurrentData);
            Assert.AreSame(active2, hud.ActiveSlots[1].CurrentData);
            Assert.AreSame(passive1, hud.PassiveSlots[0].CurrentData);
            Assert.AreSame(passive2, hud.PassiveSlots[1].CurrentData);
            Assert.AreEqual("Lv2", ((TextMeshProUGUI)GetPrivateField(hud.ActiveSlots[1], "levelText")).text);
            Assert.AreEqual("Lv2", ((TextMeshProUGUI)GetPrivateField(hud.PassiveSlots[0], "levelText")).text);
        }

        private static CardUI CreateCardSlot(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var badge = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(go.transform, false);
            var badgeTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTxt.transform.SetParent(badge.transform, false);

            var cardUi = go.AddComponent<CardUI>();
            SetPrivateField(cardUi, "bgImage", go.GetComponent<Image>());
            SetPrivateField(cardUi, "levelBadgeObject", badge);
            SetPrivateField(cardUi, "levelBadgeBg", badge.GetComponent<Image>());
            SetPrivateField(cardUi, "levelBadgeText", badgeTxt.GetComponent<TextMeshProUGUI>());
            return cardUi;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}