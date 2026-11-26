using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;

namespace ProjectB.UI
{
    /// <summary>
    /// Achievements tab. Sprint 13.2: closest-to-completion achievements first, completed ones (checked, dimmed)
    /// at the end; a summary line shows "12 / 27 completed".
    /// A sticky row of category chips (All / Progress / Bosses / ...) is built at runtime above the grid and filters it.
    /// </summary>
    public class AchievementScreenUI : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AchievementItemUI itemPrefab;
        [SerializeField] private AchievementRewardPopupUI rewardPopup;

        [Header("Sprint 13")]
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private TMPro.TMP_Text summaryText;

        private AchievementManager achievementManager;
        private SaveManager saveManager;

        private const float CATEGORY_BAR_HEIGHT = 92f;
        private const float CATEGORY_CHIP_HEIGHT = 72f;

        private readonly List<AchievementItemUI> spawnedItems = new List<AchievementItemUI>();
        private readonly List<AchievementData> sortBuffer = new List<AchievementData>(64);
        private readonly List<CategoryChip> categoryChips = new List<CategoryChip>(9);
        private Action cachedLanguageChanged;
        private RectTransform categoryBar;
        private int selectedCategory = -1; // -1 = all

        private sealed class CategoryChip
        {
            public int Category; // -1 = all
            public Button Button;
            public Image Background;
            public TMP_Text Label;
        }

        /// <summary>Current category filter; null shows everything.</summary>
        public AchievementCategory? SelectedCategory => selectedCategory >= 0 ? (AchievementCategory?)selectedCategory : null;

        public bool IsRewardPopupOpen => rewardPopup != null && rewardPopup.IsOpen;
        public void HideRewardPopup() => rewardPopup?.Hide();

        public UnityEngine.UI.Selectable GetFirstSelectable()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null && spawnedItems[i].ItemButton != null &&
                    spawnedItems[i].ItemButton.gameObject.activeInHierarchy && spawnedItems[i].ItemButton.interactable)
                {
                    return spawnedItems[i].ItemButton;
                }
            }
            return UiFocusHelper.FindFirstInteractable(transform);
        }

        [Inject]
        public void Construct(AchievementManager achievementManager, SaveManager saveManager)
        {
            this.achievementManager = achievementManager;
            this.saveManager = saveManager;
            if (isActiveAndEnabled)
            {
                RefreshUI();
            }
        }

        private void OnEnable()
        {
            cachedLanguageChanged ??= RefreshUI;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
            if (achievementManager != null && saveManager != null)
            {
                RefreshUI();
            }
            if (contentContainer != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            }
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        public void RefreshUI()
        {
            if (achievementManager == null || saveManager == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogWarning($"[AchievementScreenUI] Cannot refresh. achievementManager: {achievementManager != null}, saveManager: {saveManager != null}, itemPrefab: {itemPrefab != null}, contentContainer: {contentContainer != null}");
                return;
            }

            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.OnItemClicked -= HandleItemClicked;
                    Destroy(item.gameObject);
                }
            }
            spawnedItems.Clear();

            EnsureCategoryBar();
            UpdateCategoryChips();

            sortBuffer.Clear();
            var allAchievements = achievementManager.GetAllAchievements();
            for (int i = 0; i < allAchievements.Count; i++)
            {
                var ach = allAchievements[i];
                if (ach == null) continue;
                if (selectedCategory >= 0 && (int)ach.category != selectedCategory) continue;
                sortBuffer.Add(ach);
            }
            SortByCompletion(sortBuffer, saveManager.Data);

            int completed = 0;
            foreach (var data in sortBuffer)
            {
                var item = Instantiate(itemPrefab, contentContainer);

                bool isCompleted = saveManager.Data.completedAchievements.Contains(data.id);
                if (isCompleted) completed++;
                saveManager.Data.achievementProgress.TryGetValue(data.id, out int progress);

                item.Setup(data, progress, isCompleted, abilityCatalog);
                item.OnItemClicked += HandleItemClicked;
                spawnedItems.Add(item);
            }

            if (summaryText != null) summaryText.text = Loc.Format("ui.achievements.summary", completed, sortBuffer.Count);
        }

        /// <summary>
        /// Deep link (Play tab goal card → achievement): switches to the achievement's category, scrolls it into view
        /// and flashes a gold ring around it. Returns false when the id is unknown.
        /// </summary>
        public bool FocusAchievement(string achievementId)
        {
            if (achievementManager == null || string.IsNullOrEmpty(achievementId)) return false;
            AchievementData target = null;
            var all = achievementManager.GetAllAchievements();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == achievementId)
                {
                    target = all[i];
                    break;
                }
            }
            if (target == null) return false;

            selectedCategory = (int)target.category;
            RefreshUI();
            AchievementItemUI item = FindItem(achievementId);
            if (item == null) return false;
            if (isActiveAndEnabled && Application.isPlaying) StartCoroutine(FocusAfterLayout(item));
            return true;
        }

        public AchievementItemUI FindItem(string achievementId)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null && spawnedItems[i].Data != null && spawnedItems[i].Data.id == achievementId) return spawnedItems[i];
            }
            return null;
        }

        private System.Collections.IEnumerator FocusAfterLayout(AchievementItemUI item)
        {
            // Items from the previous list are destroyed at the end of this frame: lay out once they are gone.
            yield return null;
            if (item == null || contentContainer == null) yield break;
            var content = (RectTransform)contentContainer;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            ScrollRect scroll = contentContainer.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
            {
                RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x,
                    CalculateScrollToCenter(content, (RectTransform)item.transform, viewport.rect.height, CATEGORY_BAR_HEIGHT));
                scroll.StopMovement();
            }
            HighlightItem(item);
        }

        /// <summary>Content offset (top-anchored content) that centres <paramref name="item"/> below the sticky bar.</summary>
        public static float CalculateScrollToCenter(RectTransform content, RectTransform item, float viewportHeight, float stickyTop)
        {
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(content, item);
            float fromTop = content.rect.yMax - b.center.y;
            float target = fromTop - (viewportHeight + stickyTop) * 0.5f;
            float max = Mathf.Max(0f, content.rect.height - viewportHeight);
            return Mathf.Clamp(target, 0f, max);
        }

        private static void HighlightItem(AchievementItemUI item)
        {
            UiTheme theme = UiTheme.Current;
            var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = (RectTransform)ring.transform;
            rt.SetParent(item.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-6f, -6f);
            rt.offsetMax = new Vector2(6f, 6f);
            ring.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = ring.GetComponent<Image>();
            image.sprite = theme.outlineSprite;
            image.type = Image.Type.Sliced;
            image.color = theme.gold;
            image.raycastTarget = false;
            UiTween.Punch(item.transform, 0.08f, 0.35f);
            Destroy(ring, 1.8f);
        }

        /// <summary>Shows only <paramref name="category"/> (null = all) and rebuilds the list.</summary>
        public void SetCategoryFilter(AchievementCategory? category)
        {
            selectedCategory = category.HasValue ? (int)category.Value : -1;
            RefreshUI();
        }

        private void EnsureCategoryBar()
        {
            if (categoryBar != null || contentContainer == null) return;

            ScrollRect scroll = contentContainer.GetComponentInParent<ScrollRect>(true);
            if (scroll == null) return;
            UiTheme theme = UiTheme.Current;

            var barGo = new GameObject("CategoryBar", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            categoryBar = (RectTransform)barGo.transform;
            categoryBar.SetParent(scroll.transform, false);
            categoryBar.anchorMin = new Vector2(0f, 1f);
            categoryBar.anchorMax = new Vector2(1f, 1f);
            categoryBar.pivot = new Vector2(0.5f, 1f);
            categoryBar.anchoredPosition = Vector2.zero;
            categoryBar.sizeDelta = new Vector2(0f, CATEGORY_BAR_HEIGHT);

            // Opaque backdrop: the achievement grid scrolls underneath the sticky chip row.
            var barImage = barGo.GetComponent<Image>();
            barImage.sprite = theme.solidSprite;
            barImage.color = theme.background;

            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(categoryBar, false);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = Vector2.zero;
            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.padding = new RectOffset(8, 8, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowGo.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var barScroll = barGo.GetComponent<ScrollRect>();
            barScroll.content = row;
            barScroll.viewport = categoryBar;
            barScroll.horizontal = true;
            barScroll.vertical = false;
            barScroll.movementType = ScrollRect.MovementType.Clamped;
            barScroll.scrollSensitivity = 30f;

            categoryChips.Clear();
            CreateChip(row, -1, theme);
            foreach (AchievementCategory category in Enum.GetValues(typeof(AchievementCategory)))
            {
                if (HasAnyInCategory(category)) CreateChip(row, (int)category, theme);
            }

            var grid = contentContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.padding.top += Mathf.RoundToInt(CATEGORY_BAR_HEIGHT + 8f);
            }
        }

        private bool HasAnyInCategory(AchievementCategory category)
        {
            var all = achievementManager.GetAllAchievements();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].category == category) return true;
            }
            return false;
        }

        private void CreateChip(RectTransform parent, int category, UiTheme theme)
        {
            var chipGo = new GameObject(category >= 0 ? $"Chip_{(AchievementCategory)category}" : "Chip_All",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            chipGo.transform.SetParent(parent, false);

            var bg = chipGo.GetComponent<Image>();
            bg.sprite = theme.pillSprite != null ? theme.pillSprite : theme.buttonSprite;
            bg.type = Image.Type.Sliced;

            var layout = chipGo.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var element = chipGo.GetComponent<LayoutElement>();
            element.minHeight = CATEGORY_CHIP_HEIGHT;
            element.preferredHeight = CATEGORY_CHIP_HEIGHT;
            element.minWidth = 120f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(chipGo.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.font = theme.bodyBoldFont != null ? theme.bodyBoldFont : theme.bodyFont;
            label.fontSize = theme.bodySize - 2f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            var button = chipGo.GetComponent<Button>();
            button.targetGraphic = bg;
            int captured = category;
            button.onClick.AddListener(() =>
            {
                selectedCategory = captured;
                RefreshUI();
            });

            categoryChips.Add(new CategoryChip { Category = category, Button = button, Background = bg, Label = label });
        }

        private void UpdateCategoryChips()
        {
            if (categoryChips.Count == 0 || saveManager?.Data == null) return;
            UiTheme theme = UiTheme.Current;
            var all = achievementManager.GetAllAchievements();

            for (int c = 0; c < categoryChips.Count; c++)
            {
                CategoryChip chip = categoryChips[c];
                int total = 0, done = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    var ach = all[i];
                    if (ach == null) continue;
                    if (chip.Category >= 0 && (int)ach.category != chip.Category) continue;
                    total++;
                    if (saveManager.Data.completedAchievements.Contains(ach.id)) done++;
                }

                string name = chip.Category >= 0
                    ? Loc.Get(CategoryKey((AchievementCategory)chip.Category), ((AchievementCategory)chip.Category).ToString())
                    : Loc.Get("ui.achievements.cat.all", "All");
                chip.Label.text = $"{name}  {done}/{total}";

                bool selected = chip.Category == selectedCategory;
                chip.Background.color = selected ? theme.accent : theme.surfaceRaised;
                chip.Label.color = selected ? theme.white : theme.textSecondary;
            }
        }

        public static string CategoryKey(AchievementCategory category)
        {
            switch (category)
            {
                case AchievementCategory.Progress: return "ui.achievements.cat.progress";
                case AchievementCategory.Bosses: return "ui.achievements.cat.bosses";
                case AchievementCategory.Combat: return "ui.achievements.cat.combat";
                case AchievementCategory.Mastery: return "ui.achievements.cat.mastery";
                case AchievementCategory.Challenge: return "ui.achievements.cat.challenge";
                case AchievementCategory.Heroes: return "ui.achievements.cat.heroes";
                case AchievementCategory.Meta: return "ui.achievements.cat.meta";
                case AchievementCategory.Secrets: return "ui.achievements.cat.secrets";
                default: return "ui.achievements.cat.all";
            }
        }

        /// <summary>In progress (highest ratio first) → not started → completed.</summary>
        public static void SortByCompletion(List<AchievementData> list, SaveData data)
        {
            if (list == null || data == null) return;
            list.Sort((a, b) =>
            {
                float ka = SortKey(a, data);
                float kb = SortKey(b, data);
                int cmp = kb.CompareTo(ka);
                return cmp != 0 ? cmp : AchievementManager.CompareForDisplay(a, b);
            });
        }

        private static float SortKey(AchievementData ach, SaveData data)
        {
            if (data.completedAchievements.Contains(ach.id)) return -1f;
            data.achievementProgress.TryGetValue(ach.id, out int progress);
            return ach.targetValue > 0 ? Mathf.Clamp01((float)progress / ach.targetValue) : 0f;
        }

        private void HandleItemClicked(AchievementData data)
        {
            if (data == null || rewardPopup == null || saveManager?.Data == null) return;

            bool isCompleted = saveManager.Data.completedAchievements.Contains(data.id);
            if (!isCompleted) return;

            rewardPopup.ShowReward(data, abilityCatalog);
        }
    }
}
