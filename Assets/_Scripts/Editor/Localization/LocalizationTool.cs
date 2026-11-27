using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.EditorScripts.UiKit;
using ProjectB.LevelUp;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Sprint 12.3 editor tooling: assigns localization keys to all text-bearing ScriptableObjects,
    /// wires the EN/UK/RU switcher in <see cref="SettingsPopupUI"/>, attaches <see cref="LocalizedText"/>
    /// to every static scene <see cref="TMP_Text"/>, generates <c>Resources/Localization/en.json</c>,
    /// and validates EN/UK/RU table parity and SDF font glyph coverage.
    /// </summary>
    public static class LocalizationTool
    {
        public const string LOC_DIR = "Assets/Resources/Localization";
        public const string MAIN_MENU_SCENE = "Assets/_Scenes/MainMenu.unity";
        public const string GAMEPLAY_SCENE = "Assets/_Scenes/Gameplay.unity";

        private static readonly Dictionary<string, string> StaticUiKeyOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // MainMenu
            ["Canvas/SafeAreaRoot/UpgradesPanel/Title"] = "ui.upgrades.title",
            ["Canvas/SafeAreaRoot/ConstellationScreen/ConstellationPanel/Header/Title"] = "ui.constellation.title",
            ["Canvas/SafeAreaRoot/ConstellationScreen/ConstellationPanel/CenterButton/Text"] = "ui.constellation.core_upper",
            ["Canvas/SafeAreaRoot/AbilitiesPanel/Title"] = "ui.abilities.title",
            ["Canvas/SafeAreaRoot/AchievementsPanel/Title"] = "ui.achievements.title",
            ["Canvas/SafeAreaRoot/AchievementsPanel/RewardPopup/Window/CloseButton/Text"] = "ui.common.ok",
            ["Canvas/SafeAreaRoot/DailyBonusPopup/PopupPanel/Window/Title"] = "ui.daily.title",
            ["Canvas/SafeAreaRoot/DailyBonusPopup/PopupPanel/Window/ClaimButton/Text"] = "ui.daily.claim",
            ["Canvas/SafeAreaRoot/DailyBonusPopup/PopupPanel/Window/ClaimDoubleButton/Text"] = "ui.daily.claim_double",
            ["Canvas/SafeAreaRoot/DailyBonusPopup/PopupPanel/Window/AllClaimedMessage"] = "ui.daily.all_claimed",
            ["Canvas/SafeAreaRoot/PlayScreenPanel/HeroInfoCard/StatChips/HpChip/Caption"] = "ui.hero.hp_caption",
            ["Canvas/SafeAreaRoot/PlayScreenPanel/HeroInfoCard/StatChips/SpeedChip/Caption"] = "ui.hero.speed_caption",
            ["Canvas/SafeAreaRoot/PlayScreenPanel/HeroInfoCard/PassiveRow/Caption"] = "ui.hero.passive_caption",
            ["Canvas/SafeAreaRoot/PlayScreenPanel/HeroInfoCard/WeaponRow/Caption"] = "ui.hero.weapon_caption",
            ["Canvas/SafeAreaRoot/PlayScreenPanel/HeroInfoCard/UltimateRow/Caption"] = "ui.hero.ultimate_caption",
            ["Canvas/SafeAreaRoot/BottomBar/UpgradesButton/Label"] = "ui.tab.upgrades",
            ["Canvas/SafeAreaRoot/BottomBar/ConstellationsButton/Label"] = "ui.tab.constellations",
            ["Canvas/SafeAreaRoot/BottomBar/AbilitiesButton/Label"] = "ui.tab.abilities",
            ["Canvas/SafeAreaRoot/BottomBar/PlayButton/Label"] = "ui.tab.play",
            ["Canvas/SafeAreaRoot/BottomBar/AchievementsButton/Label"] = "ui.tab.awards",
            ["Canvas/SafeAreaRoot/SettingsPopup/PopupPanel/Window/Title"] = "ui.settings.title",
            ["Canvas/SafeAreaRoot/SettingsPopup/PopupPanel/Window/Rows/MusicVolumeRow/Header/Label"] = "ui.settings.music",
            ["Canvas/SafeAreaRoot/SettingsPopup/PopupPanel/Window/Rows/SfxVolumeRow/Header/Label"] = "ui.settings.sound",
            ["Canvas/SafeAreaRoot/SettingsPopup/PopupPanel/Window/Rows/LanguageRow/Header/Label"] = "ui.settings.language",
            ["Canvas/SafeAreaRoot/SettingsPopup/PopupPanel/Window/DoneButton/Text"] = "ui.settings.done",

            // Gameplay
            ["Canvas/PauseMenu/PausePanel/Window/Title"] = "ui.pause.title",
            ["Canvas/PauseMenu/PausePanel/Window/Summary/WaveChip/Caption"] = "ui.pause.wave_caption",
            ["Canvas/PauseMenu/PausePanel/Window/Summary/TimeChip/Caption"] = "ui.pause.time_caption",
            ["Canvas/PauseMenu/PausePanel/Window/Summary/KillsChip/Caption"] = "ui.pause.kills_caption",
            ["Canvas/PauseMenu/PausePanel/Window/Summary/CoinsChip/Caption"] = "ui.pause.coins_caption",
            ["Canvas/PauseMenu/PausePanel/Window/BuildHeader"] = "ui.pause.build",
            ["Canvas/PauseMenu/PausePanel/Window/SynergySection/Header"] = "ui.pause.active_synergies",
            ["Canvas/PauseMenu/PausePanel/Window/ResumeButton/Content/Text"] = "ui.pause.resume",
            ["Canvas/PauseMenu/PausePanel/Window/SettingsButton/Content/Text"] = "ui.settings.title",
            ["Canvas/PauseMenu/PausePanel/Window/QuitToMenuButton/Text"] = "ui.pause.quit_to_menu",
            ["Canvas/PauseMenu/PausePanel/QuitConfirmPanel/Dialog/Title"] = "ui.pause.quit_title",
            ["Canvas/PauseMenu/PausePanel/QuitConfirmPanel/Dialog/Message"] = "ui.pause.quit_message",
            ["Canvas/PauseMenu/PausePanel/QuitConfirmPanel/Dialog/ButtonsRow/CancelButton/Text"] = "ui.common.cancel",
            ["Canvas/PauseMenu/PausePanel/QuitConfirmPanel/Dialog/ButtonsRow/ConfirmButton/Text"] = "ui.pause.quit_confirm",
            ["Canvas/SettingsPopup/PopupPanel/Window/Title"] = "ui.settings.title",
            ["Canvas/SettingsPopup/PopupPanel/Window/Rows/MusicVolumeRow/Header/Label"] = "ui.settings.music",
            ["Canvas/SettingsPopup/PopupPanel/Window/Rows/SfxVolumeRow/Header/Label"] = "ui.settings.sound",
            ["Canvas/SettingsPopup/PopupPanel/Window/Rows/LanguageRow/Header/Label"] = "ui.settings.language",
            ["Canvas/SettingsPopup/PopupPanel/Window/DoneButton/Text"] = "ui.settings.done",
            ["Canvas/GameOverPanel/Content/Layout/BestRow/NewBestBadge/Text"] = "ui.gameover.new_best",
            ["Canvas/GameOverPanel/Content/Layout/StatChips/KillsChip/Caption"] = "ui.pause.kills_caption",
            ["Canvas/GameOverPanel/Content/Layout/StatChips/TimeChip/Caption"] = "ui.pause.time_caption",
            ["Canvas/GameOverPanel/Content/Layout/StatChips/ComboChip/Caption"] = "ui.gameover.best_combo",
            ["Canvas/GameOverPanel/Content/Layout/RunAchievementsSection/Header"] = "ui.gameover.achievements_header",
            ["Canvas/GameOverPanel/Content/Layout/DamageBreakdownSection/Header"] = "ui.gameover.breakdown_header",
            ["Canvas/GameOverPanel/Content/Layout/AdButtons/ReviveButton/Text"] = "ui.gameover.revive",
            ["Canvas/GameOverPanel/Content/Layout/NavButtons/RestartButton/Content/Text"] = "ui.gameover.restart",
            ["Canvas/GameOverPanel/Content/Layout/NavButtons/MenuButton/Text"] = "ui.gameover.menu",
        };

        [MenuItem("ProjectB/Localization/Generate Keys & Populate Scenes")]
        public static void GenerateKeysAndPopulateScenes()
        {
            var enMap = new SortedDictionary<string, string>(StringComparer.Ordinal);

            // Seed all runtime keys from LocalizationService defaults
            var loc = new LocalizationService();
            foreach (var kvp in loc.ActiveEntries)
            {
                enMap[kvp.Key] = kvp.Value;
            }

            AssignScriptableObjectKeys(enMap);
            ProcessScene(MAIN_MENU_SCENE, enMap);
            ProcessScene(GAMEPLAY_SCENE, enMap);

            WriteJson("en", enMap);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LocalizationTool] Generated {enMap.Count} EN keys, updated SOs and scenes.");
        }

        [MenuItem("ProjectB/Localization/Validate All Tables & Fonts")]
        public static void ValidateAll()
        {
            var en = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var uk = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var ru = new SortedDictionary<string, string>(StringComparer.Ordinal);
            LoadExistingJson("en", en);
            LoadExistingJson("uk", uk);
            LoadExistingJson("ru", ru);

            var issues = new List<string>();
            CheckParity("uk", en, uk, issues);
            CheckParity("ru", en, ru, issues);

            UiTheme theme = UiTheme.Current;
            var allChars = new HashSet<char>();
            CollectPrintableChars(en.Values, allChars);
            CollectPrintableChars(uk.Values, allChars);
            CollectPrintableChars(ru.Values, allChars);
            string charSample = string.Concat(allChars);

            foreach (TMP_FontAsset font in new[] { theme.displayFont, theme.bodyFont, theme.bodyBoldFont })
            {
                if (font == null)
                {
                    issues.Add("Missing theme font");
                    continue;
                }
                if (!font.HasCharacters(charSample, out List<char> missing))
                {
                    issues.Add($"Font '{font.name}' missing glyphs: {string.Concat(missing)}");
                }
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[LocalizationTool] Validation PASSED: {en.Count} keys across en/uk/ru, {allChars.Count} unique glyphs in all 3 atlases.");
            }
            else
            {
                Debug.LogError("[LocalizationTool] Validation FAILED:\n" + string.Join("\n", issues));
            }
        }

        private static void CheckParity(string code, SortedDictionary<string, string> en, SortedDictionary<string, string> other, List<string> issues)
        {
            foreach (var kvp in en)
            {
                if (!other.TryGetValue(kvp.Key, out string val))
                {
                    issues.Add($"{code}.json missing key: {kvp.Key}");
                }
                else if (string.IsNullOrWhiteSpace(val))
                {
                    issues.Add($"{code}.json empty value for key: {kvp.Key}");
                }
            }
            foreach (var kvp in other)
            {
                if (!en.ContainsKey(kvp.Key))
                {
                    issues.Add($"{code}.json has extra key not in en.json: {kvp.Key}");
                }
            }
        }

        public static void CollectPrintableChars(IEnumerable<string> values, HashSet<char> target)
        {
            foreach (string raw in values)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string stripped = StripTmpTags(raw);
                for (int i = 0; i < stripped.Length; i++)
                {
                    char c = stripped[i];
                    if (char.IsControl(c) || char.IsWhiteSpace(c)) continue;
                    target.Add(c);
                }
            }
        }

        public static string StripTmpTags(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>' && inTag) { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }

        public static string SanitizeKeySegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "item";
            var sb = new StringBuilder(raw.Length);
            bool prevUnderscore = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && i > 0 && char.IsLower(raw[i - 1]) && !prevUnderscore && sb.Length > 0)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                    prevUnderscore = false;
                }
                else if (!prevUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    prevUnderscore = true;
                }
            }
            return sb.ToString().Trim('_');
        }

        private static void AssignScriptableObjectKeys(SortedDictionary<string, string> enMap)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityData"))
            {
                var ab = AssetDatabase.LoadAssetAtPath<AbilityData>(AssetDatabase.GUIDToAssetPath(guid));
                if (ab == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(ab.id) ? ab.id : ab.name);
                ab.nameKey = $"ability.{seg}.name";
                ab.descriptionKey = $"ability.{seg}.desc";
                enMap[ab.nameKey] = ab.abilityName ?? string.Empty;
                enMap[ab.descriptionKey] = ab.description ?? string.Empty;
                EditorUtility.SetDirty(ab);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;
                string prefix = card switch
                {
                    AbilityCardData => "card.ability",
                    AbilityModifierCardData => "card.mod",
                    WeaponUpgradeCardData => "card.weapon",
                    StatUpgradeCardData => "card.stat",
                    _ => "card.other"
                };
                string seg = SanitizeKeySegment(card.name);
                card.nameKey = $"{prefix}.{seg}.name";
                card.descriptionKey = $"{prefix}.{seg}.desc";
                enMap[card.nameKey] = card.cardName ?? string.Empty;
                enMap[card.descriptionKey] = card.description ?? string.Empty;
                EditorUtility.SetDirty(card);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TalentNodeData"))
            {
                var node = AssetDatabase.LoadAssetAtPath<TalentNodeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (node == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(node.id) ? node.id : node.name);
                node.nameKey = $"talent.{seg}.name";
                node.descriptionKey = $"talent.{seg}.desc";
                enMap[node.nameKey] = node.displayName ?? string.Empty;
                enMap[node.descriptionKey] = node.description ?? string.Empty;
                EditorUtility.SetDirty(node);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AchievementData"))
            {
                var ach = AssetDatabase.LoadAssetAtPath<AchievementData>(AssetDatabase.GUIDToAssetPath(guid));
                if (ach == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(ach.id) ? ach.id : ach.name);
                ach.nameKey = $"ach.{seg}.title";
                ach.descriptionKey = $"ach.{seg}.desc";
                enMap[ach.nameKey] = ach.title ?? string.Empty;
                enMap[ach.descriptionKey] = ach.description ?? string.Empty;
                EditorUtility.SetDirty(ach);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:HeroData"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(hero.id) ? hero.id : hero.name);
                hero.nameKey = $"hero.{seg}.name";
                hero.roleKey = $"hero.{seg}.role";
                hero.descriptionKey = $"hero.{seg}.desc";
                hero.passiveNameKey = $"hero.{seg}.passive_name";
                hero.passiveDescriptionKey = $"hero.{seg}.passive_desc";
                hero.ultimateNameKey = $"hero.{seg}.ult_name";
                hero.ultimateDescriptionKey = $"hero.{seg}.ult_desc";
                enMap[hero.nameKey] = hero.heroName ?? string.Empty;
                enMap[hero.roleKey] = hero.role ?? string.Empty;
                enMap[hero.descriptionKey] = hero.description ?? string.Empty;
                enMap[hero.passiveNameKey] = hero.passiveName ?? string.Empty;
                enMap[hero.passiveDescriptionKey] = hero.passiveDescription ?? string.Empty;
                enMap[hero.ultimateNameKey] = hero.ultimateName ?? string.Empty;
                enMap[hero.ultimateDescriptionKey] = hero.ultimateDescription ?? string.Empty;
                EditorUtility.SetDirty(hero);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SynergyData"))
            {
                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (syn == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(syn.id) ? syn.id : syn.name);
                syn.nameKey = $"synergy.{seg}.name";
                syn.descriptionKey = $"synergy.{seg}.desc";
                enMap[syn.nameKey] = syn.synergyName ?? string.Empty;
                enMap[syn.descriptionKey] = syn.description ?? string.Empty;
                EditorUtility.SetDirty(syn);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:MetaUpgradeData"))
            {
                var up = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (up == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(up.id) ? up.id : up.name);
                up.nameKey = $"upgrade.{seg}.name";
                up.descriptionKey = $"upgrade.{seg}.desc";
                enMap[up.nameKey] = up.displayName ?? string.Empty;
                enMap[up.descriptionKey] = up.description ?? string.Empty;
                EditorUtility.SetDirty(up);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:BossData"))
            {
                var boss = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
                if (boss == null) continue;
                string seg = SanitizeKeySegment(!string.IsNullOrEmpty(boss.bossId) ? boss.bossId : boss.name);
                boss.nameKey = $"boss.{seg}.name";
                enMap[boss.nameKey] = boss.bossDisplayName ?? string.Empty;
                EditorUtility.SetDirty(boss);
            }
        }

        private static void ProcessScene(string scenePath, SortedDictionary<string, string> enMap)
        {
            Scene loaded = SceneManager.GetSceneByPath(scenePath);
            bool opened = !loaded.isLoaded;
            Scene scene = opened ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive) : loaded;
            try
            {
                WireSettingsLanguageSwitcher(scene);

                HashSet<TMP_Text> dynamicRefs = CollectDynamicTmpReferences(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        var existingLoc = tmp.GetComponent<LocalizedText>();
                        if (ShouldSkipLocalizedText(tmp, dynamicRefs))
                        {
                            if (existingLoc != null) UnityEngine.Object.DestroyImmediate(existingLoc);
                            continue;
                        }

                        string hierarchyPath = GetHierarchyPath(tmp.transform);
                        if (!StaticUiKeyOverrides.TryGetValue(hierarchyPath, out string key))
                        {
                            key = $"ui.{SanitizeKeySegment(scene.name)}.{SanitizeKeySegment(hierarchyPath)}";
                        }

                        if (existingLoc == null) existingLoc = tmp.gameObject.AddComponent<LocalizedText>();
                        existingLoc.EditorSetup(key);

                        if (!string.IsNullOrWhiteSpace(tmp.text))
                        {
                            enMap[key] = tmp.text;
                        }
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void WireSettingsLanguageSwitcher(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SettingsPopupUI settings in root.GetComponentsInChildren<SettingsPopupUI>(true))
                {
                    var so = new SerializedObject(settings);
                    var langRow = so.FindProperty("languageRow")?.objectReferenceValue as GameObject;
                    if (langRow == null) continue;

                    Transform window = langRow.transform.parent?.parent;
                    if (window != null)
                    {
                        var wrt = window as RectTransform;
                        if (wrt != null && wrt.sizeDelta.y < 860f)
                        {
                            wrt.sizeDelta = new Vector2(wrt.sizeDelta.x, 860f);
                        }
                    }

                    Transform oldHint = langRow.transform.Find("Hint");
                    if (oldHint != null) UnityEngine.Object.DestroyImmediate(oldHint.gameObject);

                    Transform oldButtons = langRow.transform.Find("LanguageButtons");
                    if (oldButtons != null) UnityEngine.Object.DestroyImmediate(oldButtons.gameObject);

                    Transform header = langRow.transform.Find("Header");
                    TMP_Text langValueText = header != null ? header.Find("Value")?.GetComponent<TMP_Text>() : null;

                    var buttonsRow = Go("LanguageButtons", langRow.transform);
                    HL(buttonsRow, 12f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);
                    LE(buttonsRow, prefH: 76f);

                    ThemedButton enBtn = Btn(buttonsRow.transform, "EnglishButton", "EN", ButtonStyle.Primary, null, 0f, UiTextStyle.Button, 0.78f);
                    ThemedButton ukBtn = Btn(buttonsRow.transform, "UkrainianButton", "UA", ButtonStyle.Secondary, null, 0f, UiTextStyle.Button, 0.78f);
                    ThemedButton ruBtn = Btn(buttonsRow.transform, "RussianButton", "RU", ButtonStyle.Secondary, null, 0f, UiTextStyle.Button, 0.78f);

                    so.FindProperty("languageValueText").objectReferenceValue = langValueText;
                    so.FindProperty("englishButton").objectReferenceValue = enBtn;
                    so.FindProperty("ukrainianButton").objectReferenceValue = ukBtn;
                    so.FindProperty("russianButton").objectReferenceValue = ruBtn;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
        }

        public static HashSet<TMP_Text> CollectDynamicTmpReferences(Scene scene)
        {
            var refs = new HashSet<TMP_Text>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb is TMP_Text || mb is ThemedText || mb is ThemedButton || mb is BottomTabUI || mb is LocalizedText) continue;
                    Type t = mb.GetType();
                    while (t != null && t != typeof(MonoBehaviour))
                    {
                        FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        for (int i = 0; i < fields.Length; i++)
                        {
                            FieldInfo f = fields[i];
                            object val = f.GetValue(mb);
                            if (val is TMP_Text singleTmp)
                            {
                                refs.Add(singleTmp);
                            }
                            else if (val is TMP_Text[] arr)
                            {
                                for (int k = 0; k < arr.Length; k++)
                                {
                                    if (arr[k] != null) refs.Add(arr[k]);
                                }
                            }
                            else if (val is CurrencyLabel cl)
                            {
                                var clText = typeof(CurrencyLabel).GetField("amountText", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(cl) as TMP_Text;
                                if (clText != null) refs.Add(clText);
                            }
                            else if (val is ProgressBarUI pb)
                            {
                                if (pb.Label != null) refs.Add(pb.Label);
                                var inv = typeof(ProgressBarUI).GetField("invertedLabel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(pb) as TMP_Text;
                                if (inv != null) refs.Add(inv);
                            }
                        }
                        t = t.BaseType;
                    }
                }
            }
            return refs;
        }

        public static bool ShouldSkipLocalizedText(TMP_Text tmp, HashSet<TMP_Text> dynamicRefs)
        {
            if (tmp == null) return true;
            string path = GetHierarchyPath(tmp.transform);
            if (StaticUiKeyOverrides.ContainsKey(path)) return false;
            if (dynamicRefs != null && dynamicRefs.Contains(tmp)) return true;
            if (tmp.GetComponentInParent<DebugCheatMenu>(true) != null) return true;

            if (path.Contains("/LanguageButtons/") || path.EndsWith("/LevelInverted")) return true;
            if (path.Contains("/SynergyChipTemplate/") || path.EndsWith("/Header/ShardsText")) return true;
            if (path.Contains("CardSelectionPanel/Content/CardsContainer/Card_") && path.EndsWith("/AdPlaceholder/Text")) return true;

            string raw = LocalizationTool.StripTmpTags(tmp.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw)) return true;
            if (raw == "X" || raw == "EN" || raw == "UA" || raw == "UK" || raw == "RU") return true;

            bool hasLetter = false;
            int letterCount = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                if (char.IsLetter(raw[i]))
                {
                    hasLetter = true;
                    letterCount++;
                }
            }
            if (!hasLetter) return true;

            // Skip unit-only / timer placeholders like "0m", "18s", "x5", "Lv 1 -> Lv 2"
            if (letterCount == 1 && (raw.EndsWith("m") || raw.EndsWith("s") || raw.StartsWith("x"))) return true;

            return false;
        }

        public static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        public static void LoadExistingJson(string code, IDictionary<string, string> target)
        {
            string filePath = Path.Combine(LOC_DIR, $"{code}.json");
            if (!File.Exists(filePath)) return;
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            LocalizationJsonParser.ParseInto(json, target);
        }

        public static void WriteJson(string code, SortedDictionary<string, string> map)
        {
            Directory.CreateDirectory(LOC_DIR);
            string filePath = Path.Combine(LOC_DIR, $"{code}.json");
            string json = LocalizationJsonParser.Serialize(map);
            File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
