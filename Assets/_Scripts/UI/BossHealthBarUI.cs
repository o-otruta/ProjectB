using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
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

        [Header("Colors")]
        [SerializeField] private Color bossFillColor = new Color(0.86f, 0.14f, 0.2f, 1f);
        [SerializeField] private Color miniBossFillColor = new Color(0.95f, 0.52f, 0.14f, 1f);

        private GameEventBus eventBus;
        private string activeBossId;
        private bool isSubscribed;

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
                    ? "BOSS"
                    : evt.DisplayName.ToUpperInvariant();
            }

            if (fillImage != null)
            {
                fillImage.color = evt.IsMiniBoss ? miniBossFillColor : bossFillColor;
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

            if (hpValueText != null)
            {
                hpValueText.text = $"{safeCur} / {safeMax}";
            }
        }

        public void Hide()
        {
            activeBossId = null;
            if (barRoot != null) barRoot.SetActive(false);
        }
    }
}
