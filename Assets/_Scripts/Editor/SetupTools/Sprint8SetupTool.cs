using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    public static class Sprint8SetupTool
    {
        private const string SYNERGIES_DIR = "Assets/_ScriptableObjects/Synergies";
        private const string CARDS_DIR = "Assets/_ScriptableObjects/Cards";
        private const string SYNERGY_CATALOG_PATH = "Assets/Resources/SynergyCatalog.asset";
        private const string ABILITY_CATALOG_PATH = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";
        private const string HERO_CATALOG_PATH = "Assets/Resources/HeroCatalog.asset";
        private const string HERO_PREFAB_PATH = "Assets/_Prefabs/Hero.prefab";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        private const string MAINMENU_SCENE_PATH = "Assets/_Scenes/MainMenu.unity";

        private struct SynergySpec
        {
            public string AssetName;
            public string CardAssetName;
            public string Id;
            public string DisplayName;
            public string Description;
            public CardRarity Rarity;
            public string[] RequiredIds;
        }

        [MenuItem("ProjectB/Setup/Run Sprint 8 Synergies, Combo & Breakdown Setup")]
        public static void RunAll()
        {
            List<SynergyData> synergies = UpdateSynergiesAndCards();
            SynergyCatalog synergyCatalog = CreateOrUpdateSynergyCatalog(synergies);
            WireHeroPrefab(synergies);
            WireGameplayScene(synergies, synergyCatalog);
            WireMainMenuScene(synergyCatalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint8SetupTool] Sprint 8 reactive synergies, SynergyCatalog, ComboHudUI, and Damage Breakdown wiring completed.");
        }

        private static List<SynergyData> UpdateSynergiesAndCards()
        {
            var specs = new[]
            {
                new SynergySpec
                {
                    AssetName = "Synergy_ToxicInferno",
                    CardAssetName = "SynergyCard_ToxicInferno",
                    Id = SynergyIds.ToxicInferno,
                    DisplayName = "Toxic Inferno",
                    Description = "Poison Flask + Fireball: Fireball blasts ignite Poison Puddles into exploding napalm fields dealing 1.6x detonation and 2x burn damage.",
                    Rarity = CardRarity.Epic,
                    RequiredIds = new[] { "PoisonAlchemist", "Fireball" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_SteamBlast",
                    CardAssetName = "SynergyCard_SteamBlast",
                    Id = SynergyIds.SteamBlast,
                    DisplayName = "Steam Blast",
                    Description = "Fireball + Ice Aura: Hitting any slowed enemy with Fireball triggers a 4.2m Steam Nova dealing 1.8x bonus AoE damage and knockback.",
                    Rarity = CardRarity.Legendary,
                    RequiredIds = new[] { "Fireball", "IceAura" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_Thunderstorm",
                    CardAssetName = "SynergyCard_Thunderstorm",
                    Id = SynergyIds.Thunderstorm,
                    DisplayName = "Thunderstorm",
                    Description = "Chain Lightning + Ice Aura: Striking slowed enemies triggers a 90% deep freeze (0.65s) and fires 2 extra icy lightning arcs.",
                    Rarity = CardRarity.Legendary,
                    RequiredIds = new[] { "ChainLightning", "IceAura" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_OverchargedGrid",
                    CardAssetName = "SynergyCard_OverchargedGrid",
                    Id = SynergyIds.OverchargedGrid,
                    DisplayName = "Overcharged Grid",
                    Description = "Laser Turret + Shooter: Active turrets link via high-voltage laser fences (<=12m), while passing Shooter projectiles gain +2 pierce.",
                    Rarity = CardRarity.Legendary,
                    RequiredIds = new[] { "LaserTurret", "Shooter" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_BladeStorm",
                    CardAssetName = "SynergyCard_BladeStorm",
                    Id = SynergyIds.BladeStorm,
                    DisplayName = "Blade Storm",
                    Description = "Orbital Sword + Boomerang: Orbital swords launch piercing spectral blades every 2.8s and on every 3rd Boomerang hit.",
                    Rarity = CardRarity.Epic,
                    RequiredIds = new[] { "orbital_small_sword", "Boomerang" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_AegisPhalanx",
                    CardAssetName = "SynergyCard_AegisPhalanx",
                    Id = SynergyIds.AegisPhalanx,
                    DisplayName = "Aegis Phalanx",
                    Description = "Orbital Sword + Guardian Shields: Knocking enemies back with Guardian Shields unleashes a 2.4m Phalanx shockwave toward the nearest sword.",
                    Rarity = CardRarity.Epic,
                    RequiredIds = new[] { "orbital_small_sword", "GuardianShield" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_DragonLord",
                    CardAssetName = "SynergyCard_DragonLord",
                    Id = SynergyIds.DragonLord,
                    DisplayName = "Dragon Lord",
                    Description = "Dragon Whelp + Battle Fury: Dragon Whelps breathe Fury flames that pierce 3 enemies and leave burning embers underneath hit targets.",
                    Rarity = CardRarity.Legendary,
                    RequiredIds = new[] { "MiniDragon", "BattleFury" }
                },
                new SynergySpec
                {
                    AssetName = "Synergy_CataclysmVortex",
                    CardAssetName = "SynergyCard_CataclysmVortex",
                    Id = SynergyIds.CataclysmVortex,
                    DisplayName = "Cataclysm Vortex",
                    Description = "Black Hole + Arcane Tornado: Tornadoes passing through Black Hole (or expiring) destabilize into 6 explosive gravity shards.",
                    Rarity = CardRarity.Legendary,
                    RequiredIds = new[] { "BlackHole", "VoxelTornado" }
                }
            };

            var list = new List<SynergyData>(specs.Length);

            for (int i = 0; i < specs.Length; i++)
            {
                SynergySpec spec = specs[i];
                string synPath = $"{SYNERGIES_DIR}/{spec.AssetName}.asset";
                string cardPath = $"{CARDS_DIR}/{spec.CardAssetName}.asset";

                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(synPath);
                if (syn == null)
                {
                    syn = ScriptableObject.CreateInstance<SynergyData>();
                    AssetDatabase.CreateAsset(syn, synPath);
                }

                var card = AssetDatabase.LoadAssetAtPath<SynergyCardData>(cardPath);
                if (card != null)
                {
                    card.cardName = $"Synergy: {spec.DisplayName}";
                    card.description = spec.Description;
                    card.rarity = spec.Rarity;
                    card.synergyId = spec.Id;
                    card.abilityBonuses.Clear();
                    card.globalDamageBonusPercent = 0f;
                    card.armorBonus = 0f;
                    EditorUtility.SetDirty(card);
                }

                syn.id = spec.Id;
                syn.synergyName = spec.DisplayName;
                syn.description = spec.Description;
                syn.rarity = spec.Rarity;
                if (card != null && card.icon != null)
                {
                    syn.icon = card.icon;
                }
                syn.requiredAbilityIds = new List<string>(spec.RequiredIds);
                syn.rewardCard = card;
                EditorUtility.SetDirty(syn);

                list.Add(syn);
            }

            return list;
        }

        private static SynergyCatalog CreateOrUpdateSynergyCatalog(List<SynergyData> synergies)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SynergyCatalog>(SYNERGY_CATALOG_PATH);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SynergyCatalog>();
                AssetDatabase.CreateAsset(catalog, SYNERGY_CATALOG_PATH);
            }

            catalog.synergies = new List<SynergyData>(synergies);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void WireHeroPrefab(List<SynergyData> synergies)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HERO_PREFAB_PATH);
            if (root == null) return;

            try
            {
                var heroAbilities = root.GetComponent<HeroAbilities>();
                if (heroAbilities != null)
                {
                    var so = new SerializedObject(heroAbilities);
                    WriteSynergyListProperty(so.FindProperty("availableSynergies"), synergies);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(heroAbilities);
                }
                PrefabUtility.SaveAsPrefabAsset(root, HERO_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireGameplayScene(List<SynergyData> synergies, SynergyCatalog synergyCatalog)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                Canvas canvas = null;
                HeroAbilities heroAbilities = null;
                UpgradeManager upgradeMgr = null;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
                    if (heroAbilities == null) heroAbilities = root.GetComponentInChildren<HeroAbilities>(true);
                    if (upgradeMgr == null) upgradeMgr = root.GetComponentInChildren<UpgradeManager>(true);
                }

                if (heroAbilities != null)
                {
                    var heroSo = new SerializedObject(heroAbilities);
                    WriteSynergyListProperty(heroSo.FindProperty("availableSynergies"), synergies);
                    heroSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(heroAbilities);
                }

                if (upgradeMgr != null)
                {
                    upgradeMgr.synergies = new List<SynergyData>(synergies);
                    EditorUtility.SetDirty(upgradeMgr);
                }

                if (canvas != null)
                {
                    TMP_FontAsset defaultFont = ResolveDefaultFont(canvas);
                    SetupComboHud(canvas, defaultFont);
                    SetupGameOverBreakdownLayout(canvas, defaultFont, synergyCatalog);
                }

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

        private static void WireMainMenuScene(SynergyCatalog synergyCatalog)
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(MAINMENU_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(MAINMENU_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                AbilitiesScreenUI abilitiesScreen = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    abilitiesScreen = root.GetComponentInChildren<AbilitiesScreenUI>(true);
                    if (abilitiesScreen != null) break;
                }

                if (abilitiesScreen != null)
                {
                    var so = new SerializedObject(abilitiesScreen);
                    so.FindProperty("synergyCatalog").objectReferenceValue = synergyCatalog;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(abilitiesScreen);
                }

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

        private static void SetupComboHud(Canvas canvas, TMP_FontAsset defaultFont)
        {
            Transform safeArea = canvas.transform.Find("SafeAreaHUD");
            Transform parent = safeArea != null ? safeArea : canvas.transform;

            Transform comboTf = parent.Find("ComboHud");
            GameObject comboGo = comboTf != null
                ? comboTf.gameObject
                : new GameObject("ComboHud", typeof(RectTransform), typeof(ComboHudUI));
            if (comboTf == null)
            {
                comboGo.transform.SetParent(parent, false);
            }

            comboGo.SetActive(true);
            var rt = (RectTransform)comboGo.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -334f);
            rt.sizeDelta = new Vector2(240f, 88f);

            Transform contentTf = comboGo.transform.Find("Content");
            GameObject contentGo = contentTf != null
                ? contentTf.gameObject
                : new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(Outline));
            if (contentTf == null)
            {
                contentGo.transform.SetParent(comboGo.transform, false);
            }

            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var bgImg = EnsureComponent<Image>(contentGo);
            bgImg.color = new Color(0.1f, 0.12f, 0.18f, 0.88f);
            bgImg.raycastTarget = false;

            var outline = EnsureComponent<Outline>(contentGo);
            outline.effectColor = new Color(1f, 0.78f, 0.22f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI comboTmp = EnsureTmpChild(
                contentGo.transform,
                "ComboText",
                new Vector2(0f, 0.42f),
                new Vector2(1f, 0.96f),
                new Vector2(10f, 0f),
                new Vector2(-10f, -2f),
                "x15",
                36f,
                Color.white,
                TextAlignmentOptions.Center,
                defaultFont);

            TextMeshProUGUI bonusTmp = EnsureTmpChild(
                contentGo.transform,
                "BonusText",
                new Vector2(0f, 0.16f),
                new Vector2(1f, 0.44f),
                new Vector2(8f, 0f),
                new Vector2(-8f, 0f),
                "+10% XP & COINS",
                17f,
                new Color(1f, 0.86f, 0.28f, 1f),
                TextAlignmentOptions.Center,
                defaultFont);

            Transform timerBgTf = contentGo.transform.Find("TimerBarBg");
            GameObject timerBgGo = timerBgTf != null
                ? timerBgTf.gameObject
                : new GameObject("TimerBarBg", typeof(RectTransform), typeof(Image));
            if (timerBgTf == null) timerBgGo.transform.SetParent(contentGo.transform, false);

            var timerBgRt = (RectTransform)timerBgGo.transform;
            timerBgRt.anchorMin = new Vector2(0.06f, 0.04f);
            timerBgRt.anchorMax = new Vector2(0.94f, 0.14f);
            timerBgRt.offsetMin = Vector2.zero;
            timerBgRt.offsetMax = Vector2.zero;
            var timerBgImg = EnsureComponent<Image>(timerBgGo);
            timerBgImg.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            timerBgImg.raycastTarget = false;

            Transform fillTf = timerBgGo.transform.Find("TimerFill");
            GameObject fillGo = fillTf != null
                ? fillTf.gameObject
                : new GameObject("TimerFill", typeof(RectTransform), typeof(Image));
            if (fillTf == null) fillGo.transform.SetParent(timerBgGo.transform, false);

            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = EnsureComponent<Image>(fillGo);
            fillImg.color = new Color(1f, 0.82f, 0.24f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;

            contentGo.SetActive(false);

            var comboHud = EnsureComponent<ComboHudUI>(comboGo);
            var so = new SerializedObject(comboHud);
            so.FindProperty("rootContainer").objectReferenceValue = contentGo;
            so.FindProperty("comboText").objectReferenceValue = comboTmp;
            so.FindProperty("bonusText").objectReferenceValue = bonusTmp;
            so.FindProperty("timerFillImage").objectReferenceValue = fillImg;
            so.FindProperty("backgroundImage").objectReferenceValue = bgImg;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comboHud);
        }

        private static void SetupGameOverBreakdownLayout(Canvas canvas, TMP_FontAsset defaultFont, SynergyCatalog synergyCatalog)
        {
            Transform gameOverTf = canvas.transform.Find("GameOverPanel");
            if (gameOverTf == null)
            {
                Debug.LogWarning("[Sprint8SetupTool] GameOverPanel not found under Canvas.");
                return;
            }

            SetAnchoredRect(gameOverTf.Find("Title"), new Vector2(0f, 740f), new Vector2(650f, 88f));
            SetAnchoredRect(gameOverTf.Find("WaveText"), new Vector2(0f, 650f), new Vector2(840f, 50f));
            SetAnchoredRect(gameOverTf.Find("CoinsText"), new Vector2(0f, 598f), new Vector2(840f, 46f));
            SetAnchoredRect(gameOverTf.Find("StatsText"), new Vector2(0f, 548f), new Vector2(880f, 46f));

            // Create / configure DamageBreakdownSection
            Transform breakdownTf = gameOverTf.Find("DamageBreakdownSection");
            GameObject breakdownGo = breakdownTf != null
                ? breakdownTf.gameObject
                : new GameObject("DamageBreakdownSection", typeof(RectTransform), typeof(Image), typeof(Outline));
            if (breakdownTf == null)
            {
                breakdownGo.transform.SetParent(gameOverTf, false);
            }

            var brRt = (RectTransform)breakdownGo.transform;
            brRt.anchorMin = new Vector2(0.5f, 0.5f);
            brRt.anchorMax = new Vector2(0.5f, 0.5f);
            brRt.pivot = new Vector2(0.5f, 0.5f);
            brRt.anchoredPosition = new Vector2(0f, 290f);
            brRt.sizeDelta = new Vector2(880f, 410f);

            var brBg = EnsureComponent<Image>(breakdownGo);
            brBg.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            brBg.raycastTarget = false;

            var brOutline = EnsureComponent<Outline>(breakdownGo);
            brOutline.effectColor = new Color(0.32f, 0.42f, 0.62f, 0.75f);
            brOutline.effectDistance = new Vector2(2f, -2f);

            EnsureTmpChild(
                breakdownGo.transform,
                "Header",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(16f, -44f),
                new Vector2(-16f, -6f),
                "DAMAGE BREAKDOWN",
                24f,
                new Color(1f, 0.85f, 0.35f, 1f),
                TextAlignmentOptions.Center,
                defaultFont);

            // ScrollView -> Viewport (RectMask2D) -> Container: rows never spill over the sections below.
            Transform scrollTf = breakdownGo.transform.Find("ScrollView");
            GameObject scrollGo = scrollTf != null
                ? scrollTf.gameObject
                : new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            if (scrollTf == null)
            {
                scrollGo.transform.SetParent(breakdownGo.transform, false);
            }

            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(14f, 12f);
            scrollRt.offsetMax = new Vector2(-14f, -48f);

            Transform viewportTf = scrollGo.transform.Find("Viewport");
            GameObject viewportGo = viewportTf != null
                ? viewportTf.gameObject
                : new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            if (viewportTf == null)
            {
                viewportGo.transform.SetParent(scrollGo.transform, false);
            }

            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImg = EnsureComponent<Image>(viewportGo);
            viewportImg.color = new Color(0f, 0f, 0f, 0f); // transparent, but receives drag raycasts
            viewportImg.raycastTarget = true;
            EnsureComponent<RectMask2D>(viewportGo);

            // Migrate a legacy Container that sat directly under the section.
            Transform containerTf = viewportGo.transform.Find("Container");
            if (containerTf == null)
            {
                containerTf = breakdownGo.transform.Find("Container");
            }
            GameObject containerGo = containerTf != null
                ? containerTf.gameObject
                : new GameObject("Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
            containerGo.transform.SetParent(viewportGo.transform, false);

            var contRt = (RectTransform)containerGo.transform;
            contRt.anchorMin = new Vector2(0f, 1f);
            contRt.anchorMax = new Vector2(1f, 1f);
            contRt.pivot = new Vector2(0.5f, 1f);
            contRt.anchoredPosition = Vector2.zero;
            contRt.sizeDelta = Vector2.zero;

            var vlg = EnsureComponent<VerticalLayoutGroup>(containerGo);
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true; // respect LayoutElement.preferredHeight of each row
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = EnsureComponent<ContentSizeFitter>(containerGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = EnsureComponent<ScrollRect>(scrollGo);
            scrollRect.viewport = viewportRt;
            scrollRect.content = contRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 20f;

            // Compact RunAchievementsSection and stack action buttons cleanly
            SetAnchoredRect(gameOverTf.Find("RunAchievementsSection"), new Vector2(0f, -25f), new Vector2(880f, 180f));
            SetAnchoredRect(gameOverTf.Find("ReviveButton"), new Vector2(0f, -190f), new Vector2(620f, 96f));
            SetAnchoredRect(gameOverTf.Find("DoubleCoinsButton"), new Vector2(0f, -304f), new Vector2(620f, 96f));
            SetAnchoredRect(gameOverTf.Find("RestartButton"), new Vector2(0f, -418f), new Vector2(620f, 96f));
            SetAnchoredRect(gameOverTf.Find("MenuButton"), new Vector2(0f, -532f), new Vector2(620f, 96f));

            var gameOverUi = canvas.GetComponentInChildren<GameOverUI>(true);
            if (gameOverUi != null)
            {
                var abCatalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(ABILITY_CATALOG_PATH);
                var heroCat = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HERO_CATALOG_PATH);

                var so = new SerializedObject(gameOverUi);
                so.FindProperty("damageBreakdownSection").objectReferenceValue = breakdownGo;
                so.FindProperty("damageBreakdownContainer").objectReferenceValue = containerGo.transform;
                so.FindProperty("abilityCatalog").objectReferenceValue = abCatalog;
                so.FindProperty("synergyCatalog").objectReferenceValue = synergyCatalog;
                so.FindProperty("heroCatalog").objectReferenceValue = heroCat;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gameOverUi);
            }
        }

        private static void WriteSynergyListProperty(SerializedProperty listProp, List<SynergyData> synergies)
        {
            if (listProp == null) return;
            listProp.arraySize = synergies.Count;
            for (int i = 0; i < synergies.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = synergies[i];
            }
        }

        private static void SetAnchoredRect(Transform tf, Vector2 pos, Vector2 size)
        {
            if (tf is RectTransform rt)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
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

        private static TextMeshProUGUI EnsureTmpChild(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            string defaultText,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            Transform tf = parent.Find(name);
            GameObject go = tf != null ? tf.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (tf == null) go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(12f, fontSize * 0.55f);
            tmp.fontSizeMax = fontSize;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp != null ? comp : go.AddComponent<T>();
        }
    }
}
