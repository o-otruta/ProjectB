using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.EditorScripts;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class LocalizationTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            LocalizationService.SetCurrent(null);
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationService.SetCurrent(null);
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

        private static Dictionary<string, string> LoadTable(string code)
        {
            TextAsset asset = Resources.Load<TextAsset>("Localization/" + code);
            Assert.IsNotNull(asset, $"Missing Resources/Localization/{code}.json");
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            LocalizationJsonParser.ParseInto(asset.text, map);
            return map;
        }

        private static string ExtractSortedTokens(string value, Regex pattern)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var matches = pattern.Matches(value);
            var list = new List<string>(matches.Count);
            for (int i = 0; i < matches.Count; i++) list.Add(matches[i].Value);
            list.Sort(StringComparer.Ordinal);
            return string.Join("|", list);
        }

        [Test]
        public void LocalizationTables_Exist_AndHaveIdenticalKeySets_AndMatchingPlaceholders()
        {
            Dictionary<string, string> en = LoadTable("en");
            Dictionary<string, string> uk = LoadTable("uk");
            Dictionary<string, string> ru = LoadTable("ru");

            Assert.Greater(en.Count, 500, "en.json should contain all scene, SO, and runtime UI keys.");
            Assert.AreEqual(en.Count, uk.Count, "uk.json key count does not match en.json.");
            Assert.AreEqual(en.Count, ru.Count, "ru.json key count does not match en.json.");

            var placeholderRegex = new Regex(@"\{\d+\}", RegexOptions.Compiled);
            var tagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

            foreach (KeyValuePair<string, string> kvp in en)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(kvp.Value), $"en.json has empty value for {kvp.Key}");

                Assert.IsTrue(uk.TryGetValue(kvp.Key, out string ukVal), $"uk.json missing key: {kvp.Key}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(ukVal), $"uk.json has empty value for {kvp.Key}");

                Assert.IsTrue(ru.TryGetValue(kvp.Key, out string ruVal), $"ru.json missing key: {kvp.Key}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(ruVal), $"ru.json has empty value for {kvp.Key}");

                Assert.AreEqual(
                    ExtractSortedTokens(kvp.Value, placeholderRegex),
                    ExtractSortedTokens(ukVal, placeholderRegex),
                    $"Placeholder mismatch in uk.json for {kvp.Key}");
                Assert.AreEqual(
                    ExtractSortedTokens(kvp.Value, placeholderRegex),
                    ExtractSortedTokens(ruVal, placeholderRegex),
                    $"Placeholder mismatch in ru.json for {kvp.Key}");

                Assert.AreEqual(
                    ExtractSortedTokens(kvp.Value, tagRegex),
                    ExtractSortedTokens(ukVal, tagRegex),
                    $"Rich-text tag mismatch in uk.json for {kvp.Key}");
                Assert.AreEqual(
                    ExtractSortedTokens(kvp.Value, tagRegex),
                    ExtractSortedTokens(ruVal, tagRegex),
                    $"Rich-text tag mismatch in ru.json for {kvp.Key}");

                if (kvp.Key.StartsWith("hero.", StringComparison.Ordinal) &&
                    kvp.Key.EndsWith(".passive_desc", StringComparison.Ordinal))
                {
                    StringAssert.Contains("<b>Зброя:</b>", ukVal, $"uk.json hero passive_desc missing <b>Зброя:</b> on {kvp.Key}");
                    StringAssert.Contains("<b>Оружие:</b>", ruVal, $"ru.json hero passive_desc missing <b>Оружие:</b> on {kvp.Key}");
                }
            }
        }

        [Test]
        public void ThemeFontAtlases_ContainUkrainianLetters_AndEveryCharacterUsedInAllThreeTables()
        {
            Dictionary<string, string> en = LoadTable("en");
            Dictionary<string, string> uk = LoadTable("uk");
            Dictionary<string, string> ru = LoadTable("ru");

            var allChars = new HashSet<char>();
            LocalizationTool.CollectPrintableChars(en.Values, allChars);
            LocalizationTool.CollectPrintableChars(uk.Values, allChars);
            LocalizationTool.CollectPrintableChars(ru.Values, allChars);
            string charSample = string.Concat(allChars);

            UiTheme theme = UiTheme.Current;
            Assert.IsNotNull(theme, "UiTheme.Current missing");

            foreach (TMP_FontAsset font in new[] { theme.displayFont, theme.bodyFont, theme.bodyBoldFont })
            {
                Assert.IsNotNull(font, "Theme font missing in UiTheme");
                Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, $"{font.name} should be baked Static");

                bool hasUkrainian = font.HasCharacters("ҐґЄєІіЇї’'", out List<char> missingUkr);
                Assert.IsTrue(hasUkrainian, $"{font.name} missing Ukrainian glyphs: {(missingUkr != null ? string.Concat(missingUkr) : "")}");

                bool hasAllTableChars = font.HasCharacters(charSample, out List<char> missingChars);
                Assert.IsTrue(hasAllTableChars, $"{font.name} missing glyphs used in localization tables: {(missingChars != null ? string.Concat(missingChars) : "")}");
            }
        }

        [Test]
        public void ScriptableObjects_AllTextBearingAssets_HaveValidLocalizationKeysInAllTables()
        {
            Dictionary<string, string> en = LoadTable("en");
            Dictionary<string, string> uk = LoadTable("uk");
            Dictionary<string, string> ru = LoadTable("ru");

            void AssertKeyPresent(string key, string assetContext)
            {
                Assert.IsFalse(string.IsNullOrEmpty(key), $"Empty localization key on {assetContext}");
                Assert.IsTrue(en.ContainsKey(key), $"Missing EN key '{key}' referenced by {assetContext}");
                Assert.IsTrue(uk.ContainsKey(key), $"Missing UK key '{key}' referenced by {assetContext}");
                Assert.IsTrue(ru.ContainsKey(key), $"Missing RU key '{key}' referenced by {assetContext}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AbilityData"))
            {
                var ab = AssetDatabase.LoadAssetAtPath<AbilityData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(ab.nameKey, $"AbilityData:{ab.name}");
                AssertKeyPresent(ab.descriptionKey, $"AbilityData:{ab.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(card.nameKey, $"CardData:{card.name}");
                AssertKeyPresent(card.descriptionKey, $"CardData:{card.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TalentNodeData"))
            {
                var node = AssetDatabase.LoadAssetAtPath<TalentNodeData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(node.nameKey, $"TalentNodeData:{node.name}");
                AssertKeyPresent(node.descriptionKey, $"TalentNodeData:{node.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AchievementData"))
            {
                var ach = AssetDatabase.LoadAssetAtPath<AchievementData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(ach.nameKey, $"AchievementData:{ach.name}");
                AssertKeyPresent(ach.descriptionKey, $"AchievementData:{ach.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:HeroData"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(hero.nameKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.roleKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.descriptionKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.passiveNameKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.passiveDescriptionKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.ultimateNameKey, $"HeroData:{hero.name}");
                AssertKeyPresent(hero.ultimateDescriptionKey, $"HeroData:{hero.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SynergyData"))
            {
                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(syn.nameKey, $"SynergyData:{syn.name}");
                AssertKeyPresent(syn.descriptionKey, $"SynergyData:{syn.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:MetaUpgradeData"))
            {
                var upg = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(upg.nameKey, $"MetaUpgradeData:{upg.name}");
                AssertKeyPresent(upg.descriptionKey, $"MetaUpgradeData:{upg.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:BossData"))
            {
                var boss = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(boss.nameKey, $"BossData:{boss.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:WaveMutatorData"))
            {
                var mutator = AssetDatabase.LoadAssetAtPath<WaveMutatorData>(AssetDatabase.GUIDToAssetPath(guid));
                AssertKeyPresent(mutator.titleKey, $"WaveMutatorData:{mutator.name}");
                AssertKeyPresent(mutator.subtitleKey, $"WaveMutatorData:{mutator.name}");
            }
        }

        [Test]
        public void Scenes_AllStaticTmpTexts_HaveLocalizedTextWithValidKey()
        {
            Dictionary<string, string> en = LoadTable("en");

            foreach (string scenePath in new[] { LocalizationTool.MAIN_MENU_SCENE, LocalizationTool.GAMEPLAY_SCENE })
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                HashSet<TMP_Text> dynamicRefs = LocalizationTool.CollectDynamicTmpReferences(scene);

                int localizedCount = 0;
                foreach (TMP_Text tmp in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (LocalizationTool.ShouldSkipLocalizedText(tmp, dynamicRefs)) continue;

                    var loc = tmp.GetComponent<LocalizedText>();
                    Assert.IsNotNull(loc, $"Static TMP_Text '{tmp.name}' (text='{tmp.text}') in {scenePath} lacks LocalizedText component.");
                    Assert.IsFalse(string.IsNullOrEmpty(loc.Key), $"LocalizedText on '{tmp.name}' in {scenePath} has empty key.");
                    Assert.IsTrue(en.ContainsKey(loc.Key), $"LocalizedText key '{loc.Key}' on '{tmp.name}' in {scenePath} is missing from en.json.");
                    localizedCount++;
                }

                Assert.Greater(localizedCount, 10, $"Expected localized static TMP_Texts in {scenePath}");
            }
        }

        [Test]
        public void LocalizationService_ResolvesSystemLanguage_PersistsSelection_SwitchesLive_AndIsZeroGc()
        {
            Assert.AreEqual(SystemLanguage.Ukrainian, LocalizationService.ResolveInitialLanguage("", SystemLanguage.Ukrainian));
            Assert.AreEqual(SystemLanguage.Russian, LocalizationService.ResolveInitialLanguage("", SystemLanguage.Russian));
            Assert.AreEqual(SystemLanguage.English, LocalizationService.ResolveInitialLanguage("", SystemLanguage.French));
            Assert.AreEqual(SystemLanguage.Ukrainian, LocalizationService.ResolveInitialLanguage("uk", SystemLanguage.Russian));
            Assert.AreEqual(SystemLanguage.Russian, LocalizationService.ResolveInitialLanguage("ru", SystemLanguage.English));

            using (var save = new SaveManager())
            {
                var service = new LocalizationService(save);
                LocalizationService.SetCurrent(service);
                service.SetLanguage(SystemLanguage.English, persist: false);

                var go = new GameObject("LocalizedText_Test");
                try
                {
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    var lt = go.AddComponent<LocalizedText>();
                    lt.SetKey("ui.settings.title");
                    Assert.AreEqual("SETTINGS", tmp.text);

                    int eventCount = 0;
                    service.LanguageChanged += () => eventCount++;

                    service.SetLanguage(SystemLanguage.Ukrainian);
                    Assert.AreEqual(1, eventCount);
                    Assert.AreEqual("НАЛАШТУВАННЯ", tmp.text);
                    Assert.AreEqual("uk", save.GetLanguageCode());

                    service.SetLanguage(SystemLanguage.Russian);
                    Assert.AreEqual(2, eventCount);
                    Assert.AreEqual("НАСТРОЙКИ", tmp.text);
                    Assert.AreEqual("ru", save.GetLanguageCode());

                    using (var reloadedSave = new SaveManager())
                    {
                        var reloaded = new LocalizationService(reloadedSave);
                        Assert.AreEqual(SystemLanguage.Russian, reloaded.CurrentLanguage);
                        Assert.AreEqual("НАСТРОЙКИ", reloaded.Get("ui.settings.title"));
                    }

                    // Warmup + Zero-GC lookup check
                    string _ = service.Get("ui.settings.title");
                    string __ = service.FormatInt("ui.hud.wave_title", 42);

                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 64; i++)
                    {
                        _ = service.Get("ui.settings.title");
                        __ = service.FormatInt("ui.hud.wave_title", 42);
                    }
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.AreEqual(0L, allocated, "Steady-state localization Get/FormatInt must not allocate.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void SettingsPopupUI_InBothScenes_HasLanguageSwitcherWired_AndSwitchesLanguage()
        {
            using (var save = new SaveManager())
            {
                var service = new LocalizationService(save);
                LocalizationService.SetCurrent(service);

                foreach (string scenePath in new[] { LocalizationTool.MAIN_MENU_SCENE, LocalizationTool.GAMEPLAY_SCENE })
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var settings = UnityEngine.Object.FindFirstObjectByType<SettingsPopupUI>(FindObjectsInactive.Include);
                    Assert.IsNotNull(settings, $"SettingsPopupUI missing in {scenePath}");

                    service.SetLanguage(SystemLanguage.English, persist: false);
                    settings.RefreshLanguageButtons();

                    var valueTextField = typeof(SettingsPopupUI).GetField("languageValueText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var enBtnField = typeof(SettingsPopupUI).GetField("englishButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var ukBtnField = typeof(SettingsPopupUI).GetField("ukrainianButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var ruBtnField = typeof(SettingsPopupUI).GetField("russianButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    var ukBtnObj = ukBtnField?.GetValue(settings) as Component;
                    var valueText = valueTextField?.GetValue(settings) as TMP_Text;
                    Assert.IsNotNull(valueText, $"languageValueText not wired in {scenePath}");
                    Assert.IsNotNull(enBtnField?.GetValue(settings), $"englishButton not wired in {scenePath}");
                    Assert.IsNotNull(ukBtnObj, $"ukrainianButton not wired in {scenePath}");
                    Assert.IsNotNull(ruBtnField?.GetValue(settings), $"russianButton not wired in {scenePath}");

                    var ukLabel = ukBtnObj.GetComponentInChildren<TMP_Text>(true);
                    Assert.IsNotNull(ukLabel, $"UkrainianButton TMP_Text missing in {scenePath}");
                    Assert.AreEqual("UA", ukLabel.text, $"UkrainianButton label must be 'UA' in {scenePath}");

                    Assert.AreEqual("English", valueText.text);

                    settings.SelectUkrainian();
                    Assert.AreEqual(SystemLanguage.Ukrainian, service.CurrentLanguage);
                    Assert.AreEqual("Українська", valueText.text);

                    settings.SelectRussian();
                    Assert.AreEqual(SystemLanguage.Russian, service.CurrentLanguage);
                    Assert.AreEqual("Русский", valueText.text);
                }
            }
        }

        [Test]
        public void RootLifetimeScope_Build_EagerlyCreatesLocalizationService_AsCurrentWithSavedLanguage()
        {
            using (var seedSave = new SaveManager())
            {
                seedSave.SetLanguageCode("uk", saveImmediately: true);
            }

            LocalizationService.SetCurrent(null);

            var rootGo = new GameObject("LocalizationRootScopeTest");
            rootGo.SetActive(false);
            var rootScope = rootGo.AddComponent<RootLifetimeScope>();
            try
            {
                using (LifetimeScope.EnqueueParent(null))
                {
                    rootScope.Build();
                }

                LocalizationService currentBeforeResolve = LocalizationService.Current;
                LocalizationService fromDi = rootScope.Container.Resolve<LocalizationService>();

                Assert.AreSame(fromDi, currentBeforeResolve, "RootLifetimeScope must eagerly initialize LocalizationService.Current via DI.");
                Assert.AreEqual(SystemLanguage.Ukrainian, currentBeforeResolve.CurrentLanguage, "DI LocalizationService must load saved language from SaveManager.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootGo);
            }
        }
    }
}
