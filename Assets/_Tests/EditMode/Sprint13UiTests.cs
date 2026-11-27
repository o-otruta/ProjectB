using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Platform;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Sprint 13 — UI/UX redesign (AUDIT 13.7): design system (UiTheme, Cyrillic fonts, kit sprites, inline icons),
    /// scene hygiene (no LiberationSans / builtin UISprite), HUD modal rule, menu preview camera, locked HUD slots,
    /// LevelUp stack ↔ row, Zero-GC HUD updates and the formatting helpers behind the new screens.
    /// </summary>
    public class Sprint13UiTests
    {
        private const string MainMenuPath = "Assets/_Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/_Scenes/Gameplay.unity";
        private const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        private static UiTheme LoadTheme()
        {
            var theme = Resources.Load<UiTheme>(UiTheme.RESOURCE_PATH);
            Assert.IsNotNull(theme, "Resources/UiTheme.asset must exist");
            return theme;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            f.SetValue(target, value);
        }

        private static object GetPrivate(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
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

        private static IEnumerable<GameObject> LoadUiPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs/UI" }))
            {
                yield return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static bool IsBuiltinUiSprite(Sprite sprite)
        {
            return sprite != null
                   && AssetDatabase.GetAssetPath(sprite) == "Resources/unity_builtin_extra"
                   && (sprite.name == "UISprite" || sprite.name == "Background" || sprite.name == "Knob" || sprite.name == "InputFieldBackground");
        }

        // ───────────────────────── 13.1 Design system ─────────────────────────

        [Test]
        public void UiTheme_Exists_WithRarityColors_ButtonStyles_Typography_AndKit()
        {
            UiTheme theme = LoadTheme();

            var rarities = new[] { theme.rarityCommon, theme.rarityRare, theme.rarityEpic, theme.rarityLegendary };
            for (int i = 0; i < rarities.Length; i++)
            {
                Assert.Greater(rarities[i].a, 0.99f, $"rarity {i} must be opaque");
                for (int j = i + 1; j < rarities.Length; j++) Assert.AreNotEqual(rarities[i], rarities[j], "rarity colours must differ");
            }
            Assert.AreEqual(theme.rarityLegendary, theme.GetRarityColor(CardRarity.Legendary));

            foreach (ButtonStyle style in Enum.GetValues(typeof(ButtonStyle)))
            {
                ButtonStyleColors colors = theme.GetButtonStyle(style);
                Assert.AreNotEqual(Color.magenta, theme.GetColor(colors.face), $"{style} face colour unresolved");
                Assert.AreNotEqual(Color.magenta, theme.GetColor(colors.label), $"{style} label colour unresolved");
            }
            foreach (UiColor token in Enum.GetValues(typeof(UiColor)))
            {
                Assert.AreNotEqual(Color.magenta, theme.GetColor(token), $"token {token} unresolved");
            }

            Assert.AreEqual(96f, theme.displaySize);
            Assert.AreEqual(64f, theme.h1Size);
            Assert.AreEqual(44f, theme.h2Size);
            Assert.AreEqual(32f, theme.bodySize);
            Assert.AreEqual(24f, theme.captionSize);
            Assert.AreEqual(8f, theme.gridUnit);
            Assert.AreEqual(0.12f, theme.durationFast, 1e-4f);
            Assert.AreEqual(0.2f, theme.durationNormal, 1e-4f);
            Assert.AreEqual(0.35f, theme.durationSlow, 1e-4f);

            foreach (UiSprite s in Enum.GetValues(typeof(UiSprite)))
            {
                if (s == UiSprite.None) continue;
                Assert.IsNotNull(theme.GetSprite(s), $"kit sprite {s} missing");
            }
            Assert.Greater(theme.panelSprite.border.x, 0f, "panel must be 9-slice");
            Assert.Greater(theme.buttonSprite.border.y, theme.buttonSprite.border.w, "button bottom border includes the toy 'step'");

            var icons = new[] { theme.iconCoin, theme.iconShard, theme.iconAd, theme.iconGear, theme.iconPause, theme.iconLock,
                theme.iconCheck, theme.iconChevronLeft, theme.iconChevronRight };
            foreach (Sprite icon in icons) Assert.IsNotNull(icon, "currency / action icon missing");

            Assert.IsNotNull(theme.titleMaterial);
            Assert.IsNotNull(theme.numberMaterial);
            Assert.IsNotNull(theme.bodyMaterial);
            Assert.IsTrue(theme.titleMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), "Title preset has an outline");
            Assert.IsTrue(theme.numberMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), "Number preset has a thick outline");
            Assert.IsFalse(theme.bodyMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), "Body preset has no effects");
        }

        [Test]
        public void ThemeFonts_AreStaticAtlases_WithEnglishAndRussianGlyphs_AndLiberationOnlyAsFallback()
        {
            UiTheme theme = LoadTheme();
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
            var fonts = new[] { theme.displayFont, theme.bodyFont, theme.bodyBoldFont };
            const string english = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789%+×•";
            const string russian = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя№";

            foreach (TMP_FontAsset font in fonts)
            {
                Assert.IsNotNull(font, "theme font missing");
                Assert.AreNotEqual(liberation, font, "LiberationSans must not be a theme font");
                Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, $"{font.name} must use a static atlas");
                Assert.IsTrue(font.HasCharacters(english, out List<char> missingEn), $"{font.name} missing EN: {string.Concat(missingEn ?? new List<char>())}");
                Assert.IsTrue(font.HasCharacters(russian, out List<char> missingRu), $"{font.name} missing RU: {string.Concat(missingRu ?? new List<char>())}");
                Assert.IsTrue(font.fallbackFontAssetTable.Contains(liberation), $"{font.name} keeps LiberationSans as fallback");
            }

            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            Assert.IsNotNull(settings);
            Assert.AreEqual(theme.bodyFont, TMP_Settings.defaultFontAsset, "theme body font is the TMP default");
        }

        [Test]
        public void InlineIconSpriteAsset_ContainsCurrencyAndActionIcons()
        {
            UiTheme theme = LoadTheme();
            Assert.IsNotNull(theme.iconSpriteAsset, "TMP sprite asset for <sprite name=coin> missing");
            foreach (string name in new[] { "coin", "shard", "ad", "lock", "check" })
            {
                Assert.GreaterOrEqual(theme.iconSpriteAsset.GetSpriteIndexFromName(name), 0, $"<sprite name={name}> missing");
            }
        }

        [Test]
        public void KitSprites_ArePackedInOneSpriteAtlas()
        {
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath("Assets/_UI/Kit/UiKit.spriteatlasv2"), "UI kit SpriteAtlas missing");
            Assert.AreEqual(SpritePackerMode.SpriteAtlasV2, EditorSettings.spritePackerMode);
        }

        // ───────────────────────── Scene hygiene ─────────────────────────

        [Test]
        public void ScenesAndUiPrefabs_DoNotUseLiberationSansAsPrimaryFont()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
            var offenders = new List<string>();

            foreach (string path in new[] { MainMenuPath, GameplayPath })
            {
                WithScene(path, scene =>
                {
                    foreach (TMP_Text text in FindInScene<TMP_Text>(scene))
                    {
                        if (text.font == null || text.font == liberation) offenders.Add($"{scene.name}/{text.name}");
                    }
                });
            }
            foreach (GameObject prefab in LoadUiPrefabs())
            {
                foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == null || text.font == liberation) offenders.Add($"{prefab.name}/{text.name}");
                }
            }

            Assert.IsEmpty(offenders, "Texts still on LiberationSans: " + string.Join(", ", offenders));
        }

        [Test]
        public void Scenes_DoNotUseBuiltinUISprite()
        {
            var offenders = new List<string>();
            foreach (string path in new[] { MainMenuPath, GameplayPath })
            {
                WithScene(path, scene =>
                {
                    foreach (Image image in FindInScene<Image>(scene))
                    {
                        if (IsBuiltinUiSprite(image.sprite)) offenders.Add($"{scene.name}/{image.name}");
                    }
                });
            }
            Assert.IsEmpty(offenders, "Images still on builtin UI sprites: " + string.Join(", ", offenders));
        }

        [Test]
        public void MainMenuTabs_UseRedrawnIcons_AndNumericBadges()
        {
            UiTheme theme = LoadTheme();
            WithScene(MainMenuPath, scene =>
            {
                TabBarUI tabBar = FindInScene<TabBarUI>(scene).Find(t => t != null);
                Assert.IsNotNull(tabBar, "MainMenu needs a TabBarUI");
                Assert.AreEqual(5, tabBar.TabCount);

                var expected = new[] { theme.tabUpgrades, theme.tabAbilities, theme.tabPlay, theme.tabAchievements, theme.tabBonus };
                var tabs = (BottomTabUI[])GetPrivate(tabBar, "tabs");
                for (int i = 0; i < tabs.Length; i++)
                {
                    Assert.AreEqual(expected[i], tabs[i].iconImage.sprite, $"tab {i} must use the redrawn kit icon");
                    Assert.IsNotNull(tabBar.GetBadge(i), $"tab {i} needs a BadgeUI");
                }
            });
        }

        // ───────────────────────── 13.2 Menu ─────────────────────────

        [Test]
        public void MainMenu_HeroPreviewCamera_IsOffOnEveryTabExceptPlay()
        {
            var camGo = Track(new GameObject("PreviewCam", typeof(Camera)));
            var cam = camGo.GetComponent<Camera>();
            cam.cullingMask = 1 << 3 | 1;
            var hero = Track(new GameObject("Hero"));
            var menuGo = Track(new GameObject("Menu", typeof(RectTransform)));
            var menu = menuGo.AddComponent<MainMenuUI>();
            SetPrivate(menu, "previewCamera", cam);
            SetPrivate(menu, "heroPreviewRoot", hero);
            typeof(MainMenuUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
            int originalMask = cam.cullingMask;

            menu.OnUpgradesClicked();
            Assert.IsFalse(menu.IsHeroPreviewActive);
            Assert.AreEqual(0, cam.cullingMask, "camera renders nothing behind opaque screens");
            Assert.IsFalse(hero.activeSelf, "rotating preview hero is deactivated");

            menu.OnAchievementsClicked();
            Assert.IsFalse(menu.IsHeroPreviewActive);
            menu.OnBonusClicked();
            Assert.IsFalse(menu.IsHeroPreviewActive);
            menu.OnAbilitiesClicked();
            Assert.IsFalse(menu.IsHeroPreviewActive);

            menu.OnPlayClicked();
            Assert.IsTrue(menu.IsHeroPreviewActive);
            Assert.AreEqual(originalMask, cam.cullingMask);
            Assert.IsTrue(hero.activeSelf);

            WithScene(MainMenuPath, scene =>
            {
                MainMenuUI sceneMenu = FindInScene<MainMenuUI>(scene)[0];
                Assert.IsNotNull(GetPrivate(sceneMenu, "previewCamera"), "MainMenuUI.previewCamera must be wired");
                Assert.IsNotNull(GetPrivate(sceneMenu, "heroPreviewRoot"), "MainMenuUI.heroPreviewRoot must be wired");
                Assert.IsNotNull(GetPrivate(sceneMenu, "coinsLabel"), "header coin pill");
                Assert.IsNotNull(GetPrivate(sceneMenu, "shardsLabel"), "header Boss Shard pill (shards visible everywhere)");

                // Every non-Play screen is opaque.
                foreach (string name in new[] { "UpgradesPanel", "AbilitiesPanel", "AchievementsPanel", "DailyBonusScreenPanel" })
                {
                    GameObject panel = null;
                    foreach (RectTransform rt in FindInScene<RectTransform>(scene)) if (rt.name == name) panel = rt.gameObject;
                    Assert.IsNotNull(panel, name);
                    var img = panel.GetComponent<Image>();
                    Assert.IsNotNull(img, $"{name} needs an opaque background");
                    Assert.GreaterOrEqual(img.color.a, 0.99f, $"{name} must be opaque");
                }
            });
        }

        [Test]
        public void PlayScreen_Landscape_PutsHeroLeft_AndStatsRight()
        {
            // Camera math: the focus projects onto the requested viewport point.
            var camGo = Track(new GameObject("Cam", typeof(Camera)));
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.aspect = 16f / 9f;
            var focus = new Vector3(0f, 1.5f, -5f);
            camGo.transform.position = MenuPreviewFraming.CalculateCameraPosition(focus, Quaternion.identity, 60f, cam.aspect, new Vector2(0.25f, 0.45f), 6.6f);
            Vector3 vp = cam.WorldToViewportPoint(focus);
            Assert.AreEqual(0.25f, vp.x, 1e-3f, "landscape: hero in the centre of the left half");
            Assert.AreEqual(0.45f, vp.y, 1e-3f);
            Assert.AreEqual(6.6f, vp.z, 1e-3f);

            WithScene(MainMenuPath, scene =>
            {
                MenuPreviewFraming framing = FindInScene<MenuPreviewFraming>(scene).Find(f => f != null);
                Assert.IsNotNull(framing, "main camera re-frames the hero per layout mode");

                HeroSelectionUI play = FindInScene<HeroSelectionUI>(scene)[0];
                RectPreset Land(string child) => play.transform.Find(child).GetComponent<ResponsiveRect>().GetPreset(LayoutMode.Landscape);

                // UI redesign: the ability tiles (+ detail panel) live in AbilityBlock.
                RectPreset card = Land("AbilityBlock");
                Assert.IsTrue(card.captured);
                Assert.GreaterOrEqual(card.anchorMin.x, 0.5f, "ability tiles live in the right half");
                Assert.GreaterOrEqual(Land("StartButton").anchorMin.x, 0.5f, "START in the right half");
                Assert.LessOrEqual(Land("NamePlate").anchorMax.x, 0.5f, "name plate above the hero (left half)");
                Assert.LessOrEqual(Land("NextHeroButton").anchorMax.x, 0.5f, "chevrons frame the hero in the left half");
            });
        }

        // ───────────────────────── 13.3 HUD ─────────────────────────

        [Test]
        public void HudVisibility_HidesHudInPausedLevelUpAndGameOver()
        {
            Assert.IsTrue(HudVisibilityController.ShouldShowHud(GameState.Playing));
            Assert.IsFalse(HudVisibilityController.ShouldShowHud(GameState.Paused));
            Assert.IsFalse(HudVisibilityController.ShouldShowHud(GameState.LevelUp));
            Assert.IsFalse(HudVisibilityController.ShouldShowHud(GameState.GameOver));

            var go = Track(new GameObject("Hud", typeof(RectTransform), typeof(CanvasGroup)));
            var group = go.GetComponent<CanvasGroup>();
            var controller = go.AddComponent<HudVisibilityController>();
            controller.EditorSetup(new[] { group });

            foreach (GameState state in new[] { GameState.Paused, GameState.LevelUp, GameState.GameOver })
            {
                controller.ForceState(state);
                Assert.AreEqual(0f, group.alpha, $"HUD alpha in {state}");
                Assert.IsFalse(group.blocksRaycasts, $"HUD must not catch taps in {state}");
            }
            controller.ForceState(GameState.Playing);
            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.blocksRaycasts);

            WithScene(GameplayPath, scene =>
            {
                HudVisibilityController sceneController = FindInScene<HudVisibilityController>(scene)[0];
                var groups = (CanvasGroup[])GetPrivate(sceneController, "groups");
                var names = new List<string>();
                foreach (CanvasGroup g in groups) names.Add(g.name);
                CollectionAssert.Contains(names, "SafeAreaHUD", "bars, slots, combo, compass, ultimate");
                CollectionAssert.Contains(names, "JoystickBg", "joystick");

                // Toasts / dev menu live outside the faded HUD.
                Transform hud = groups[0].transform;
                Assert.IsNull(hud.Find("AchievementPopup"), "achievement toast must not fade with the HUD");
            });
        }

        [Test]
        public void AbilityHudUI_HidesLockedSlots_AndCollapsesThemIntoALockChip()
        {
            var stats = new HeroRuntimeStats();
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, null, null);

            var passive = Track(ScriptableObject.CreateInstance<StatPassiveAbilityData>());
            passive.id = "IronSkin";
            passive.abilityName = "Iron Skin";
            passive.type = AbilityType.Passive;
            passive.effectType = PassiveEffectType.ArmorFlat;
            passive.baseValue = 20f;
            Assert.IsTrue(heroAbilities.AddAbility(passive));

            var prefabGo = Track(new GameObject("SlotPrefab", typeof(RectTransform), typeof(Image), typeof(AbilityHudSlotUI)));
            var slotPrefab = prefabGo.GetComponent<AbilityHudSlotUI>();
            SetPrivate(slotPrefab, "borderImage", prefabGo.GetComponent<Image>());

            var hudGo = Track(new GameObject("AbilityHud", typeof(RectTransform), typeof(AbilityHudUI)));
            var activeRow = new GameObject("ActiveSlots", typeof(RectTransform));
            activeRow.transform.SetParent(hudGo.transform, false);
            var passiveRow = new GameObject("PassiveSlots", typeof(RectTransform));
            passiveRow.transform.SetParent(hudGo.transform, false);
            var chip = new GameObject("LockedChip", typeof(RectTransform));
            chip.transform.SetParent(passiveRow.transform, false);
            var chipText = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            chipText.transform.SetParent(chip.transform, false);

            var hud = hudGo.GetComponent<AbilityHudUI>();
            SetPrivate(hud, "activeSlotsContainer", activeRow.transform);
            SetPrivate(hud, "passiveSlotsContainer", passiveRow.transform);
            SetPrivate(hud, "slotPrefab", slotPrefab);
            SetPrivate(hud, "hideLockedSlots", true);
            SetPrivate(hud, "passiveLockedChip", chip);
            SetPrivate(hud, "passiveLockedChipText", chipText.GetComponent<TextMeshProUGUI>());
            hud.Construct(heroAbilities);
            hud.Refresh();

            int unlocked = heroAbilities.UnlockedPassiveSlots;
            Assert.Greater(HeroAbilities.MAX_PASSIVE_SLOTS, unlocked, "test needs locked passive slots");
            for (int i = 0; i < hud.PassiveSlots.Count; i++)
            {
                AbilityHudSlotUI slot = hud.PassiveSlots[i];
                if (slot.IsLocked) Assert.IsFalse(slot.gameObject.activeSelf, $"locked slot {i} must be hidden");
                else Assert.IsTrue(slot.gameObject.activeSelf, $"open slot {i} stays visible");
            }
            Assert.AreSame(passive, hud.PassiveSlots[0].CurrentData);
            Assert.IsTrue(chip.activeSelf, "locked slots collapse into one chip");
            Assert.AreEqual($"+{HeroAbilities.MAX_PASSIVE_SLOTS - unlocked}", chipText.GetComponent<TextMeshProUGUI>().text);
            Assert.AreEqual(chip.transform.parent.childCount - 1, chip.transform.GetSiblingIndex(), "chip sits after the slots");

            WithScene(GameplayPath, scene =>
            {
                AbilityHudUI sceneHud = FindInScene<AbilityHudUI>(scene)[0];
                Assert.IsTrue(sceneHud.HidesLockedSlots, "Gameplay HUD must hide locked slots");
            });
        }

        [Test]
        public void HudSlot_OverdriveTiers_ColorTheFrame()
        {
            UiTheme theme = LoadTheme();
            var go = Track(new GameObject("Slot", typeof(RectTransform), typeof(AbilityHudSlotUI)));
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(go.transform, false);
            var slot = go.GetComponent<AbilityHudSlotUI>();
            SetPrivate(slot, "borderImage", border.GetComponent<Image>());

            var data = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            data.type = AbilityType.Active;
            data.rarity = CardRarity.Rare;

            slot.SetAbility(data, 3);
            Assert.AreEqual(theme.rarityRare, border.GetComponent<Image>().color, "below Lv 10 the frame shows rarity");
            slot.SetAbility(data, 10);
            Assert.AreEqual(1, slot.OverdriveTier);
            Assert.AreEqual(theme.overdriveTier1, border.GetComponent<Image>().color, "Lv 10 — golden frame");
            slot.SetAbility(data, 20);
            Assert.AreEqual(2, slot.OverdriveTier);
            Assert.AreEqual(theme.overdriveTier2, border.GetComponent<Image>().color);
            slot.SetAbility(data, 35);
            Assert.AreEqual(3, slot.OverdriveTier);
        }

        // ───────────────────────── 13.4 Modals ─────────────────────────

        [Test]
        public void LevelUp_StacksCardsInPortrait_AndRowsThemInLandscape()
        {
            WithScene(GameplayPath, scene =>
            {
                CardSelectionUI selection = FindInScene<CardSelectionUI>(scene)[0];
                var cards = (CardUI[])GetPrivate(selection, "cardSlots");
                Assert.AreEqual(3, cards.Length, "3 free cards");
                Transform container = cards[0].transform.parent;
                Assert.AreSame(container, cards[2].transform.parent, "all 3 cards live in the same stack");

                var stack = container.GetComponent<AdaptiveLayoutGroup>();
                Assert.IsNotNull(stack, "cards container uses AdaptiveLayoutGroup");
                var presets = container.GetComponent<ResponsiveRect>();
                Assert.IsNotNull(presets);
                Assert.IsTrue(presets.HasPreset(LayoutMode.Portrait) && presets.HasPreset(LayoutMode.Landscape));

                try
                {
                    stack.ApplyLayoutMode(LayoutMode.Portrait);
                    presets.ApplyLayoutMode(LayoutMode.Portrait);
                    Assert.IsTrue(stack.IsVertical, "portrait: vertical stack");
                    Vector2 portraitSize = ((RectTransform)container).sizeDelta;
                    Assert.Greater(portraitSize.y, portraitSize.x * 0.6f, "portrait stack is tall");

                    stack.ApplyLayoutMode(LayoutMode.Landscape);
                    presets.ApplyLayoutMode(LayoutMode.Landscape);
                    Assert.IsFalse(stack.IsVertical, "landscape: row of cards");
                    Vector2 landscapeSize = ((RectTransform)container).sizeDelta;
                    Assert.Greater(landscapeSize.x, landscapeSize.y * 2f, "landscape row is wide");

                    var titlePresets = cards[0].transform.Find("Title").GetComponent<ResponsiveRect>();
                    Assert.IsNotNull(titlePresets, "card children carry two layouts");
                }
                finally
                {
                    stack.ApplyLayoutMode(LayoutMode.Portrait);
                    presets.ApplyLayoutMode(LayoutMode.Portrait);
                }

                Assert.IsNotNull(GetPrivate(selection, "titleText"), "LEVEL UP! title");
                Assert.IsNotNull(GetPrivate(cards[0], "frameImage"), "rarity lives in the frame");
                Assert.AreEqual(3, selection.FreeSlotsCount, "all 3 LevelUp cards are free");
                Assert.IsNull(GetPrivate(cards[2], "lockOverlay"), "3rd card is free and has no lock overlay");
                Assert.IsNull(GetPrivate(cards[2], "adBadgeObject"), "3rd card is free and has no ad badge");
            });
        }

        [Test]
        public void CardUI_TintsFrameNotBody_HidesDuplicateBadges_AndTagsCategories()
        {
            UiTheme theme = LoadTheme();
            var go = Track(new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardUI)));
            var card = go.GetComponent<CardUI>();
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(go.transform, false);
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(go.transform, false);
            var badgeText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeText.transform.SetParent(badge.transform, false);
            var tag = new GameObject("Tag", typeof(RectTransform), typeof(TextMeshProUGUI));
            tag.transform.SetParent(go.transform, false);

            SetPrivate(card, "bgImage", go.GetComponent<Image>());
            SetPrivate(card, "frameImage", frame.GetComponent<Image>());
            SetPrivate(card, "levelBadgeObject", badge);
            SetPrivate(card, "levelBadgeText", badgeText.GetComponent<TextMeshProUGUI>());
            SetPrivate(card, "tagText", tag.GetComponent<TextMeshProUGUI>());

            var data = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            data.cardName = "Max HP";
            data.rarity = CardRarity.Epic;

            card.Setup(data, _ => { }, false, true, "STAT BOOST");
            Assert.AreEqual(theme.rarityEpic, frame.GetComponent<Image>().color, "rarity colour goes to the frame");
            Assert.AreEqual(theme.surfaceRaised, go.GetComponent<Image>().color, "card body stays neutral");
            Assert.IsFalse(badge.activeSelf, "category badge is replaced by the tag");
            Assert.AreEqual("STAT", tag.GetComponent<TextMeshProUGUI>().text);

            card.Setup(data, _ => { }, false, true, "Lv 2 -> Lv 3");
            Assert.IsTrue(badge.activeSelf, "level progress stays visible");
            Assert.AreEqual("Lv 2 → Lv 3", badgeText.GetComponent<TextMeshProUGUI>().text);
        }

        [Test]
        public void GameplayModals_AreWired_ForPauseSummaryBuildAndGameOver()
        {
            WithScene(GameplayPath, scene =>
            {
                PauseMenuUI pause = FindInScene<PauseMenuUI>(scene)[0];
                foreach (string field in new[] { "waveValueText", "timeValueText", "killsValueText", "coinsValueText", "buildContainer", "buildSlotPrefab", "settingsPopup" })
                {
                    Assert.IsNotNull(GetPrivate(pause, field), $"PauseMenuUI.{field}");
                }
                Assert.IsNull(GetPrivate(pause, "musicSlider"), "Pause no longer duplicates the volume sliders");

                GameOverUI gameOver = FindInScene<GameOverUI>(scene)[0];
                foreach (string field in new[] { "titleText", "newBestBadge", "coinsLabel", "shardsLabel", "breakdownLayout", "killsValueText", "timeValueText" })
                {
                    Assert.IsNotNull(GetPrivate(gameOver, field), $"GameOverUI.{field}");
                }

                foreach (SettingsPopupUI settings in FindInScene<SettingsPopupUI>(scene))
                {
                    Assert.IsNotNull(GetPrivate(settings, "languageRow"), "Settings reserves the language row (Sprint 12.3)");
                }
            });
        }

        [Test]
        public void GameOver_BreakdownHeight_GrowsWithRowsUpToTheScrollMaximum()
        {
            Assert.AreEqual(0f, GameOverUI.CalculateBreakdownHeight(0, 72f, 430f));
            float one = GameOverUI.CalculateBreakdownHeight(1, 72f, 430f);
            float three = GameOverUI.CalculateBreakdownHeight(3, 72f, 430f);
            Assert.Greater(one, 72f);
            Assert.Greater(three, one);
            Assert.AreEqual(430f, GameOverUI.CalculateBreakdownHeight(12, 72f, 430f), "after the maximum the list scrolls");
        }

        [Test]
        public void GameOverPanel_ClearsToySurfaceBottomLedges_AndEnablesRewardPulse()
        {
            WithScene(GameplayPath, scene =>
            {
                GameOverUI gameOver = FindInScene<GameOverUI>(scene)[0];
                Transform panel = gameOver.transform;

                foreach (string chipName in new[] { "KillsChip", "TimeChip", "ComboChip" })
                {
                    Transform chip = panel.Find($"Content/Layout/Columns/LeftColumn/StatChips/{chipName}");
                    Assert.IsNotNull(chip, chipName);
                    var vlg = chip.GetComponent<VerticalLayoutGroup>();
                    var le = chip.GetComponent<LayoutElement>();
                    Assert.GreaterOrEqual(vlg.padding.bottom, 34, $"{chipName} padding must clear kit_card 33px bottom ledge");
                    Assert.GreaterOrEqual(le.preferredHeight, 166f, $"{chipName} needs height for padded content");
                }

                Transform newBest = panel.Find("Content/Layout/BestRow/NewBestBadge");
                Assert.IsNotNull(newBest);
                Assert.GreaterOrEqual(newBest.GetComponent<HorizontalLayoutGroup>().padding.bottom, 20, "NewBestBadge clears kit_pill bottom ledge");

                foreach (string pillName in new[] { "CoinsReward", "ShardsReward" })
                {
                    Transform pill = panel.Find($"Content/Layout/Columns/LeftColumn/Rewards/{pillName}");
                    Assert.IsNotNull(pill, pillName);
                    Assert.GreaterOrEqual(pill.GetComponent<HorizontalLayoutGroup>().padding.bottom, 20, $"{pillName} clears kit_pill bottom ledge");
                    Assert.LessOrEqual(pill.Find("Icon").GetComponent<LayoutElement>().preferredHeight, 56f, $"{pillName} icon fits inside pill face");
                    var cl = pill.GetComponent<CurrencyLabel>();
                    Assert.IsNotNull(cl);
                    Assert.IsTrue((bool)GetPrivate(cl, "pulseWhileCounting"), $"{pillName} pulses while counting up");
                }

                Transform dmgScroll = panel.Find("Content/Layout/Columns/RightColumn/DamageBreakdownSection/ScrollView");
                Transform achScroll = panel.Find("Content/Layout/Columns/LeftColumn/RunAchievementsSection/ScrollView");
                Assert.IsNotNull(dmgScroll);
                Assert.IsNotNull(achScroll);
                Assert.GreaterOrEqual(((RectTransform)dmgScroll).offsetMin.y, 33f, "DamageBreakdown ScrollView clears kit_card bottom ledge");
                Assert.GreaterOrEqual(((RectTransform)achScroll).offsetMin.y, 33f, "RunAchievements ScrollView clears kit_card bottom ledge");

                foreach (string relPath in new[]
                {
                    "Content/Layout/AdButtons/ReviveButton/Text",
                    "Content/Layout/AdButtons/DoubleCoinsButton/Text",
                    "Content/Layout/NavButtons/MenuButton/Text",
                    "Content/Layout/NavButtons/RestartButton/Content"
                })
                {
                    var rt = panel.Find(relPath) as RectTransform;
                    Assert.IsNotNull(rt, relPath);
                    Assert.GreaterOrEqual(rt.offsetMin.y, 36f, $"{relPath} clears kit_button 38px bottom ledge");
                    Assert.LessOrEqual(rt.offsetMax.y, -6f, $"{relPath} clears kit_button top border");
                }
            });
        }

        // ───────────────────────── 13.6 Zero-GC ─────────────────────────

        [Test]
        public void XpBar_SplitLabel_IsDarkOverTheFill_AndLightOverTheEmptyPart()
        {
            var barGo = Track(new GameObject("Xp", typeof(RectTransform)));
            ((RectTransform)barGo.transform).sizeDelta = new Vector2(208f, 38f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barGo.transform, false);
            var normalMask = new GameObject("LabelMask", typeof(RectTransform), typeof(RectMask2D));
            normalMask.transform.SetParent(barGo.transform, false);
            var label = new GameObject("Level", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(normalMask.transform, false);
            var inverseMask = new GameObject("InvertedMask", typeof(RectTransform), typeof(RectMask2D));
            inverseMask.transform.SetParent(barGo.transform, false);
            var inverted = new GameObject("LevelInverted", typeof(RectTransform), typeof(TextMeshProUGUI));
            inverted.transform.SetParent(inverseMask.transform, false);

            var bar = barGo.AddComponent<ProgressBarUI>();
            bar.EditorSetup(fill.GetComponent<Image>(), null, label.GetComponent<TextMeshProUGUI>(), ProgressLabelFormat.None, UiColor.XpFill);
            bar.EditorSetupSplitLabel((RectTransform)barGo.transform, normalMask.GetComponent<RectMask2D>(),
                inverted.GetComponent<TextMeshProUGUI>(), inverseMask.GetComponent<RectMask2D>(), 4f);

            bar.SetNormalized(0.5f, instant: true);
            // Fill edge = inset + 0.5 * (208 - 2 * 4) = 104 px from the left.
            Assert.AreEqual(104f, inverseMask.GetComponent<RectMask2D>().padding.z, 0.01f, "dark copy clipped at the fill edge");
            Assert.AreEqual(104f, normalMask.GetComponent<RectMask2D>().padding.x, 0.01f, "light copy starts at the fill edge");

            bar.SetNormalized(0f, instant: true);
            Assert.AreEqual(204f, inverseMask.GetComponent<RectMask2D>().padding.z, 0.01f, "empty bar: no dark text");

            var chars = "LV 7".ToCharArray();
            bar.SetLabel(chars, chars.Length);
            Assert.AreEqual("LV 7", label.GetComponent<TextMeshProUGUI>().text);
            Assert.AreEqual("LV 7", inverted.GetComponent<TextMeshProUGUI>().text, "both copies show the same text");
            Assert.AreEqual(LoadTheme().background, inverted.GetComponent<TextMeshProUGUI>().color, "inverted copy is dark");
        }

        [Test]
        public void HudUpdates_HpXpCoins_AreZeroGc()
        {
            var barGo = Track(new GameObject("Bar", typeof(RectTransform)));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barGo.transform, false);
            var ghost = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
            ghost.transform.SetParent(barGo.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(barGo.transform, false);
            var bar = barGo.AddComponent<ProgressBarUI>();
            bar.EditorSetup(fill.GetComponent<Image>(), ghost.GetComponent<Image>(), label.GetComponent<TextMeshProUGUI>(), ProgressLabelFormat.Ratio, UiColor.HpFill);

            var coinGo = Track(new GameObject("Coins", typeof(RectTransform)));
            var coinText = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
            coinText.transform.SetParent(coinGo.transform, false);
            var coins = coinGo.AddComponent<CurrencyLabel>();
            coins.EditorSetup(CurrencyType.Coins, coinText.GetComponent<TextMeshProUGUI>(), null);

            var buffer = new char[32];

            // Warm-up (TMP grows its internal buffers once).
            for (int i = 0; i < 5; i++)
            {
                bar.SetValue(200 - i, 200);
                coins.SetValue(1000 + i, animate: false);
                UiFormat.WriteGrouped(buffer, 0, 14702 + i);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                bar.SetValue(180 - (i % 90), 200);
                coins.SetValue(2000 + i * 7, animate: false);
                UiFormat.WriteGrouped(buffer, 0, 10000 + i);
                UiFormat.WriteClock(buffer, 0, i);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "HP / XP / coin HUD updates must not allocate");
            Assert.AreEqual(161f / 200f, bar.Normalized, 1e-4f, "last SetValue(161, 200) drives the fill");
        }

        // ───────────────────────── Formatting helpers ─────────────────────────

        [Test]
        public void Formatting_GroupsThousands_ClocksDeltasCyclesAndWeapons()
        {
            var buffer = new char[32];
            int len = UiFormat.WriteGrouped(buffer, 0, 14702);
            Assert.AreEqual("14 702", new string(buffer, 0, len));
            Assert.AreEqual("1 000 000", UiFormat.Grouped(1000000));
            Assert.AreEqual("0", UiFormat.Grouped(0));
            len = UiFormat.WriteClock(buffer, 0, 221);
            Assert.AreEqual("03:41", new string(buffer, 0, len));

            Assert.AreEqual("+24% → +32%", UpgradeItemUI.FormatDelta(MetaUpgradeEffectType.HeroDamage, 24f, 32f, false));
            Assert.AreEqual("+0.3×", UpgradeItemUI.FormatDelta(MetaUpgradeEffectType.CritMultiplier, 0.3f, 0.4f, true));
            Assert.AreEqual("+1 Lv → +2 Lv", UpgradeItemUI.FormatDelta(MetaUpgradeEffectType.StartLevel, 1f, 2f, false));

            Assert.AreEqual("Cycle 2 · ×1.2", DailyBonusScreenUI.FormatCycle(1, 1.2f, 5));
            Assert.AreEqual("Cycle 1 · ×1", DailyBonusScreenUI.FormatCycle(0, 1.2f, 5));
            Assert.AreEqual(3600, DailyBonusScreenUI.SecondsUntilNextReward(new DateTime(2026, 11, 23, 23, 0, 0, DateTimeKind.Utc)));

            HeroSelectionUI.SplitPassiveDescription("+100% Max HP\n<b>Weapon:</b> Steady Bolt (10 dmg, 2/s, 8m range)", out string passive, out string weapon);
            Assert.AreEqual("+100% Max HP", passive);
            HeroSelectionUI.SplitWeapon(weapon, out string weaponName, out string details);
            Assert.AreEqual("Steady Bolt", weaponName);
            Assert.AreEqual("10 dmg, 2/s, 8m range", details);
        }

        [Test]
        public void Achievements_SortClosestToCompletionFirst_CompletedLast()
        {
            AchievementData Make(string id, int target)
            {
                var a = Track(ScriptableObject.CreateInstance<AchievementData>());
                a.id = id;
                a.targetValue = target;
                return a;
            }

            var done = Make("done", 5);
            var almost = Make("almost", 10);
            var started = Make("started", 10);
            var fresh = Make("fresh", 10);
            var data = new SaveData();
            data.completedAchievements.Add("done");
            data.achievementProgress["almost"] = 9;
            data.achievementProgress["started"] = 2;

            var list = new List<AchievementData> { done, fresh, started, almost };
            AchievementScreenUI.SortByCompletion(list, data);

            CollectionAssert.AreEqual(new[] { almost, started, fresh, done }, list);
        }

        [Test]
        public void UiTween_RunsOnUnscaledTime_AndSnapsOutsidePlayMode()
        {
            var go = Track(new GameObject("Tweened", typeof(RectTransform), typeof(CanvasGroup)));
            var group = go.GetComponent<CanvasGroup>();
            UiTween.Fade(group, 0f, 0.5f, 0.2f);
            Assert.AreEqual(0.5f, group.alpha, 1e-4f, "edit mode: tweens snap to their end value");
            UiTween.Scale(go.transform, Vector3.zero, Vector3.one * 2f, 0.3f);
            Assert.AreEqual(Vector3.one * 2f, go.transform.localScale);

            Assert.AreEqual(1f, UiTween.Evaluate(UiEase.OutBack, 1f), 1e-4f);
            Assert.AreEqual(0f, UiTween.Evaluate(UiEase.OutCubic, 0f), 1e-4f);
            Assert.Greater(UiTween.Evaluate(UiEase.OutBack, 0.7f), 1f, "OutBack overshoots");
        }

        [Test]
        public void UiTween_CountPulseScale_StartsAndEndsAtOne_AndPulsesUpward()
        {
            Assert.AreEqual(1f, UiTween.EvaluateCountPulseScale(0f, 0.16f, 3f), 1e-4f);
            Assert.AreEqual(1f, UiTween.EvaluateCountPulseScale(1f, 0.16f, 3f), 1e-4f);
            Assert.AreEqual(1.16f, UiTween.EvaluateCountPulseScale(0.5f, 0.16f, 3f), 1e-3f);
            Assert.AreEqual(1f, UiTween.EvaluateCountPulseScale(0.5f, 0f, 3f), 1e-4f);

            for (int i = 0; i <= 20; i++)
            {
                float s = UiTween.EvaluateCountPulseScale(i / 20f, 0.15f, 3f);
                Assert.GreaterOrEqual(s, 1f - 1e-5f, $"pulse never shrinks below 1 at step {i}");
            }
        }

        [Test]
        public void GameOverUI_CountUp_CountsFromZero_PulsesAndLandsAtTarget_ZeroGc()
        {
            var root = Track(new GameObject("GameOverRoot", typeof(RectTransform), typeof(GameOverUI)));
            var ui = root.GetComponent<GameOverUI>();
            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var wave = new GameObject("Wave", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var kills = new GameObject("Kills", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var time = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var combo = new GameObject("Combo", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            foreach (var t in new[] { title, wave, kills, time, combo }) t.transform.SetParent(root.transform, false);

            SetPrivate(ui, "panel", root);
            SetPrivate(ui, "titleText", title);
            SetPrivate(ui, "waveText", wave);
            SetPrivate(ui, "killsValueText", kills);
            SetPrivate(ui, "timeValueText", time);
            SetPrivate(ui, "comboValueText", combo);

            var stats = new RunStatistics();
            for (int i = 0; i < 1234; i++) stats.AddKill();
            stats.UpdatePlayTime(125f);
            stats.UpdateBestCombo(18);
            SetPrivate(ui, "runStatistics", stats);

            // EditMode Show() snaps immediately to final values.
            ui.Show(12, 500);
            Assert.IsFalse(ui.IsCounting, "EditMode Show snaps without leaving active counters");
            Assert.AreEqual("1\u00A0234", kills.text);
            Assert.AreEqual("02:05", time.text);
            Assert.AreEqual("x18", combo.text);
            Assert.AreEqual(Vector3.one, kills.transform.localScale);

            // Deterministic animated count-up stepping + Zero-GC check.
            MethodInfo startMethod = typeof(GameOverUI).GetMethod("StartCounter", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo tickMethod = typeof(GameOverUI).GetMethod("TickCounters", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(startMethod);
            Assert.IsNotNull(tickMethod);
            Type formatType = typeof(GameOverUI).GetNestedType("StatFormat", BindingFlags.NonPublic);
            var tick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), ui, tickMethod);

            // Warm-up TMP char buffers once.
            startMethod.Invoke(ui, new object[] { 0, wave, Enum.ToObject(formatType, 3), 12, 0f, 0.5f, 0.12f, "WAVE ", "", true });
            startMethod.Invoke(ui, new object[] { 1, kills, Enum.ToObject(formatType, 0), 1234, 0f, 0.5f, 0.15f, null, null, true });
            startMethod.Invoke(ui, new object[] { 2, time, Enum.ToObject(formatType, 1), 125, 0f, 0.5f, 0.15f, null, null, true });
            startMethod.Invoke(ui, new object[] { 3, combo, Enum.ToObject(formatType, 2), 18, 0f, 0.5f, 0.15f, null, null, true });
            Assert.IsTrue(ui.IsCounting);
            Assert.AreEqual("WAVE 0", wave.text);
            Assert.AreEqual("0", kills.text);
            Assert.AreEqual("00:00", time.text);
            Assert.AreEqual("x0", combo.text);

            tick(0.01f);
            // Re-start cleanly after TMP warm-up.
            startMethod.Invoke(ui, new object[] { 0, wave, Enum.ToObject(formatType, 3), 12, 0f, 0.5f, 0.12f, "WAVE ", "", true });
            startMethod.Invoke(ui, new object[] { 1, kills, Enum.ToObject(formatType, 0), 1234, 0f, 0.5f, 0.15f, null, null, true });
            startMethod.Invoke(ui, new object[] { 2, time, Enum.ToObject(formatType, 1), 125, 0f, 0.5f, 0.15f, null, null, true });
            startMethod.Invoke(ui, new object[] { 3, combo, Enum.ToObject(formatType, 2), 18, 0f, 0.5f, 0.15f, null, null, true });

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 12; i++) tick(0.02f); // t = 0.24 / 0.50 = 0.48
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "GameOverUI counter tick must be Zero-GC");
            Assert.IsTrue(ui.IsCounting);
            Assert.AreNotEqual("0", kills.text);
            Assert.AreNotEqual("1\u00A0234", kills.text);
            Assert.Greater(kills.transform.localScale.x, 1.0f, "number pulses upward while counting");

            for (int i = 0; i < 20; i++) tick(0.02f);
            Assert.IsFalse(ui.IsCounting);
            Assert.AreEqual("WAVE 12", wave.text);
            Assert.AreEqual("1\u00A0234", kills.text);
            Assert.AreEqual("02:05", time.text);
            Assert.AreEqual("x18", combo.text);
            Assert.AreEqual(Vector3.one, kills.transform.localScale);
            Assert.AreEqual(Vector3.one, wave.transform.localScale);
        }
    }
}
