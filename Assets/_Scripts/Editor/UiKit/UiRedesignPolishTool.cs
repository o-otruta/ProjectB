using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Platform;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Idempotent UI Kit v4 polish for secondary screens and shared prefabs that sit outside the 3 core
    /// redesign tools: <c>AbilityCollectionItem.prefab</c> rarity stripe, frameless <c>AbilitiesPanel/HeaderChips</c>
    /// readouts, centered <c>SettingsPopup</c> header, snug <c>GameOver</c> reward pills, and Primary gold <c>Restart</c> CTA.
    /// </summary>
    public static class UiRedesignPolishTool
    {
        public const string ABILITY_ITEM_PREFAB = "Assets/_Prefabs/UI/AbilityCollectionItem.prefab";

        [MenuItem("ProjectB/UI/Redesign/Apply Polish (Abilities, Settings, GameOver)")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            RestyleAbilityPrefab();

            Scene menu = EditorSceneManager.OpenScene(UiRedesignPlayScreenTool.MAIN_MENU_SCENE, OpenSceneMode.Single);
            ApplyMainMenu(menu);
            EditorSceneManager.SaveScene(menu);

            Scene gameplay = EditorSceneManager.OpenScene(UiRedesignHudTool.GAMEPLAY_SCENE, OpenSceneMode.Single);
            ApplyGameplay(gameplay);
            EditorSceneManager.SaveScene(gameplay);

            Debug.Log("[UiRedesignPolishTool] Polish applied to AbilityCollectionItem.prefab, MainMenu, and Gameplay.");
        }

        public static void ApplyMainMenu(Scene scene)
        {
            RestyleAbilitiesHeaderReadouts(scene);
            CenterSettingsTitle(scene);
            CenterButtonLabels(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void RestyleAbilitiesHeaderReadouts(Scene scene)
        {
            Transform abilitiesPanel = FindChildRecursive(scene, "AbilitiesPanel");
            if (abilitiesPanel == null) return;

            Transform title = abilitiesPanel.Find("Title");
            if (title != null)
            {
                SetResponsivePresets(Rt(title),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(-80f, 80f)),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-80f, 68f)));
                EditorUtility.SetDirty(title.gameObject);
            }

            Transform headerChips = abilitiesPanel.Find("HeaderChips");
            if (headerChips != null)
            {
                SetResponsivePresets(Rt(headerChips),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(-80f, 40f)),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(-80f, 36f)));

                if (headerChips.TryGetComponent(out HorizontalLayoutGroup rowHl))
                {
                    rowHl.spacing = 32f;
                    rowHl.padding = new RectOffset(0, 0, 0, 0);
                    rowHl.childAlignment = TextAnchor.MiddleCenter;
                    rowHl.childControlWidth = true;
                    rowHl.childControlHeight = true;
                    rowHl.childForceExpandWidth = false;
                    rowHl.childForceExpandHeight = true;
                    EditorUtility.SetDirty(rowHl);
                }

                foreach (string chipName in new[] { "CollectedChip", "SynergiesChip", "SlotsChip" })
                {
                    Transform chip = headerChips.Find(chipName);
                    if (chip == null) continue;

                    foreach (ThemedButton tb in chip.GetComponents<ThemedButton>())
                    {
                        Object.DestroyImmediate(tb);
                    }
                    foreach (Button btn in chip.GetComponents<Button>())
                    {
                        Object.DestroyImmediate(btn);
                    }

                    var tg = GetOrAdd<ThemedGraphic>(chip.gameObject);
                    tg.Configure(UiColor.SurfaceSunken, 0f, UiSprite.Pill);
                    EditorUtility.SetDirty(tg);

                    if (chip.TryGetComponent(out Image img))
                    {
                        img.raycastTarget = false;
                        if (img.material == Theme.surfaceMaterial) img.material = null;
                        EditorUtility.SetDirty(img);
                    }

                    if (chip.TryGetComponent(out HorizontalLayoutGroup hl))
                    {
                        hl.padding = new RectOffset(4, 4, 0, 0);
                        hl.spacing = 8f;
                        hl.childAlignment = TextAnchor.MiddleCenter;
                        hl.childControlWidth = true;
                        hl.childControlHeight = true;
                        hl.childForceExpandWidth = false;
                        hl.childForceExpandHeight = false;
                        EditorUtility.SetDirty(hl);
                    }

                    Transform icon = chip.Find("Icon");
                    if (icon != null)
                    {
                        var iconLe = LE(icon.gameObject, minH: 30f, prefH: 30f, minW: 30f, prefW: 30f);
                        EditorUtility.SetDirty(iconLe);

                        var iconTg = GetOrAdd<ThemedGraphic>(icon.gameObject);
                        if (chipName == "SlotsChip")
                        {
                            iconTg.Configure(UiColor.TextSecondary, 1f, UiSprite.None);
                        }
                        else
                        {
                            iconTg.Configure(UiColor.White, 0.85f, UiSprite.None);
                        }
                        EditorUtility.SetDirty(iconTg);
                        if (icon.TryGetComponent(out Image iconImg)) EditorUtility.SetDirty(iconImg);
                    }

                    Transform text = chip.Find("Text");
                    if (text != null)
                    {
                        var textTg = GetOrAdd<ThemedGraphic>(text.gameObject);
                        textTg.Configure(UiColor.TextSecondary, 1f, UiSprite.None);
                        EditorUtility.SetDirty(textTg);

                        if (text.TryGetComponent(out TMP_Text tmp))
                        {
                            tmp.raycastTarget = false;
                            tmp.textWrappingMode = TextWrappingModes.NoWrap;
                            tmp.overflowMode = TextOverflowModes.Overflow;
                            tmp.alignment = TextAlignmentOptions.MidlineLeft;
                            EditorUtility.SetDirty(tmp);
                        }
                    }

                    EditorUtility.SetDirty(chip.gameObject);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(Rt(headerChips));
            }

            Transform filter = abilitiesPanel.Find("Filter");
            if (filter != null)
            {
                SetResponsivePresets(Rt(filter),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(-80f, 92f)),
                    Preset(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(-80f, 92f)));

                var filterTg = GetOrAdd<ThemedGraphic>(filter.gameObject);
                filterTg.Configure(UiColor.SurfaceSunken, 0f, UiSprite.Pill);
                EditorUtility.SetDirty(filterTg);

                if (filter.TryGetComponent(out Image filterImg))
                {
                    filterImg.raycastTarget = false;
                    if (Theme.surfaceMaterial != null && filterImg.material == Theme.surfaceMaterial) filterImg.material = null;
                    EditorUtility.SetDirty(filterImg);
                }

                if (filter.TryGetComponent(out HorizontalLayoutGroup filterHl))
                {
                    filterHl.spacing = 12f;
                    filterHl.padding = new RectOffset(0, 0, 0, 0);
                    filterHl.childAlignment = TextAnchor.MiddleCenter;
                    filterHl.childControlWidth = true;
                    filterHl.childControlHeight = true;
                    filterHl.childForceExpandWidth = false;
                    filterHl.childForceExpandHeight = false;
                    EditorUtility.SetDirty(filterHl);
                }

                string[] chipNames = { "FilterActive", "FilterPassive", "FilterSynergies" };
                for (int i = 0; i < chipNames.Length; i++)
                {
                    Transform seg = filter.Find(chipNames[i]);
                    if (seg == null) continue;

                    if (seg.TryGetComponent(out Image segImg))
                    {
                        if (Theme.pillSprite != null)
                        {
                            segImg.sprite = Theme.pillSprite;
                            segImg.type = Image.Type.Sliced;
                        }
                        if (Theme.surfaceMaterial != null && segImg.material == Theme.surfaceMaterial)
                        {
                            segImg.material = null;
                        }
                        segImg.raycastTarget = true;
                        segImg.color = i == 0 ? Theme.accent : Theme.surfaceRaised;
                        EditorUtility.SetDirty(segImg);
                    }

                    if (seg.TryGetComponent(out Button segBtn))
                    {
                        segBtn.transition = Selectable.Transition.ColorTint;
                        EditorUtility.SetDirty(segBtn);
                    }

                    var segHl = HL(seg.gameObject, 0f, new RectOffset(28, 28, 0, 6), TextAnchor.MiddleCenter, true, true, false, true);
                    EditorUtility.SetDirty(segHl);

                    var segLe = LE(seg.gameObject, minH: 72f, prefH: 72f, flexH: -1f, minW: 120f, prefW: -1f, flexW: -1f);
                    EditorUtility.SetDirty(segLe);

                    Transform labelT = seg.Find("Label");
                    if (labelT != null)
                    {
                        foreach (LocalizedText loc in labelT.GetComponents<LocalizedText>())
                        {
                            Object.DestroyImmediate(loc);
                        }

                        var tt = GetOrAdd<ThemedText>(labelT.gameObject);
                        tt.Configure(UiTextStyle.Caption, 1.25f);
                        EditorUtility.SetDirty(tt);

                        if (labelT.TryGetComponent(out TMP_Text labelTmp))
                        {
                            labelTmp.alignment = TextAlignmentOptions.Center;
                            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
                            labelTmp.overflowMode = TextOverflowModes.Overflow;
                            labelTmp.raycastTarget = false;
                            labelTmp.color = i == 0 ? Theme.white : Theme.textSecondary;
                            EditorUtility.SetDirty(labelTmp);
                        }
                    }

                    EditorUtility.SetDirty(seg.gameObject);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(Rt(filter));
            }

            Transform scrollView = abilitiesPanel.Find("ScrollView");
            if (scrollView != null)
            {
                SetResponsivePresets(Rt(scrollView),
                    Preset(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(0f, -260f)),
                    Preset(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -114f), new Vector2(0f, -228f)));
                EditorUtility.SetDirty(scrollView.gameObject);

                Transform content = scrollView.Find("Viewport/Content");
                if (content != null && content.TryGetComponent(out GridLayoutGroup grid))
                {
                    grid.padding = new RectOffset(40, 40, 20, 28);
                    EditorUtility.SetDirty(grid);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ApplyGameplay(Scene scene)
        {
            CenterSettingsTitle(scene);
            CenterButtonLabels(scene);
            PolishGameOver(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void CenterButtonLabels(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ThemedButton tb in root.GetComponentsInChildren<ThemedButton>(true))
                {
                    if (tb == null || tb.Style == ButtonStyle.Icon || tb.Label == null) continue;
                    string n = tb.name;
                    if (n == "StartButton" || n == "RerollButton") continue;

                    if (tb.Label.transform.parent == tb.transform)
                    {
                        RectTransform rt = Rt(tb.Label);
                        rt.offsetMin = new Vector2(rt.offsetMin.x, 28f);
                        rt.offsetMax = new Vector2(rt.offsetMax.x, -2f);
                        EditorUtility.SetDirty(rt);
                    }
                    else
                    {
                        Transform content = tb.transform.Find("Content");
                        if (content != null && content.TryGetComponent<HorizontalLayoutGroup>(out _))
                        {
                            RectTransform rt = Rt(content);
                            rt.offsetMin = new Vector2(rt.offsetMin.x, 26f);
                            rt.offsetMax = new Vector2(rt.offsetMax.x, -2f);
                            EditorUtility.SetDirty(rt);
                        }
                    }
                }
            }
        }

        public static void RestyleAbilityPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ABILITY_ITEM_PREFAB);
            if (root == null) return;
            try
            {
                if (root.TryGetComponent(out HorizontalLayoutGroup rootHl))
                {
                    rootHl.padding = Pad(24, 24, 22, 30);
                    rootHl.spacing = 20f;
                    EditorUtility.SetDirty(rootHl);
                }

                Transform frame = root.transform.Find("Frame");
                if (frame != null)
                {
                    frame.gameObject.SetActive(false);
                }
                Transform stripe = root.transform.Find("RarityStripe");
                if (stripe != null)
                {
                    stripe.gameObject.SetActive(false);
                }

                Transform iconBg = root.transform.Find("IconBg");
                if (iconBg != null)
                {
                    LE(iconBg.gameObject, minH: 116f, prefH: 116f, minW: 116f, prefW: 116f);
                    Transform icon = iconBg.Find("Icon");
                    if (icon != null)
                    {
                        Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(88f, 88f));
                    }
                }

                Transform nameT = root.transform.Find("Info/Name");
                if (nameT != null)
                {
                    GetOrAdd<ThemedText>(nameT.gameObject).Configure(UiTextStyle.H2, 0.75f);
                    LE(nameT.gameObject, minH: 42f, prefH: 42f);
                }

                Transform descT = root.transform.Find("Info/Description");
                if (descT != null)
                {
                    GetOrAdd<ThemedText>(descT.gameObject).Configure(UiTextStyle.Caption, 1.1f);
                }

                Transform chipT = root.transform.Find("Info/SynergyChips/ChipTemplate");
                if (chipT != null)
                {
                    LE(chipT.gameObject, prefH: 52f);
                    if (chipT.TryGetComponent(out HorizontalLayoutGroup chipHl))
                    {
                        chipHl.padding = Pad(10, 18, 4, 4);
                        EditorUtility.SetDirty(chipHl);
                    }
                    Transform left = chipT.Find("Left");
                    if (left != null) LE(left.gameObject, prefH: 38f, prefW: 38f);
                    Transform right = chipT.Find("Right");
                    if (right != null) LE(right.gameObject, prefH: 38f, prefW: 38f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, ABILITY_ITEM_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetResponsivePresets(RectTransform rt, RectPreset portrait, RectPreset landscape)
        {
            portrait.ApplyTo(rt);
            var rr = GetOrAdd<ResponsiveRect>(rt.gameObject);
            rr.SetPreset(LayoutMode.Portrait, portrait);
            rr.SetPreset(LayoutMode.Landscape, landscape);
            EditorUtility.SetDirty(rr);
        }

        private static RectPreset Preset(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            return new RectPreset
            {
                captured = true,
                anchorMin = anchorMin,
                anchorMax = anchorMax,
                pivot = pivot,
                anchoredPosition = pos,
                sizeDelta = size
            };
        }

        public static void CenterSettingsTitle(Scene scene)
        {
            Transform title = FindChildRecursive(scene, "SettingsPopup/PopupPanel/Window/Title");
            if (title == null) return;

            Band(Rt(title), true, 36f, 80f, 120f, 120f);
            if (title.TryGetComponent(out TMP_Text tmp))
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 36f;
                tmp.fontSizeMax = Theme.h1Size;
                EditorUtility.SetDirty(tmp);
            }
        }

        public static void PolishGameOver(Scene scene)
        {
            Transform gameOverRoot = FindTransformByName(scene, "GameOverPanel");
            if (gameOverRoot == null) return;

            Transform layout = gameOverRoot.Find("Content/Layout");
            if (layout != null && layout.TryGetComponent(out VerticalLayoutGroup layoutVl))
            {
                layoutVl.spacing = 16f;
                EditorUtility.SetDirty(layoutVl);
            }

            Transform bestRow = FindDescendantByName(gameOverRoot, "BestRow");
            if (bestRow != null)
            {
                LE(bestRow.gameObject, minH: -1f, prefH: 64f);
                EditorUtility.SetDirty(bestRow.gameObject);
            }

            Transform bestBadge = FindDescendantByName(gameOverRoot, "NewBestBadge");
            if (bestBadge != null)
            {
                Rt(bestBadge).sizeDelta = new Vector2(368f, 64f);
                HL(bestBadge.gameObject, 8f, Pad(22, 24, 6, 20), TextAnchor.MiddleCenter, true, true, false, false);
                Transform star = bestBadge.Find("Star");
                if (star != null) LE(star.gameObject, prefW: 34f, prefH: 34f);
                Transform bestText = bestBadge.Find("Text");
                if (bestText != null && bestText.TryGetComponent(out TMP_Text bestTmp))
                {
                    bestTmp.alignment = TextAlignmentOptions.Center;
                    bestTmp.enableAutoSizing = true;
                    bestTmp.fontSizeMin = 16f;
                    bestTmp.fontSizeMax = 28f;
                    bestTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    EditorUtility.SetDirty(bestTmp);
                }
                EditorUtility.SetDirty(bestBadge.gameObject);
            }

            Transform statChips = FindDescendantByName(gameOverRoot, "StatChips");
            if (statChips != null)
            {
                LE(statChips.gameObject, minH: 166f, prefH: 166f, flexH: 0f);
                EditorUtility.SetDirty(statChips.gameObject);
            }

            foreach (string chipName in new[] { "KillsChip", "TimeChip", "ComboChip" })
            {
                Transform chip = FindDescendantByName(gameOverRoot, chipName);
                if (chip == null) continue;

                LE(chip.gameObject, minH: 166f, prefH: 166f, prefW: 260f, flexW: 1f);
                VL(chip.gameObject, 2f, Pad(14, 14, 14, 36), TextAnchor.MiddleCenter, true, true, false, false);

                Transform icon = chip.Find("Icon");
                if (icon != null) LE(icon.gameObject, prefW: 42f, prefH: 42f);

                Transform caption = chip.Find("Caption");
                if (caption != null && caption.TryGetComponent(out TMP_Text capTmp))
                {
                    capTmp.alignment = TextAlignmentOptions.Center;
                    capTmp.enableAutoSizing = true;
                    capTmp.fontSizeMin = 13f;
                    capTmp.fontSizeMax = 20f;
                    capTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    EditorUtility.SetDirty(capTmp);
                }
                EditorUtility.SetDirty(chip.gameObject);
            }

            Transform rewards = FindDescendantByName(gameOverRoot, "Rewards");
            if (rewards != null && rewards.TryGetComponent(out HorizontalLayoutGroup rhl))
            {
                rhl.spacing = 18f;
                rhl.childAlignment = TextAnchor.MiddleCenter;
                rhl.childControlWidth = false;
                rhl.childControlHeight = false;
                rhl.childForceExpandWidth = false;
                rhl.childForceExpandHeight = false;
                EditorUtility.SetDirty(rhl);
            }

            foreach (string pillName in new[] { "CoinsReward", "ShardsReward" })
            {
                Transform pill = FindDescendantByName(gameOverRoot, pillName);
                if (pill == null) continue;

                Rt(pill).sizeDelta = new Vector2(200f, 92f);
                var csf = GetOrAdd<ContentSizeFitter>(pill.gameObject);
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                LE(pill.gameObject, minW: 200f, prefW: -1f, flexW: -1f, prefH: 92f);

                HL(pill.gameObject, 12f, Pad(20, 28, 8, 22), TextAnchor.MiddleCenter, true, true, false, false);

                Transform icon = pill.Find("Icon");
                if (icon != null) LE(icon.gameObject, prefW: 54f, prefH: 54f);

                Transform amount = pill.Find("Amount");
                if (amount != null)
                {
                    LE(amount.gameObject, prefW: -1f, flexW: -1f);
                    if (amount.TryGetComponent(out TMP_Text tmp))
                    {
                        tmp.alignment = TextAlignmentOptions.MidlineLeft;
                        tmp.textWrappingMode = TextWrappingModes.NoWrap;
                        EditorUtility.SetDirty(tmp);
                    }
                }

                if (pill.TryGetComponent(out CurrencyLabel cl))
                {
                    SetFloat(cl, "countUpDuration", pillName == "CoinsReward" ? 1.0f : 0.85f);
                    SetBool(cl, "pulseWhileCounting", true);
                    SetFloat(cl, "pulseAmplitude", 0.24f);
                    SetFloat(cl, "pulseCycles", 4f);
                    EditorUtility.SetDirty(cl);
                }
                EditorUtility.SetDirty(pill.gameObject);
            }

            Transform dmgSection = FindDescendantByName(gameOverRoot, "DamageBreakdownSection");
            if (dmgSection != null)
            {
                Transform scroll = dmgSection.Find("ScrollView");
                if (scroll != null)
                {
                    Stretch(Rt(scroll), 0f, 34f, 0f, 56f);
                    EditorUtility.SetDirty(scroll);
                }
                Transform container = FindDescendantByName(dmgSection, "Container");
                if (container != null && container.TryGetComponent(out VerticalLayoutGroup dvl))
                {
                    dvl.padding = Pad(16, 16, 4, 6);
                    EditorUtility.SetDirty(dvl);
                }
            }

            Transform achSection = FindDescendantByName(gameOverRoot, "RunAchievementsSection");
            if (achSection != null)
            {
                LE(achSection.gameObject, minH: 176f, prefH: 176f);
                Transform scroll = achSection.Find("ScrollView");
                if (scroll != null)
                {
                    Stretch(Rt(scroll), 0f, 34f, 0f, 56f);
                    EditorUtility.SetDirty(scroll);
                }
                Transform content = FindDescendantByName(achSection, "Content");
                if (content != null && content.TryGetComponent(out VerticalLayoutGroup avl))
                {
                    avl.padding = Pad(16, 16, 4, 6);
                    EditorUtility.SetDirty(avl);
                }
                EditorUtility.SetDirty(achSection.gameObject);
            }

            if (gameOverRoot.TryGetComponent(out GameOverUI goUi))
            {
                SetFloat(goUi, "breakdownHeaderHeight", 86f);
                SetFloat(goUi, "countUpDuration", 0.90f);
                SetFloat(goUi, "countStaggerDelay", 0.07f);
                SetFloat(goUi, "countPulseAmplitude", 0.24f);
                SetFloat(goUi, "countPulseCycles", 4f);
                SetFloat(goUi, "countFinishPunch", 0.18f);
                EditorUtility.SetDirty(goUi);
            }

            foreach (string btnName in new[] { "ReviveButton", "DoubleCoinsButton", "MenuButton" })
            {
                Transform btn = FindDescendantByName(gameOverRoot, btnName);
                if (btn == null) continue;
                Transform txt = btn.Find("Text");
                if (txt != null)
                {
                    RectTransform rt = Rt(txt);
                    rt.offsetMin = new Vector2(20f, 38f);
                    rt.offsetMax = new Vector2(-20f, -8f);
                    EditorUtility.SetDirty(rt);
                }
            }

            Transform restart = FindDescendantByName(gameOverRoot, "RestartButton");
            if (restart != null && restart.TryGetComponent(out ThemedButton tb))
            {
                if (tb.Label != null && tb.Label.TryGetComponent(out ThemedGraphic labelGraphic))
                {
                    Object.DestroyImmediate(labelGraphic);
                }
                tb.SetStyle(ButtonStyle.Primary);

                Transform content = restart.Find("Content");
                if (content != null)
                {
                    RectTransform crt = Rt(content);
                    crt.offsetMin = new Vector2(20f, 38f);
                    crt.offsetMax = new Vector2(-20f, -8f);
                    EditorUtility.SetDirty(crt);

                    Transform icon = content.Find("Icon");
                    if (icon != null) LE(icon.gameObject, prefW: 42f, prefH: 42f);

                    Transform rText = content.Find("Text");
                    if (rText != null && rText.TryGetComponent(out TMP_Text rTmp))
                    {
                        rTmp.enableAutoSizing = true;
                        rTmp.fontSizeMin = 18f;
                        rTmp.fontSizeMax = 34f;
                        rTmp.textWrappingMode = TextWrappingModes.NoWrap;
                        EditorUtility.SetDirty(rTmp);
                    }
                }
                EditorUtility.SetDirty(tb);
            }
        }

        private static Transform FindChildRecursive(Scene scene, string relativePath)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform direct = root.transform.Find(relativePath);
                if (direct != null) return direct;

                int slash = relativePath.IndexOf('/');
                string head = slash >= 0 ? relativePath.Substring(0, slash) : relativePath;
                string tail = slash >= 0 ? relativePath.Substring(slash + 1) : string.Empty;
                Transform anchor = FindDescendantByName(root.transform, head);
                if (anchor != null)
                {
                    Transform found = string.IsNullOrEmpty(tail) ? anchor : anchor.Find(tail);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static Transform FindTransformByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDescendantByName(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDescendantByName(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDescendantByName(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
