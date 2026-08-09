using UnityEngine;
using UnityEngine.UI;
using ProjectB.Player;
using VContainer;

namespace ProjectB.UI
{
    public class HpBarUI : MonoBehaviour
    {
        private HeroHealth heroHealth;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI hpText;
        private bool isInitialized = false;

        [Inject]
        public void Construct(HeroHealth heroHealth)
        {
            this.heroHealth = heroHealth;
            isInitialized = true;
            if (gameObject.activeInHierarchy)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (heroHealth != null)
            {
                heroHealth.OnHealthChanged -= UpdateHpBar;
            }
        }

        private void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (heroHealth == null) return;

            if (heroHealth != null)
            {
                heroHealth.OnHealthChanged -= UpdateHpBar;
                heroHealth.OnHealthChanged += UpdateHpBar;
                UpdateHpBar(heroHealth.CurrentHp, heroHealth.MaxHp);
            }
        }

        private void UpdateHpBar(int currentHp, int maxHp)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHp;
                hpSlider.value = currentHp;
            }
            if (hpText != null)
            {
                hpText.text = $"{currentHp} / {maxHp}";
            }
        }
    }
}
