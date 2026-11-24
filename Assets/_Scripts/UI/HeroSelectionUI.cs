using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Play tab (Sprint 13.2): hero name plate + role, chevrons and pagination dots (swipe on the preview works too),
    /// locked heroes as a dark silhouette with a lock and the unlocking achievement, stat chips (HP / Speed),
    /// Passive / Weapon / Ultimate rows that expand on tap, and a pulsing START button.
    /// </summary>
    public class HeroSelectionUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Hero Info UI")]
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private TextMeshProUGUI heroStatusText;
        [SerializeField] private TextMeshProUGUI passiveText;
        [SerializeField] private TextMeshProUGUI ultimateText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Controls")]
        [SerializeField] private Button prevHeroButton;
        [SerializeField] private Button nextHeroButton;
        [SerializeField] private Button startRunButton;
        [SerializeField] private TextMeshProUGUI startButtonText;

        [Header("Sprint 13: redesigned Play tab")]
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private Transform paginationContainer;
        [SerializeField] private Image paginationDotTemplate;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text lockRequirementText;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private TMP_Text speedValueText;
        [SerializeField] private Image passiveIcon;
        [SerializeField] private TMP_Text passiveNameText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text weaponDescriptionText;
        [SerializeField] private Image ultimateIcon;
        [SerializeField] private TMP_Text ultimateNameText;
        [SerializeField] private TMP_Text ultimateDescriptionText;
        [SerializeField] private Button passiveRowButton;
        [SerializeField] private Button weaponRowButton;
        [SerializeField] private Button ultimateRowButton;
        [SerializeField] private PulseUI startPulse;

        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 50f;

        [Header("Optional 3D Preview")]
        [SerializeField] private Transform heroPreviewVisualRoot;

        private SelectedHeroService selectedHeroService;
        private AchievementManager achievementManager;
        private MetaUpgradeManager metaUpgradeManager;

        private int currentIndex;
        private GameObject spawnedPreviewModel;
        private Vector2 pointerDownPosition;
        private bool isTrackingSwipe;
        private Canvas parentCanvas;
        private readonly List<Image> paginationDots = new List<Image>(8);
        private MaterialPropertyBlock silhouetteBlock;

        public int CurrentIndex => currentIndex;

        [Inject]
        public void Construct(
            SelectedHeroService selectedHeroService,
            AchievementManager achievementManager = null,
            MetaUpgradeManager metaUpgradeManager = null)
        {
            this.selectedHeroService = selectedHeroService;
            this.achievementManager = achievementManager;
            this.metaUpgradeManager = metaUpgradeManager;
        }

        private void Awake()
        {
            parentCanvas = GetComponentInParent<Canvas>();
            if (GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
            if (GetComponent<Graphic>() == null)
            {
                gameObject.AddComponent<InvisibleRaycastTarget>();
            }

            DisableRaycastTarget(heroNameText);
            DisableRaycastTarget(heroStatusText);
            DisableRaycastTarget(passiveText);
            DisableRaycastTarget(ultimateText);
            DisableRaycastTarget(statsText);

            if (prevHeroButton != null) prevHeroButton.onClick.AddListener(SelectPrevious);
            if (nextHeroButton != null) nextHeroButton.onClick.AddListener(SelectNext);
            if (passiveRowButton != null) passiveRowButton.onClick.AddListener(TogglePassive);
            if (weaponRowButton != null) weaponRowButton.onClick.AddListener(ToggleWeapon);
            if (ultimateRowButton != null) ultimateRowButton.onClick.AddListener(ToggleUltimate);
            if (paginationDotTemplate != null) paginationDotTemplate.gameObject.SetActive(false);
        }

        private static void DisableRaycastTarget(Graphic graphic)
        {
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            SyncIndexToSelectedHero();
            RefreshUI();
        }

        private void OnDisable()
        {
            isTrackingSwipe = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null) return;
            pointerDownPosition = eventData.position;
            isTrackingSwipe = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Required by Unity EventSystem so pointer press tracking stays active during swipes.
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isTrackingSwipe || eventData == null) return;
            isTrackingSwipe = false;

            Vector2 delta = eventData.position - pointerDownPosition;
            if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
            float scaleFactor = parentCanvas != null && parentCanvas.scaleFactor > 0f ? parentCanvas.scaleFactor : 1f;
            float effectiveThreshold = swipeThreshold * scaleFactor;

            if (Mathf.Abs(delta.x) >= effectiveThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x < 0f)
                {
                    SelectNext();
                }
                else
                {
                    SelectPrevious();
                }
            }
        }

        private void Start()
        {
            SyncIndexToSelectedHero();
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (prevHeroButton != null) prevHeroButton.onClick.RemoveListener(SelectPrevious);
            if (nextHeroButton != null) nextHeroButton.onClick.RemoveListener(SelectNext);
            if (passiveRowButton != null) passiveRowButton.onClick.RemoveListener(TogglePassive);
            if (weaponRowButton != null) weaponRowButton.onClick.RemoveListener(ToggleWeapon);
            if (ultimateRowButton != null) ultimateRowButton.onClick.RemoveListener(ToggleUltimate);
        }

        private void SyncIndexToSelectedHero()
        {
            if (selectedHeroService == null) return;

            var heroes = selectedHeroService.GetAllHeroes();
            var selected = selectedHeroService.GetSelectedHero();
            currentIndex = 0;

            if (selected != null)
            {
                for (int i = 0; i < heroes.Count; i++)
                {
                    if (heroes[i] == selected)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
        }

        public void SelectPrevious()
        {
            if (selectedHeroService == null) return;
            var heroes = selectedHeroService.GetAllHeroes();
            if (heroes.Count <= 1) return;

            currentIndex = (currentIndex - 1 + heroes.Count) % heroes.Count;
            ApplyCurrentHeroIfUnlocked();
            RefreshUI();
            PlaySwitchFeedback();
        }

        public void SelectNext()
        {
            if (selectedHeroService == null) return;
            var heroes = selectedHeroService.GetAllHeroes();
            if (heroes.Count <= 1) return;

            currentIndex = (currentIndex + 1) % heroes.Count;
            ApplyCurrentHeroIfUnlocked();
            RefreshUI();
            PlaySwitchFeedback();
        }

        private void PlaySwitchFeedback()
        {
            if (!Application.isPlaying) return;
            if (heroNameText != null) UiTween.Punch(heroNameText.transform, 0.12f, 0.25f);
        }

        private void ApplyCurrentHeroIfUnlocked()
        {
            var heroes = selectedHeroService.GetAllHeroes();
            if (currentIndex < 0 || currentIndex >= heroes.Count) return;

            var hero = heroes[currentIndex];
            if (selectedHeroService.IsHeroUnlocked(hero))
            {
                selectedHeroService.TrySelectHero(hero.id, saveImmediately: true);
            }
        }

        public void RefreshUI()
        {
            if (selectedHeroService == null) return;

            UiTheme theme = UiTheme.Current;
            IReadOnlyList<HeroData> heroes = selectedHeroService.GetAllHeroes();
            bool hasMultiple = heroes.Count > 1;

            if (prevHeroButton != null) prevHeroButton.gameObject.SetActive(hasMultiple);
            if (nextHeroButton != null) nextHeroButton.gameObject.SetActive(hasMultiple);

            if (heroes.Count == 0) return;

            currentIndex = Mathf.Clamp(currentIndex, 0, heroes.Count - 1);
            HeroData hero = heroes[currentIndex];
            if (hero == null) return;

            bool isUnlocked = selectedHeroService.IsHeroUnlocked(hero);
            string requirement = isUnlocked ? string.Empty : FindHeroUnlockRequirement(hero.id);

            if (heroNameText != null)
            {
                heroNameText.text = hero.heroName.ToUpperInvariant();
            }

            if (roleText != null)
            {
                roleText.text = string.IsNullOrEmpty(hero.role) ? string.Empty : hero.role.ToUpperInvariant();
            }

            if (heroStatusText != null)
            {
                if (isUnlocked)
                {
                    heroStatusText.text = "READY FOR BATTLE";
                    heroStatusText.color = theme.success;
                }
                else
                {
                    heroStatusText.text = string.IsNullOrEmpty(requirement) ? "LOCKED" : $"LOCKED — {requirement}";
                    heroStatusText.color = theme.warning;
                }
            }

            if (lockOverlay != null) lockOverlay.SetActive(!isUnlocked);
            if (lockRequirementText != null)
            {
                lockRequirementText.text = string.IsNullOrEmpty(requirement) ? "Locked" : requirement;
            }

            SplitPassiveDescription(hero.passiveDescription, out string passiveLine, out string weaponLine);

            if (passiveText != null)
            {
                passiveText.text = passiveNameText != null
                    ? passiveLine
                    : $"<b>Passive:</b> {hero.passiveName} — {hero.passiveDescription}";
            }
            if (passiveNameText != null) passiveNameText.text = hero.passiveName;
            if (passiveIcon != null)
            {
                passiveIcon.sprite = hero.icon;
                passiveIcon.enabled = hero.icon != null;
            }

            if (weaponNameText != null || weaponDescriptionText != null)
            {
                SplitWeapon(weaponLine, out string weaponName, out string weaponDetails);
                if (weaponNameText != null) weaponNameText.text = string.IsNullOrEmpty(weaponName) ? "Weapon" : weaponName;
                if (weaponDescriptionText != null) weaponDescriptionText.text = weaponDetails;
            }

            if (ultimateText != null && ultimateNameText == null)
            {
                ultimateText.text = $"<b>Ultimate:</b> {hero.ultimateName} — {hero.ultimateDescription} ({hero.ultimateCooldown:0}s CD)";
            }
            if (ultimateNameText != null) ultimateNameText.text = $"{hero.ultimateName}  <color=#{ColorUtility.ToHtmlStringRGB(theme.textSecondary)}><size=80%>{hero.ultimateCooldown:0}s</size></color>";
            if (ultimateDescriptionText != null) ultimateDescriptionText.text = hero.ultimateDescription;
            if (ultimateIcon != null)
            {
                ultimateIcon.sprite = hero.ultimateIcon;
                ultimateIcon.enabled = hero.ultimateIcon != null;
            }

            int effectiveHp = CalculateEffectiveHp(hero);
            float effectiveSpeed = CalculateEffectiveSpeed(hero);

            if (statsText != null)
            {
                statsText.text = $"HP: {effectiveHp}   •   Speed: {effectiveSpeed:0.0}";
            }
            if (hpValueText != null) hpValueText.text = effectiveHp.ToString();
            if (speedValueText != null) speedValueText.text = effectiveSpeed.ToString("0.0");

            if (startRunButton != null)
            {
                startRunButton.interactable = isUnlocked;
            }

            if (startButtonText != null)
            {
                startButtonText.text = isUnlocked ? "START" : "LOCKED";
            }

            if (startPulse != null) startPulse.enabled = isUnlocked;

            RefreshPagination(heroes, currentIndex);
            UpdatePreviewModel(hero);
            ApplySilhouette(!isUnlocked);
        }

        private int CalculateEffectiveHp(HeroData hero)
        {
            float passiveHpMult = hero.passiveType == HeroPassiveType.MaxHpPercent
                ? 1f + hero.passiveValue / 100f
                : 1f;
            float metaHpMult = metaUpgradeManager != null
                ? 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP) / 100f
                : 1f;
            return Mathf.RoundToInt(hero.maxHp * passiveHpMult * metaHpMult);
        }

        private float CalculateEffectiveSpeed(HeroData hero)
        {
            float passiveSpeedMult = hero.passiveType == HeroPassiveType.MoveSpeedPercent
                ? 1f + hero.passiveValue / 100f
                : 1f;
            float metaSpeedMult = metaUpgradeManager != null
                ? 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroSpeed) / 100f
                : 1f;
            return hero.moveSpeed * passiveSpeedMult * metaSpeedMult;
        }

        /// <summary>Hero passive descriptions embed the weapon as "…\n&lt;b&gt;Weapon:&lt;/b&gt; Name (details)".</summary>
        public static void SplitPassiveDescription(string description, out string passive, out string weapon)
        {
            passive = description ?? string.Empty;
            weapon = string.Empty;
            if (string.IsNullOrEmpty(description)) return;

            const string marker = "<b>Weapon:</b>";
            int idx = description.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            passive = description.Substring(0, idx).Trim();
            weapon = description.Substring(idx + marker.Length).Replace('\n', ' ').Trim();
            while (weapon.Contains("  ")) weapon = weapon.Replace("  ", " ");
        }

        /// <summary>"Steady Bolt (10 dmg, 2/s, 8m range)" → name "Steady Bolt", details "10 dmg, 2/s, 8m range".</summary>
        public static void SplitWeapon(string weapon, out string name, out string details)
        {
            name = weapon ?? string.Empty;
            details = string.Empty;
            if (string.IsNullOrEmpty(weapon)) return;
            int open = weapon.IndexOf('(');
            if (open <= 0) return;
            name = weapon.Substring(0, open).Trim();
            int close = weapon.LastIndexOf(')');
            details = close > open ? weapon.Substring(open + 1, close - open - 1).Trim() : weapon.Substring(open + 1).Trim();
        }

        private void TogglePassive() => ToggleDescription(passiveText);
        private void ToggleWeapon() => ToggleDescription(weaponDescriptionText);
        private void ToggleUltimate() => ToggleDescription(ultimateDescriptionText);

        /// <summary>Accordion: one description expanded at a time.</summary>
        private void ToggleDescription(TMP_Text text)
        {
            if (text == null) return;
            bool expand = !text.gameObject.activeSelf;
            if (passiveNameText != null && passiveText != null) passiveText.gameObject.SetActive(false);
            if (weaponDescriptionText != null) weaponDescriptionText.gameObject.SetActive(false);
            if (ultimateDescriptionText != null) ultimateDescriptionText.gameObject.SetActive(false);
            text.gameObject.SetActive(expand);

            var layoutRoot = text.transform.parent != null ? text.transform.parent.parent as RectTransform : null;
            if (layoutRoot != null) LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
        }

        private void RefreshPagination(IReadOnlyList<HeroData> heroes, int index)
        {
            if (paginationContainer == null || paginationDotTemplate == null) return;
            UiTheme theme = UiTheme.Current;

            while (paginationDots.Count < heroes.Count)
            {
                Image dot = Instantiate(paginationDotTemplate, paginationContainer);
                dot.gameObject.SetActive(true);
                paginationDots.Add(dot);
            }

            for (int i = 0; i < paginationDots.Count; i++)
            {
                bool visible = i < heroes.Count;
                paginationDots[i].gameObject.SetActive(visible);
                if (!visible) continue;

                bool active = i == index;
                bool unlocked = selectedHeroService.IsHeroUnlocked(heroes[i]);
                paginationDots[i].color = active ? theme.gold : (unlocked ? theme.textSecondary : theme.textDisabled);
                paginationDots[i].rectTransform.localScale = active ? new Vector3(1.4f, 1.4f, 1f) : Vector3.one;
            }
        }

        private void ApplySilhouette(bool silhouette)
        {
            Transform root = spawnedPreviewModel != null ? spawnedPreviewModel.transform : heroPreviewVisualRoot;
            if (root == null) return;

            if (silhouetteBlock == null) silhouetteBlock = new MaterialPropertyBlock();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (silhouette)
                {
                    silhouetteBlock.Clear();
                    silhouetteBlock.SetColor(BaseColorId, UiTheme.Current.surfaceSunken);
                    renderers[i].SetPropertyBlock(silhouetteBlock);
                }
                else
                {
                    renderers[i].SetPropertyBlock(null);
                }
            }
        }

        private void UpdatePreviewModel(HeroData hero)
        {
            if (heroPreviewVisualRoot == null || hero == null) return;

            if (spawnedPreviewModel != null)
            {
                Destroy(spawnedPreviewModel);
                spawnedPreviewModel = null;
            }

            if (hero.visualModelPrefab != null)
            {
                for (int i = 0; i < heroPreviewVisualRoot.childCount; i++)
                {
                    heroPreviewVisualRoot.GetChild(i).gameObject.SetActive(false);
                }

                spawnedPreviewModel = Instantiate(hero.visualModelPrefab, heroPreviewVisualRoot);
                spawnedPreviewModel.transform.localPosition = Vector3.zero;
                spawnedPreviewModel.transform.localRotation = Quaternion.identity;
                spawnedPreviewModel.transform.localScale = Vector3.one;
            }
            else
            {
                for (int i = 0; i < heroPreviewVisualRoot.childCount; i++)
                {
                    heroPreviewVisualRoot.GetChild(i).gameObject.SetActive(true);
                }
            }
        }

        private string FindHeroUnlockRequirement(string heroId)
        {
            if (achievementManager == null || string.IsNullOrEmpty(heroId)) return string.Empty;

            var allAchievements = achievementManager.GetAllAchievements();
            for (int i = 0; i < allAchievements.Count; i++)
            {
                var ach = allAchievements[i];
                if (ach == null) continue;
                if (ach.rewardType == AchievementRewardType.UnlockHero && ach.rewardId == heroId)
                {
                    return !string.IsNullOrEmpty(ach.description) ? ach.description : ach.title;
                }
            }

            return string.Empty;
        }
    }
}
