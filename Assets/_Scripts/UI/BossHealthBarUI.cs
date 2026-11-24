using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;

namespace ProjectB.UI
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject barRoot;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private TextMeshProUGUI hpValueText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Image fillImage;
        [Tooltip("Sprint 13: themed bar with a damage ghost-trail (replaces the Slider when assigned).")]
        [SerializeField] private ProgressBarUI progressBar;

        private GameEventBus eventBus;
        private string activeBossId;
        private bool isSubscribed;
        private int lastDisplayedCur = int.MinValue;
        private int lastDisplayedMax = int.MinValue;
        private readonly char[] hpCharBuffer = new char[24];

        [Inject]
        public void Construct(GameEventBus eventBus)
        {
            this.eventBus = eventBus;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            Hide();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (eventBus == null || isSubscribed) return;

            eventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            eventBus.Subscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            eventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            eventBus.Subscribe<GameOverEvent>(OnGameOver);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (eventBus == null || !isSubscribed) return;

            eventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            eventBus.Unsubscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            eventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            isSubscribed = false;
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            activeBossId = evt.BossId;

            if (barRoot != null) barRoot.SetActive(true);
            if (bossNameText != null)
            {
                bossNameText.text = string.IsNullOrEmpty(evt.DisplayName)
                    ? Loc.Get("ui.hud.boss_upper", "BOSS")
                    : evt.DisplayName.ToUpperInvariant();
            }

            if (fillImage != null)
            {
                fillImage.color = evt.IsMiniBoss ? UiTheme.Current.warning : UiTheme.Current.danger;
            }
            if (progressBar != null)
            {
                progressBar.SetFillColor(evt.IsMiniBoss ? UiColor.Warning : UiColor.Danger);
                progressBar.SetValue(evt.CurrentHp, Mathf.Max(1, evt.MaxHp), instant: true);
            }

            UpdateBar(evt.CurrentHp, evt.MaxHp);
        }

        private void OnBossHealthChanged(BossHealthChangedEvent evt)
        {
            if (barRoot == null || !barRoot.activeSelf) return;
            if (!string.IsNullOrEmpty(activeBossId) && evt.BossId != activeBossId) return;

            UpdateBar(evt.CurrentHp, evt.MaxHp);
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            if (string.IsNullOrEmpty(activeBossId) || evt.BossId == activeBossId)
            {
                Hide();
            }
        }

        private void OnGameOver(GameOverEvent _)
        {
            Hide();
        }

        private void UpdateBar(int currentHp, int maxHp)
        {
            int safeMax = Mathf.Max(1, maxHp);
            int safeCur = Mathf.Clamp(currentHp, 0, safeMax);

            if (hpSlider != null)
            {
                hpSlider.maxValue = safeMax;
                hpSlider.value = safeCur;
            }
            if (progressBar != null)
            {
                progressBar.SetValue(safeCur, safeMax);
            }

            if (hpValueText != null && (safeCur != lastDisplayedCur || safeMax != lastDisplayedMax))
            {
                lastDisplayedCur = safeCur;
                lastDisplayedMax = safeMax;
                int len = HpBarUI.WriteRatioToBuffer(hpCharBuffer, safeCur, safeMax);
                hpValueText.SetCharArray(hpCharBuffer, 0, len);
            }
        }

        public void Hide()
        {
            activeBossId = null;
            lastDisplayedCur = int.MinValue;
            lastDisplayedMax = int.MinValue;
            if (barRoot != null) barRoot.SetActive(false);
        }
    }
}
