using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.LevelUp;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint2SetupTool
    {
        private const string CARDS_DIR = "Assets/_ScriptableObjects/Cards";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        public static void RunAll()
        {
            CreateAndWireModifierCards();
            VoxelIconGenerator.GenerateAllIcons();
            SetupGameplayScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint2SetupTool] All Sprint 2 modifier cards, icons, card UI polish, reroll, and pause menu setup completed successfully.");
        }

        private static void CreateAndWireModifierCards()
        {
            var specs = new[]
            {
                new ModifierSpec(
                    "OrbitalSword_SpeedAndRadius",
                    "OrbitalSmallSwordCard",
                    "orbital_small_sword",
                    "Orbital Sword: Rapid Reach",
                    "+40 Orbit Speed & +0.6 Radius.",
                    CardRarity.Rare,
                    ModifierType.Speed, 40f,
                    hasSecondary: true, ModifierType.Radius, 0.6f),

                new ModifierSpec(
                    "ShooterMod_Count",
                    "ShooterCard",
                    "Shooter",
                    "Shooter: +1 Projectile",
                    "Fires 1 additional homing projectile.",
                    CardRarity.Rare,
                    ModifierType.Count, 1f),

                new ModifierSpec(
                    "ShooterMod_Cooldown",
                    "ShooterCard",
                    "Shooter",
                    "Shooter: Rapid Fire",
                    "Reduces fire cooldown by 0.18s.",
                    CardRarity.Common,
                    ModifierType.Cooldown, 0.18f),

                new ModifierSpec(
                    "FireballMod_Count",
                    "FireballCard",
                    "Fireball",
                    "Fireball: +1 Fireball",
                    "Launches 1 additional explosive fireball.",
                    CardRarity.Rare,
                    ModifierType.Count, 1f),

                new ModifierSpec(
                    "FireballMod_Area",
                    "FireballCard",
                    "Fireball",
                    "Fireball: Wider Blast",
                    "Increases explosion radius by +1.2m.",
                    CardRarity.Rare,
                    ModifierType.Area, 1.2f),

                new ModifierSpec(
                    "BoomerangMod_Count",
                    "BoomerangCard",
                    "Boomerang",
                    "Boomerang: +1 Boomerang",
                    "Throws 1 additional boomerang in a fan.",
                    CardRarity.Rare,
                    ModifierType.Count, 1f),

                new ModifierSpec(
                    "BoomerangMod_Pierce",
                    "BoomerangCard",
                    "Boomerang",
                    "Boomerang: Heavy Throw",
                    "+3 Pierce & +2m throw range.",
                    CardRarity.Common,
                    ModifierType.PierceCount, 3f,
                    hasSecondary: true, ModifierType.Radius, 2f),

                new ModifierSpec(
                    "IceAuraMod_Radius",
                    "IceAuraCard",
                    "IceAura",
                    "Ice Aura: Expanding Chill",
                    "Increases aura radius by +1.0m.",
                    CardRarity.Common,
                    ModifierType.Radius, 1.0f),

                new ModifierSpec(
                    "IceAuraMod_Slow",
                    "IceAuraCard",
                    "IceAura",
                    "Ice Aura: Deep Freeze",
                    "Increases slow power by +15%.",
                    CardRarity.Rare,
                    ModifierType.SlowAmount, 0.15f),

                new ModifierSpec(
                    "BlackHoleMod_Radius",
                    "BlackHoleCard",
                    "BlackHole",
                    "Black Hole: Event Horizon",
                    "Increases pull radius by +1.2m.",
                    CardRarity.Epic,
                    ModifierType.Radius, 1.2f),

                new ModifierSpec(
                    "BlackHoleMod_Force",
                    "BlackHoleCard",
                    "BlackHole",
                    "Black Hole: Gravity Surge",
                    "Increases pull force by +1.5.",
                    CardRarity.Rare,
                    ModifierType.Speed, 1.5f),
            };

            foreach (var spec in specs)
            {
                string modPath = $"{CARDS_DIR}/{spec.AssetName}.asset";
                var modCard = AssetDatabase.LoadAssetAtPath<AbilityModifierCardData>(modPath);
                if (modCard == null)
                {
                    modCard = ScriptableObject.CreateInstance<AbilityModifierCardData>();
                    AssetDatabase.CreateAsset(modCard, modPath);
                }

                modCard.cardName = spec.CardName;
                modCard.description = spec.Description;
                modCard.rarity = spec.Rarity;
                modCard.isConsumable = false;
                modCard.targetAbilityId = spec.TargetAbilityId;
                modCard.modifierType = spec.PrimaryType;
                modCard.value = spec.PrimaryValue;
                modCard.hasSecondaryModifier = spec.HasSecondary;
                modCard.secondaryModifierType = spec.SecondaryType;
                modCard.secondaryValue = spec.SecondaryValue;
                EditorUtility.SetDirty(modCard);

                string parentPath = $"{CARDS_DIR}/{spec.ParentAbilityCardName}.asset";
                var parentCard = AssetDatabase.LoadAssetAtPath<AbilityCardData>(parentPath);
                if (parentCard != null)
                {
                    if (parentCard.unlockedModifiers == null)
                    {
                        parentCard.unlockedModifiers = new List<CardData>();
                    }
                    if (!parentCard.unlockedModifiers.Contains(modCard))
                    {
                        parentCard.unlockedModifiers.Add(modCard);
                        EditorUtility.SetDirty(parentCard);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Sprint2SetupTool] Parent AbilityCardData not found at {parentPath}");
                }
            }
        }

        private static void SetupGameplayScene()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                Canvas canvas = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    canvas = root.GetComponentInChildren<Canvas>(true);
                    if (canvas != null) break;
                }

                if (canvas == null)
                {
                    Debug.LogError("[Sprint2SetupTool] Canvas not found in Gameplay.unity");
                    return;
                }

                TMP_FontAsset defaultFont = ResolveDefaultFont(canvas);
                SetupCardSelectionPolish(canvas, defaultFont);
                SetupPauseMenuAndButton(canvas, defaultFont);
                SetupAbilityHudRows(canvas, defaultFont);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static TMP_FontAsset ResolveDefaultFont(Canvas canvas)
        {
            var existingTmp = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingTmp != null && existingTmp.font != null)
            {
                return existingTmp.font;
            }
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private static void SetupCardSelectionPolish(Canvas canvas, TMP_FontAsset defaultFont)
        {
            Transform selectionPanel = canvas.transform.Find("CardSelectionPanel");
            if (selectionPanel == null)
            {
                Debug.LogWarning("[Sprint2SetupTool] CardSelectionPanel not found under Canvas.");
                return;
            }

            Transform cardsContainer = selectionPanel.Find("CardsContainer");
            if (cardsContainer != null)
            {
                var containerRt = (RectTransform)cardsContainer;
                containerRt.anchoredPosition = new Vector2(0f, 50f);

                CardUI[] slots = cardsContainer.GetComponentsInChildren<CardUI>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    PolishCardSlot(slots[i], defaultFont);
                }
            }

            // Reroll button under CardSelectionPanel
            Transform rerollTf = selectionPanel.Find("RerollButton");
            GameObject rerollGo;
            if (rerollTf == null)
            {
                rerollGo = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button));
                rerollGo.transform.SetParent(selectionPanel, false);
            }
            else
            {
                rerollGo = rerollTf.gameObject;
            }

            var rerollRt = (RectTransform)rerollGo.transform;
            rerollRt.anchorMin = new Vector2(0.5f, 0.5f);
            rerollRt.anchorMax = new Vector2(0.5f, 0.5f);
            rerollRt.pivot = new Vector2(0.5f, 0.5f);
            rerollRt.anchoredPosition = new Vector2(0f, -680f);
            rerollRt.sizeDelta = new Vector2(460f, 96f);

            var rerollImg = EnsureComponent<Image>(rerollGo);
            rerollImg.color = new Color(0.18f, 0.48f, 0.72f, 1f);
            var rerollBtn = EnsureComponent<Button>(rerollGo);
            rerollBtn.targetGraphic = rerollImg;

            var rerollText = EnsureChildTmp(rerollGo.transform, "Text", defaultFont, "REROLL (FREE x1)", 32f, Color.white, FontStyles.Bold);
            rerollText.enableAutoSizing = true;
            rerollText.fontSizeMin = 20f;
            rerollText.fontSizeMax = 32f;
            rerollText.raycastTarget = false;

            var selectionUi = canvas.GetComponent<CardSelectionUI>();
            if (selectionUi != null)
            {
                var so = new SerializedObject(selectionUi);
                so.FindProperty("rerollButton").objectReferenceValue = rerollBtn;
                so.FindProperty("rerollButtonText").objectReferenceValue = rerollText;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(selectionUi);
            }
        }

        private static void PolishCardSlot(CardUI cardUi, TMP_FontAsset defaultFont)
        {
            if (cardUi == null) return;

            Transform cardTf = cardUi.transform;

            // 1. Level badge pill at top
            Transform badgeTf = cardTf.Find("LevelBadge");
            GameObject badgeGo;
            if (badgeTf == null)
            {
                badgeGo = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(cardTf, false);
            }
            else
            {
                badgeGo = badgeTf.gameObject;
            }

            var badgeRt = (RectTransform)badgeGo.transform;
            badgeRt.anchorMin = new Vector2(0.14f, 0.89f);
            badgeRt.anchorMax = new Vector2(0.86f, 0.97f);
            badgeRt.offsetMin = Vector2.zero;
            badgeRt.offsetMax = Vector2.zero;

            var badgeImg = EnsureComponent<Image>(badgeGo);
            badgeImg.color = new Color(0.16f, 0.56f, 0.88f, 1f);
            badgeImg.raycastTarget = false;

            var badgeTmp = EnsureChildTmp(badgeGo.transform, "Text", defaultFont, "Lv 1 -> Lv 2", 24f, Color.white, FontStyles.Bold);
            badgeTmp.enableAutoSizing = true;
            badgeTmp.fontSizeMin = 16f;
            badgeTmp.fontSizeMax = 24f;
            badgeTmp.raycastTarget = false;

            // 2. Title positioning
            Transform titleTf = cardTf.Find("Title");
            if (titleTf != null)
            {
                var titleRt = (RectTransform)titleTf;
                titleRt.anchorMin = new Vector2(0.06f, 0.72f);
                titleRt.anchorMax = new Vector2(0.94f, 0.88f);
                titleRt.offsetMin = Vector2.zero;
                titleRt.offsetMax = Vector2.zero;

                var titleTmp = titleTf.GetComponent<TextMeshProUGUI>();
                if (titleTmp != null)
                {
                    if (defaultFont != null && titleTmp.font == null) titleTmp.font = defaultFont;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.enableAutoSizing = true;
                    titleTmp.fontSizeMin = 20f;
                    titleTmp.fontSizeMax = 34f;
                    titleTmp.raycastTarget = false;
                }
            }

            // 3. Center Icon
            Transform iconTf = cardTf.Find("Icon");
            GameObject iconGo;
            if (iconTf == null)
            {
                iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(cardTf, false);
            }
            else
            {
                iconGo = iconTf.gameObject;
            }

            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.5f, 0.54f);
            iconRt.anchorMax = new Vector2(0.5f, 0.54f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(168f, 168f);

            var iconImg = EnsureComponent<Image>(iconGo);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 4. Description positioning
            Transform descTf = cardTf.Find("Description");
            if (descTf != null)
            {
                var descRt = (RectTransform)descTf;
                descRt.anchorMin = new Vector2(0.08f, 0.14f);
                descRt.anchorMax = new Vector2(0.92f, 0.39f);
                descRt.offsetMin = Vector2.zero;
                descRt.offsetMax = Vector2.zero;

                var descTmp = descTf.GetComponent<TextMeshProUGUI>();
                if (descTmp != null)
                {
                    if (defaultFont != null && descTmp.font == null) descTmp.font = defaultFont;
                    descTmp.alignment = TextAlignmentOptions.Center;
                    descTmp.enableAutoSizing = true;
                    descTmp.fontSizeMin = 18f;
                    descTmp.fontSizeMax = 26f;
                    descTmp.raycastTarget = false;
                }
            }

            // Wire serialized fields on CardUI
            var so = new SerializedObject(cardUi);
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("levelBadgeObject").objectReferenceValue = badgeGo;
            so.FindProperty("levelBadgeText").objectReferenceValue = badgeTmp;
            so.FindProperty("levelBadgeBg").objectReferenceValue = badgeImg;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cardUi);
        }

        private static void SetupPauseMenuAndButton(Canvas canvas, TMP_FontAsset defaultFont)
        {
            Transform safeArea = canvas.transform.Find("SafeAreaHUD");
            if (safeArea == null)
            {
                Debug.LogWarning("[Sprint2SetupTool] SafeAreaHUD not found under Canvas.");
                return;
            }

            // Shift CoinHud left to make clean room for PauseButton in top-right corner
            Transform coinHud = safeArea.Find("CoinHud");
            if (coinHud != null)
            {
                var coinRt = (RectTransform)coinHud;
                coinRt.anchoredPosition = new Vector2(-112f, -66f);
            }

            // PauseButton (||) in top-right corner of SafeAreaHUD
            Transform pauseBtnTf = safeArea.Find("PauseButton");
            GameObject pauseBtnGo;
            if (pauseBtnTf == null)
            {
                pauseBtnGo = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
                pauseBtnGo.transform.SetParent(safeArea, false);
            }
            else
            {
                pauseBtnGo = pauseBtnTf.gameObject;
            }

            var pbRt = (RectTransform)pauseBtnGo.transform;
            pbRt.anchorMin = new Vector2(1f, 1f);
            pbRt.anchorMax = new Vector2(1f, 1f);
            pbRt.pivot = new Vector2(1f, 1f);
            pbRt.anchoredPosition = new Vector2(-20f, -60f);
            pbRt.sizeDelta = new Vector2(76f, 66f);

            var pbImg = EnsureComponent<Image>(pauseBtnGo);
            pbImg.color = new Color(0.12f, 0.14f, 0.2f, 0.88f);
            var pbOutline = EnsureComponent<Outline>(pauseBtnGo);
            pbOutline.effectColor = new Color(1f, 1f, 1f, 0.28f);
            pbOutline.effectDistance = new Vector2(2f, -2f);
            var pauseButton = EnsureComponent<Button>(pauseBtnGo);
            pauseButton.targetGraphic = pbImg;

            EnsurePauseBar(pauseBtnGo.transform, "BarLeft", new Vector2(-10f, 0f));
            EnsurePauseBar(pauseBtnGo.transform, "BarRight", new Vector2(10f, 0f));

            // PauseMenu root under Canvas (MUST stay active so VContainer injection & Escape key Update run)
            Transform pauseMenuTf = canvas.transform.Find("PauseMenu");
            GameObject pauseMenuGo;
            if (pauseMenuTf == null)
            {
                pauseMenuGo = new GameObject("PauseMenu", typeof(RectTransform), typeof(PauseMenuUI));
                pauseMenuGo.transform.SetParent(canvas.transform, false);
            }
            else
            {
                pauseMenuGo = pauseMenuTf.gameObject;
            }

            pauseMenuGo.SetActive(true);
            StretchFullScreen((RectTransform)pauseMenuGo.transform);

            // Order PauseMenu right before GameOverPanel so GameOver sits above it, and PausePanel sits above HUD/CardSelection
            Transform gameOverTf = canvas.transform.Find("GameOverPanel");
            if (gameOverTf != null)
            {
                int targetIdx = gameOverTf.GetSiblingIndex();
                if (pauseMenuGo.transform.GetSiblingIndex() > targetIdx)
                {
                    pauseMenuGo.transform.SetSiblingIndex(targetIdx);
                }
                else
                {
                    pauseMenuGo.transform.SetSiblingIndex(Mathf.Max(0, targetIdx - 1));
                }
            }

            var pauseMenuUi = EnsureComponent<PauseMenuUI>(pauseMenuGo);

            // Child PausePanel (starts inactive)
            Transform pausePanelTf = pauseMenuGo.transform.Find("PausePanel");
            GameObject pausePanelGo;
            if (pausePanelTf == null)
            {
                pausePanelGo = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
                pausePanelGo.transform.SetParent(pauseMenuGo.transform, false);
            }
            else
            {
                pausePanelGo = pausePanelTf.gameObject;
            }
            StretchFullScreen((RectTransform)pausePanelGo.transform);
            var dimImg = EnsureComponent<Image>(pausePanelGo);
            dimImg.color = new Color(0f, 0f, 0f, 0.78f);
            dimImg.raycastTarget = true;

            // Window inside PausePanel
            Transform windowTf = pausePanelGo.transform.Find("Window");
            GameObject windowGo;
            if (windowTf == null)
            {
                windowGo = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
                windowGo.transform.SetParent(pausePanelGo.transform, false);
            }
            else
            {
                windowGo = windowTf.gameObject;
            }

            var windowRt = (RectTransform)windowGo.transform;
            windowRt.anchorMin = new Vector2(0.5f, 0.5f);
            windowRt.anchorMax = new Vector2(0.5f, 0.5f);
            windowRt.pivot = new Vector2(0.5f, 0.5f);
            windowRt.anchoredPosition = Vector2.zero;
            windowRt.sizeDelta = new Vector2(820f, 760f);

            var winImg = EnsureComponent<Image>(windowGo);
            winImg.color = new Color(0.11f, 0.13f, 0.19f, 0.97f);
            var winOutline = EnsureComponent<Outline>(windowGo);
            winOutline.effectColor = new Color(0.35f, 0.42f, 0.58f, 0.85f);
            winOutline.effectDistance = new Vector2(3f, -3f);

            var vlg = EnsureComponent<VerticalLayoutGroup>(windowGo);
            vlg.padding = new RectOffset(44, 44, 42, 42);
            vlg.spacing = 28f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            EnsureLayoutLabel(windowGo.transform, "Title", "PAUSED", defaultFont, 56f, 72f, Color.white, TextAlignmentOptions.Center);

            // Music & SFX slider rows
            EnsureSliderRow(
                windowGo.transform,
                "MusicVolumeRow",
                "Music Volume",
                defaultFont,
                out Slider musicSlider,
                out TextMeshProUGUI musicValueText);

            EnsureSliderRow(
                windowGo.transform,
                "SfxVolumeRow",
                "SFX Volume",
                defaultFont,
                out Slider sfxSlider,
                out TextMeshProUGUI sfxValueText);

            // Resume & Quit buttons
            Button resumeButton = EnsureWindowButton(
                windowGo.transform,
                "ResumeButton",
                "RESUME",
                new Color(0.18f, 0.65f, 0.34f, 1f),
                38f,
                112f,
                defaultFont);

            Button quitButton = EnsureWindowButton(
                windowGo.transform,
                "QuitToMenuButton",
                "QUIT TO MENU",
                new Color(0.68f, 0.22f, 0.24f, 1f),
                36f,
                112f,
                defaultFont);

            pausePanelGo.SetActive(false);

            // Wire PauseMenuUI references
            var so = new SerializedObject(pauseMenuUi);
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("pausePanel").objectReferenceValue = pausePanelGo;
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("musicValueText").objectReferenceValue = musicValueText;
            so.FindProperty("sfxValueText").objectReferenceValue = sfxValueText;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("quitToMenuButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pauseMenuUi);
        }

        private static void SetupAbilityHudRows(Canvas canvas, TMP_FontAsset defaultFont)
        {
            Transform safeArea = canvas.transform.Find("SafeAreaHUD");
            if (safeArea == null)
            {
                Debug.LogWarning("[Sprint2SetupTool] SafeAreaHUD not found under Canvas.");
                return;
            }

            Transform abilityHudTf = safeArea.Find("AbilityHud");
            if (abilityHudTf == null)
            {
                Debug.LogWarning("[Sprint2SetupTool] AbilityHud not found under SafeAreaHUD.");
                return;
            }

            var hudRt = (RectTransform)abilityHudTf;
            hudRt.anchoredPosition = new Vector2(0f, -142f);
            hudRt.sizeDelta = new Vector2(-40f, 76f);

            var oldVlg = abilityHudTf.GetComponent<VerticalLayoutGroup>();
            if (oldVlg != null)
            {
                Object.DestroyImmediate(oldVlg);
            }

            Transform oldActiveRow = abilityHudTf.Find("ActiveRow");
            if (oldActiveRow != null) Object.DestroyImmediate(oldActiveRow.gameObject);
            Transform oldPassiveRow = abilityHudTf.Find("PassiveRow");
            if (oldPassiveRow != null) Object.DestroyImmediate(oldPassiveRow.gameObject);

            var hlg = EnsureComponent<HorizontalLayoutGroup>(abilityHudTf.gameObject);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Transform activeSlots = EnsureHorizontalSlotGroup(abilityHudTf, "ActiveSlots", 0);
            EnsureDivider(abilityHudTf, "Divider", 1);
            Transform passiveSlots = EnsureHorizontalSlotGroup(abilityHudTf, "PassiveSlots", 2);

            var hudUi = abilityHudTf.GetComponent<AbilityHudUI>();
            if (hudUi != null)
            {
                var so = new SerializedObject(hudUi);
                so.FindProperty("activeSlotsContainer").objectReferenceValue = activeSlots;
                so.FindProperty("passiveSlotsContainer").objectReferenceValue = passiveSlots;
                so.FindProperty("slotSize").floatValue = 68f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hudUi);
            }

            Transform bossBarTf = safeArea.Find("BossHealthBar");
            if (bossBarTf != null)
            {
                var bossRt = (RectTransform)bossBarTf;
                bossRt.anchoredPosition = new Vector2(bossRt.anchoredPosition.x, -236f);
            }
        }

        private static Transform EnsureHorizontalSlotGroup(Transform parent, string groupName, int siblingIndex)
        {
            Transform tf = parent.Find(groupName);
            GameObject go = tf != null
                ? tf.gameObject
                : new GameObject(groupName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            if (tf == null) go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(siblingIndex);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(438f, 68f);

            var hlg = EnsureComponent<HorizontalLayoutGroup>(go);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            return go.transform;
        }

        private static void EnsureDivider(Transform parent, string dividerName, int siblingIndex)
        {
            Transform tf = parent.Find(dividerName);
            GameObject go = tf != null
                ? tf.gameObject
                : new GameObject(dividerName, typeof(RectTransform), typeof(Image));
            if (tf == null) go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(siblingIndex);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(3f, 48f);

            var img = EnsureComponent<Image>(go);
            img.color = new Color(1f, 1f, 1f, 0.28f);
            img.raycastTarget = false;
        }

        private static void EnsurePauseBar(Transform parent, string barName, Vector2 pos)
        {
            Transform barTf = parent.Find(barName);
            GameObject barGo = barTf != null ? barTf.gameObject : new GameObject(barName, typeof(RectTransform), typeof(Image));
            if (barTf == null) barGo.transform.SetParent(parent, false);

            var rt = (RectTransform)barGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(10f, 32f);

            var img = EnsureComponent<Image>(barGo);
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private static void EnsureLayoutLabel(
            Transform parent,
            string goName,
            string text,
            TMP_FontAsset font,
            float fontSize,
            float height,
            Color color,
            TextAlignmentOptions alignment)
        {
            Transform tf = parent.Find(goName);
            GameObject go = tf != null ? tf.gameObject : new GameObject(goName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            if (tf == null) go.transform.SetParent(parent, false);

            var le = EnsureComponent<LayoutElement>(go);
            le.preferredHeight = height;
            le.minHeight = height;

            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
        }

        private static void EnsureSliderRow(
            Transform parent,
            string rowName,
            string label,
            TMP_FontAsset font,
            out Slider slider,
            out TextMeshProUGUI valueText)
        {
            Transform rowTf = parent.Find(rowName);
            GameObject rowGo = rowTf != null ? rowTf.gameObject : new GameObject(rowName, typeof(RectTransform), typeof(LayoutElement));
            if (rowTf == null) rowGo.transform.SetParent(parent, false);

            var le = EnsureComponent<LayoutElement>(rowGo);
            le.preferredHeight = 110f;
            le.minHeight = 110f;

            // Label (top-left)
            Transform labelTf = rowGo.transform.Find("Label");
            GameObject labelGo = labelTf != null ? labelTf.gameObject : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (labelTf == null) labelGo.transform.SetParent(rowGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0.55f);
            labelRt.anchorMax = new Vector2(0.72f, 1f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelTmp = EnsureComponent<TextMeshProUGUI>(labelGo);
            if (font != null) labelTmp.font = font;
            labelTmp.text = label;
            labelTmp.fontSize = 32f;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.color = new Color(0.9f, 0.93f, 0.98f, 1f);
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.raycastTarget = false;

            // Value (top-right)
            Transform valueTf = rowGo.transform.Find("Value");
            GameObject valueGo = valueTf != null ? valueTf.gameObject : new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (valueTf == null) valueGo.transform.SetParent(rowGo.transform, false);
            var valueRt = (RectTransform)valueGo.transform;
            valueRt.anchorMin = new Vector2(0.72f, 0.55f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.offsetMin = Vector2.zero;
            valueRt.offsetMax = Vector2.zero;
            valueText = EnsureComponent<TextMeshProUGUI>(valueGo);
            if (font != null) valueText.font = font;
            valueText.text = "100%";
            valueText.fontSize = 32f;
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = new Color(0.55f, 0.85f, 1f, 1f);
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.raycastTarget = false;

            // Slider (bottom half)
            Transform sliderTf = rowGo.transform.Find("Slider");
            GameObject sliderGo = sliderTf != null ? sliderTf.gameObject : new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            if (sliderTf == null) sliderGo.transform.SetParent(rowGo.transform, false);
            var sliderRt = (RectTransform)sliderGo.transform;
            sliderRt.anchorMin = new Vector2(0f, 0.05f);
            sliderRt.anchorMax = new Vector2(1f, 0.5f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            slider = EnsureComponent<Slider>(sliderGo);

            Transform bgTf = sliderGo.transform.Find("Background");
            GameObject bgGo = bgTf != null ? bgTf.gameObject : new GameObject("Background", typeof(RectTransform), typeof(Image));
            if (bgTf == null) bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            EnsureComponent<Image>(bgGo).color = new Color(0.06f, 0.08f, 0.12f, 1f);

            Transform fillAreaTf = sliderGo.transform.Find("Fill Area");
            GameObject fillAreaGo = fillAreaTf != null ? fillAreaTf.gameObject : new GameObject("Fill Area", typeof(RectTransform));
            if (fillAreaTf == null) fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = (RectTransform)fillAreaGo.transform;
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(10f, 0f);
            fillAreaRt.offsetMax = new Vector2(-10f, 0f);

            Transform fillTf = fillAreaGo.transform.Find("Fill");
            GameObject fillGo = fillTf != null ? fillTf.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(Image));
            if (fillTf == null) fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.sizeDelta = new Vector2(10f, 0f);
            EnsureComponent<Image>(fillGo).color = new Color(0.24f, 0.66f, 0.94f, 1f);

            Transform handleAreaTf = sliderGo.transform.Find("Handle Slide Area");
            GameObject handleAreaGo = handleAreaTf != null ? handleAreaTf.gameObject : new GameObject("Handle Slide Area", typeof(RectTransform));
            if (handleAreaTf == null) handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = (RectTransform)handleAreaGo.transform;
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(18f, 0f);
            handleAreaRt.offsetMax = new Vector2(-18f, 0f);

            Transform handleTf = handleAreaGo.transform.Find("Handle");
            GameObject handleGo = handleTf != null ? handleTf.gameObject : new GameObject("Handle", typeof(RectTransform), typeof(Image));
            if (handleTf == null) handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;
            handleRt.sizeDelta = new Vector2(36f, 0f);
            var handleImg = EnsureComponent<Image>(handleGo);
            handleImg.color = new Color(0.95f, 0.97f, 1f, 1f);

            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(1f);
        }

        private static Button EnsureWindowButton(
            Transform parent,
            string goName,
            string label,
            Color bgColor,
            float fontSize,
            float height,
            TMP_FontAsset font)
        {
            Transform tf = parent.Find(goName);
            GameObject go = tf != null ? tf.gameObject : new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            if (tf == null) go.transform.SetParent(parent, false);

            var le = EnsureComponent<LayoutElement>(go);
            le.preferredHeight = height;
            le.minHeight = height;

            var img = EnsureComponent<Image>(go);
            img.color = bgColor;
            var btn = EnsureComponent<Button>(go);
            btn.targetGraphic = img;

            var tmp = EnsureChildTmp(go.transform, "Text", font, label, fontSize, Color.white, FontStyles.Bold);
            tmp.raycastTarget = false;
            return btn;
        }

        private static TextMeshProUGUI EnsureChildTmp(
            Transform parent,
            string childName,
            TMP_FontAsset font,
            string text,
            float fontSize,
            Color color,
            FontStyles style)
        {
            Transform tf = parent.Find(childName);
            GameObject go = tf != null ? tf.gameObject : new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (tf == null) go.transform.SetParent(parent, false);

            StretchFullScreen((RectTransform)go.transform);
            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp != null ? comp : go.AddComponent<T>();
        }

        private readonly struct ModifierSpec
        {
            public readonly string AssetName;
            public readonly string ParentAbilityCardName;
            public readonly string TargetAbilityId;
            public readonly string CardName;
            public readonly string Description;
            public readonly CardRarity Rarity;
            public readonly ModifierType PrimaryType;
            public readonly float PrimaryValue;
            public readonly bool HasSecondary;
            public readonly ModifierType SecondaryType;
            public readonly float SecondaryValue;

            public ModifierSpec(
                string assetName,
                string parentAbilityCardName,
                string targetAbilityId,
                string cardName,
                string description,
                CardRarity rarity,
                ModifierType primaryType,
                float primaryValue,
                bool hasSecondary = false,
                ModifierType secondaryType = default,
                float secondaryValue = 0f)
            {
                AssetName = assetName;
                ParentAbilityCardName = parentAbilityCardName;
                TargetAbilityId = targetAbilityId;
                CardName = cardName;
                Description = description;
                Rarity = rarity;
                PrimaryType = primaryType;
                PrimaryValue = primaryValue;
                HasSecondary = hasSecondary;
                SecondaryType = secondaryType;
                SecondaryValue = secondaryValue;
            }
        }
    }
}