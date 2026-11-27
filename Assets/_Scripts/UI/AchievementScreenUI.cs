using System;
using System.Collections;
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
        private const float CATEGORY_BAR_HEIGHT = 92f;
        private const float CATEGORY_CHIP_HEIGHT = 72f;
        private const int INITIAL_BATCH_SIZE = 10;
        private const int BATCH_PER_FRAME = 8;

        [SerializeField] private Transform contentContainer;
        [SerializeField] private AchievementItemUI itemPrefab;
        [SerializeField] private AchievementRewardPopupUI rewardPopup;

        [Header("Sprint 13")]
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private TMPro.TMP_Text summaryText;

        private AchievementManager achievementManager;
        private SaveManager saveManager;

        private readonly PanelCanvasVisibility panelVisibility = new PanelCanvasVisibility();
        private readonly List<AchievementItemUI> itemPool = new List<AchievementItemUI>(64);
        private readonly List<AchievementData> sortBuffer = new List<AchievementData>(64);
        private readonly List<CategoryChip> categoryChips = new List<CategoryChip>(9);
        private int[] categoryTotals;
        private int[] categoryCompleted;

        private static readonly HashSet<string> sortCompletedScratch = new HashSet<string>(StringComparer.Ordinal);

        private ScrollRect cachedScrollRect;
        private RectTransform categoryBar;
        private int selectedCategory = -1; // -1 = all
        private int activeItemCount;
        private int populatedUpTo;
        private bool dataDirty = true;
        private bool eventsSubscribed;
        private Coroutine populateCoroutine;

        private SaveManager subscribedSaveManager;
        private AchievementManager subscribedAchievementManager;
        private LocalizationService subscribedLocalization;
        private Action cachedDataInvalidated;

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
        public bool IsVisible => panelVisibility.IsVisible;
        public void HideRewardPopup() => rewardPopup?.Hide();

        public UnityEngine.UI.Selectable GetFirstSelectable()
        {
            int count = Mathf.Min(activeItemCount, itemPool.Count);
            for (int i = 0; i < count; i++)
            {
                if (itemPool[i] != null && itemPool[i].ItemButton != null &&
                    itemPool[i].ItemButton.gameObject.activeInHierarchy && itemPool[i].ItemButton.interactable)
                {
                    return itemPool[i].ItemButton;
                }
            }
            return UiFocusHelper.FindFirstInteractable(transform);
        }

        [Inject]
        public void Construct(AchievementManager achievementManager, SaveManager saveManager)
        {
            this.achievementManager = achievementManager;
            this.saveManager = saveManager;
            dataDirty = true;

            if (isActiveAndEnabled && panelVisibility.IsVisible)
            {
                EnsureEventSubscriptions();
                RefreshUI(immediate: !Application.isPlaying);
            }
        }

        private void Awake()
        {
            if (contentContainer != null)
            {
                cachedScrollRect = contentContainer.GetComponentInParent<ScrollRect>(true);
            }
        }

        private void OnEnable()
        {
            panelVisibility.SyncOnEnable(gameObject);
            EnsureEventSubscriptions();
            RefreshIfNeeded();
        }

        private void OnDisable()
        {
            StopPopulateCoroutine();
            panelVisibility.SyncOnDisable();
        }

        private void OnDestroy()
        {
            StopPopulateCoroutine();
            UnsubscribeEventSubscriptions();
            for (int i = 0; i < itemPool.Count; i++)
            {
                if (itemPool[i] != null)
                {
                    itemPool[i].OnItemClicked -= HandleItemClicked;
                }
            }
        }

        public void Show()
        {
            bool wasAlreadyActive = panelVisibility.BeginShow(gameObject);
            if (wasAlreadyActive)
            {
                RefreshIfNeeded();
                panelVisibility.FinishShow(true);
            }
            else if (!Application.isPlaying)
            {
                EnsureEventSubscriptions();
                RefreshIfNeeded();
            }
        }

        public void Hide()
        {
            StopPopulateCoroutine();
            if (cachedScrollRect != null) cachedScrollRect.StopMovement();
            if (IsRewardPopupOpen) HideRewardPopup();
            panelVisibility.Hide(gameObject);
        }

        private void EnsureEventSubscriptions()
        {
            cachedDataInvalidated ??= HandleDataInvalidated;
            if (eventsSubscribed
                && ReferenceEquals(subscribedSaveManager, saveManager)
                && ReferenceEquals(subscribedAchievementManager, achievementManager)
                && ReferenceEquals(subscribedLocalization, LocalizationService.Current))
            {
                return;
            }

            UnsubscribeEventSubscriptions();

            subscribedSaveManager = saveManager;
            if (subscribedSaveManager != null) subscribedSaveManager.OnDataChanged += cachedDataInvalidated;

            subscribedAchievementManager = achievementManager;
            if (subscribedAchievementManager != null) subscribedAchievementManager.OnAchievementUnlocked += HandleAchievementUnlocked;

            subscribedLocalization = LocalizationService.Current;
            if (subscribedLocalization != null) subscribedLocalization.LanguageChanged += cachedDataInvalidated;

            eventsSubscribed = true;
        }

        private void UnsubscribeEventSubscriptions()
        {
            if (!eventsSubscribed) return;

            if (subscribedSaveManager != null) subscribedSaveManager.OnDataChanged -= cachedDataInvalidated;
            if (subscribedAchievementManager != null) subscribedAchievementManager.OnAchievementUnlocked -= HandleAchievementUnlocked;
            if (subscribedLocalization != null) subscribedLocalization.LanguageChanged -= cachedDataInvalidated;

            subscribedSaveManager = null;
            subscribedAchievementManager = null;
            subscribedLocalization = null;
            eventsSubscribed = false;
        }

        private void HandleAchievementUnlocked(AchievementData _)
        {
            HandleDataInvalidated();
        }

        private void HandleDataInvalidated()
        {
            dataDirty = true;
            if (panelVisibility.IsVisible && isActiveAndEnabled)
            {
                RefreshUI(immediate: !Application.isPlaying);
            }
        }

        private void RefreshIfNeeded()
        {
            if (achievementManager == null || saveManager == null || itemPrefab == null || contentContainer == null) return;

            if (dataDirty)
            {
                RefreshUI(immediate: !Application.isPlaying);
                return;
            }

            if (populatedUpTo < sortBuffer.Count && Application.isPlaying && isActiveAndEnabled && populateCoroutine == null)
            {
                populateCoroutine = StartCoroutine(PopulateRemainingCoroutine());
            }
        }

        public void RefreshUI()
        {
            RefreshUI(immediate: !Application.isPlaying);
        }

        private void RefreshUI(bool immediate)
        {
            if (achievementManager == null || saveManager == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogWarning($"[AchievementScreenUI] Cannot refresh. achievementManager: {achievementManager != null}, saveManager: {saveManager != null}, itemPrefab: {itemPrefab != null}, contentContainer: {contentContainer != null}");
                return;
            }

            StopPopulateCoroutine();
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
            SortByCompletion(sortBuffer, saveManager.Data, achievementManager);

            int completed = 0;
            for (int i = 0; i < sortBuffer.Count; i++)
            {
                if (achievementManager.IsAchievementCompleted(sortBuffer[i].id)) completed++;
            }

            if (summaryText != null)
            {
                string summary = Loc.Format("ui.achievements.summary", completed, sortBuffer.Count);
                if (!string.Equals(summaryText.text, summary, StringComparison.Ordinal))
                {
                    summaryText.text = summary;
                }
            }

            activeItemCount = sortBuffer.Count;
            for (int i = activeItemCount; i < itemPool.Count; i++)
            {
                if (itemPool[i] != null && itemPool[i].gameObject.activeSelf)
                {
                    itemPool[i].gameObject.SetActive(false);
                }
            }

            populatedUpTo = 0;
            int initialCount = immediate ? activeItemCount : Mathf.Min(activeItemCount, INITIAL_BATCH_SIZE);
            PopulateRange(0, initialCount);

            if (populatedUpTo < activeItemCount && Application.isPlaying && isActiveAndEnabled)
            {
                populateCoroutine = StartCoroutine(PopulateRemainingCoroutine());
            }

            if (contentContainer != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(contentContainer as RectTransform);
            }

            dataDirty = false;
        }

        private void PopulateRange(int startIndex, int endExclusive)
        {
            int limit = Mathf.Min(endExclusive, sortBuffer.Count);
            SaveData saveData = saveManager.Data;
            for (int i = startIndex; i < limit; i++)
            {
                AchievementData data = sortBuffer[i];
                AchievementItemUI item;
                if (i < itemPool.Count)
                {
                    item = itemPool[i];
                    if (item == null)
                    {
                        item = CreatePooledItem();
                        itemPool[i] = item;
                    }
                    else if (!item.gameObject.activeSelf)
                    {
                        item.gameObject.SetActive(true);
                    }
                }
                else
                {
                    item = CreatePooledItem();
                    itemPool.Add(item);
                }

                bool isCompleted = achievementManager.IsAchievementCompleted(data.id);
                saveData.achievementProgress.TryGetValue(data.id, out int progress);
                item.Setup(data, progress, isCompleted, abilityCatalog);
            }
            populatedUpTo = Mathf.Max(populatedUpTo, limit);
        }

        private AchievementItemUI CreatePooledItem()
        {
            var item = Instantiate(itemPrefab, contentContainer);
            item.OnItemClicked += HandleItemClicked;
            return item;
        }

        private IEnumerator PopulateRemainingCoroutine()
        {
            while (populatedUpTo < activeItemCount && populatedUpTo < sortBuffer.Count)
            {
                yield return null;
                if (!panelVisibility.IsVisible || !isActiveAndEnabled)
                {
                    populateCoroutine = null;
                    yield break;
                }

                int nextEnd = Mathf.Min(activeItemCount, populatedUpTo + BATCH_PER_FRAME);
                PopulateRange(populatedUpTo, nextEnd);

                if (contentContainer != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(contentContainer as RectTransform);
                }
            }

            populateCoroutine = null;
        }

        private void StopPopulateCoroutine()
        {
            if (populateCoroutine != null)
            {
                StopCoroutine(populateCoroutine);
                populateCoroutine = null;
            }
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
            RefreshUI(immediate: true);
            AchievementItemUI item = FindItem(achievementId);
            if (item == null) return false;
            if (isActiveAndEnabled && Application.isPlaying) StartCoroutine(FocusAfterLayout(item));
            return true;
        }

        public AchievementItemUI FindItem(string achievementId)
        {
            int count = Mathf.Min(activeItemCount, itemPool.Count);
            for (int i = 0; i < count; i++)
            {
                if (itemPool[i] != null && itemPool[i].Data != null && itemPool[i].Data.id == achievementId) return itemPool[i];
            }
            return null;
        }

        private IEnumerator FocusAfterLayout(AchievementItemUI item)
        {
            // Wait one frame for layout pass before scrolling to target item.
            yield return null;
            if (!panelVisibility.IsVisible || item == null || contentContainer == null) yield break;
            var content = (RectTransform)contentContainer;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            ScrollRect scroll = cachedScrollRect != null ? cachedScrollRect : contentContainer.GetComponentInParent<ScrollRect>(true);
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
            RefreshUI(immediate: !Application.isPlaying);
            if (cachedScrollRect != null) cachedScrollRect.verticalNormalizedPosition = 1f;
        }

        private void EnsureCategoryBar()
        {
            if (categoryBar != null || contentContainer == null) return;

            ScrollRect scroll = cachedScrollRect != null ? cachedScrollRect : contentContainer.GetComponentInParent<ScrollRect>(true);
            if (scroll == null) return;
            cachedScrollRect = scroll;
            UiTheme theme = UiTheme.Current;

            var barGo = new GameObject("CategoryBar", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            categoryBar = (RectTransform)barGo.transform;
            categoryBar.SetParent(scroll.transform, false);
            categoryBar.anchorMin = new Vector2(0f, 1f);
            categoryBar.anchorMax = new Vector2(1f, 1f);
            categoryBar.pivot = new Vector2(0.5f, 1f);
            categoryBar.anchoredPosition = Vector2.zero;
            categoryBar.sizeDelta = new Vector2(0f, CATEGORY_BAR_HEIGHT);

            // Opaque untextured quad avoids SpriteAtlas bilinear alpha bleed on top/bottom edges.
            var barImage = barGo.GetComponent<Image>();
            barImage.sprite = null;
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
            int maxCat = 0;
            foreach (AchievementCategory category in Enum.GetValues(typeof(AchievementCategory)))
            {
                if ((int)category > maxCat) maxCat = (int)category;
                if (HasAnyInCategory(category)) CreateChip(row, (int)category, theme);
            }

            categoryTotals = new int[maxCat + 1];
            categoryCompleted = new int[maxCat + 1];

            var grid = contentContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.padding.top += Mathf.RoundToInt(CATEGORY_BAR_HEIGHT + 8f);
            }

            // Clip scrolling cards at the bottom edge of CategoryBar without shrinking Viewport.rect.
            if (scroll.viewport != null && scroll.viewport.TryGetComponent(out RectMask2D viewportMask))
            {
                Vector4 pad = viewportMask.padding;
                pad.w = Mathf.Max(pad.w, CATEGORY_BAR_HEIGHT);
                viewportMask.padding = pad;
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
                RefreshUI(immediate: !Application.isPlaying);
                if (cachedScrollRect != null) cachedScrollRect.verticalNormalizedPosition = 1f;
            });

            categoryChips.Add(new CategoryChip { Category = category, Button = button, Background = bg, Label = label });
        }

        private void UpdateCategoryChips()
        {
            if (categoryChips.Count == 0 || saveManager?.Data == null) return;
            UiTheme theme = UiTheme.Current;
            var all = achievementManager.GetAllAchievements();

            if (categoryTotals == null)
            {
                categoryTotals = new int[16];
                categoryCompleted = new int[16];
            }
            Array.Clear(categoryTotals, 0, categoryTotals.Length);
            Array.Clear(categoryCompleted, 0, categoryCompleted.Length);

            int totalAll = 0;
            int doneAll = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var ach = all[i];
                if (ach == null) continue;
                int cat = (int)ach.category;
                totalAll++;
                bool done = achievementManager.IsAchievementCompleted(ach.id);
                if (done) doneAll++;

                if (cat >= 0 && cat < categoryTotals.Length)
                {
                    categoryTotals[cat]++;
                    if (done) categoryCompleted[cat]++;
                }
            }

            for (int c = 0; c < categoryChips.Count; c++)
            {
                CategoryChip chip = categoryChips[c];
                int total = chip.Category < 0
                    ? totalAll
                    : (chip.Category < categoryTotals.Length ? categoryTotals[chip.Category] : 0);
                int done = chip.Category < 0
                    ? doneAll
                    : (chip.Category < categoryCompleted.Length ? categoryCompleted[chip.Category] : 0);

                string name = chip.Category >= 0
                    ? Loc.Get(CategoryKey((AchievementCategory)chip.Category), ((AchievementCategory)chip.Category).ToString())
                    : Loc.Get("ui.achievements.cat.all", "All");
                string labelText = $"{name}  {done}/{total}";
                if (!string.Equals(chip.Label.text, labelText, StringComparison.Ordinal))
                {
                    chip.Label.text = labelText;
                }

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
            SortByCompletion(list, data, null);
        }

        private static void SortByCompletion(List<AchievementData> list, SaveData data, AchievementManager manager)
        {
            if (list == null || data == null) return;

            if (manager != null)
            {
                list.Sort((a, b) =>
                {
                    float ka = SortKey(a, data, manager.IsAchievementCompleted(a.id));
                    float kb = SortKey(b, data, manager.IsAchievementCompleted(b.id));
                    int cmp = kb.CompareTo(ka);
                    return cmp != 0 ? cmp : AchievementManager.CompareForDisplay(a, b);
                });
                return;
            }

            sortCompletedScratch.Clear();
            if (data.completedAchievements != null)
            {
                for (int i = 0; i < data.completedAchievements.Count; i++)
                {
                    string id = data.completedAchievements[i];
                    if (!string.IsNullOrEmpty(id)) sortCompletedScratch.Add(id);
                }
            }
            try
            {
                list.Sort((a, b) =>
                {
                    float ka = SortKey(a, data, sortCompletedScratch.Contains(a.id));
                    float kb = SortKey(b, data, sortCompletedScratch.Contains(b.id));
                    int cmp = kb.CompareTo(ka);
                    return cmp != 0 ? cmp : AchievementManager.CompareForDisplay(a, b);
                });
            }
            finally
            {
                sortCompletedScratch.Clear();
            }
        }

        private static float SortKey(AchievementData ach, SaveData data, bool isCompleted)
        {
            if (isCompleted) return -1f;
            data.achievementProgress.TryGetValue(ach.id, out int progress);
            return ach.targetValue > 0 ? Mathf.Clamp01((float)progress / ach.targetValue) : 0f;
        }

        private void HandleItemClicked(AchievementData data)
        {
            if (data == null || rewardPopup == null || saveManager?.Data == null) return;

            bool isCompleted = achievementManager != null
                ? achievementManager.IsAchievementCompleted(data.id)
                : saveManager.Data.completedAchievements.Contains(data.id);
            if (!isCompleted) return;

            rewardPopup.ShowReward(data, abilityCatalog);
        }
    }
}
