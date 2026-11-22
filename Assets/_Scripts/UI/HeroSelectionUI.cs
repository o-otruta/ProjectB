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
    public class HeroSelectionUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
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
        }

        public void SelectNext()
        {
            if (selectedHeroService == null) return;
            var heroes = selectedHeroService.GetAllHeroes();
            if (heroes.Count <= 1) return;

            currentIndex = (currentIndex + 1) % heroes.Count;
            ApplyCurrentHeroIfUnlocked();
            RefreshUI();
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

            IReadOnlyList<HeroData> heroes = selectedHeroService.GetAllHeroes();
            bool hasMultiple = heroes.Count > 1;

            if (prevHeroButton != null) prevHeroButton.gameObject.SetActive(hasMultiple);
            if (nextHeroButton != null) nextHeroButton.gameObject.SetActive(hasMultiple);

            if (heroes.Count == 0) return;

            currentIndex = Mathf.Clamp(currentIndex, 0, heroes.Count - 1);
            HeroData hero = heroes[currentIndex];
            if (hero == null) return;

            bool isUnlocked = selectedHeroService.IsHeroUnlocked(hero);

            if (heroNameText != null)
            {
                heroNameText.text = hero.heroName.ToUpperInvariant();
            }

            if (heroStatusText != null)
            {
                if (isUnlocked)
                {
                    heroStatusText.text = "READY FOR BATTLE";
                    heroStatusText.color = new Color(0.45f, 0.92f, 0.55f, 1f);
                }
                else
                {
                    string req = FindHeroUnlockRequirement(hero.id);
                    heroStatusText.text = string.IsNullOrEmpty(req) ? "LOCKED" : $"LOCKED — {req}";
                    heroStatusText.color = new Color(1f, 0.7f, 0.25f, 1f);
                }
            }

            if (passiveText != null)
            {
                passiveText.text = $"<b>Passive:</b> {hero.passiveName} — {hero.passiveDescription}";
            }

            if (ultimateText != null)
            {
                ultimateText.text = $"<b>Ultimate:</b> {hero.ultimateName} — {hero.ultimateDescription} ({hero.ultimateCooldown:0}s CD)";
            }

            if (statsText != null)
            {
                float passiveHpMult = hero.passiveType == HeroPassiveType.MaxHpPercent
                    ? 1f + hero.passiveValue / 100f
                    : 1f;
                float metaHpMult = metaUpgradeManager != null
                    ? 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP) / 100f
                    : 1f;
                int effectiveHp = Mathf.RoundToInt(hero.maxHp * passiveHpMult * metaHpMult);

                float passiveSpeedMult = hero.passiveType == HeroPassiveType.MoveSpeedPercent
                    ? 1f + hero.passiveValue / 100f
                    : 1f;
                float metaSpeedMult = metaUpgradeManager != null
                    ? 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroSpeed) / 100f
                    : 1f;
                float effectiveSpeed = hero.moveSpeed * passiveSpeedMult * metaSpeedMult;

                statsText.text = $"HP: {effectiveHp}   •   Speed: {effectiveSpeed:0.0}";
            }

            if (startRunButton != null)
            {
                startRunButton.interactable = isUnlocked;
            }

            if (startButtonText != null)
            {
                startButtonText.text = isUnlocked ? "START" : "LOCKED";
            }

            UpdatePreviewModel(hero);
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
