using UnityEngine;
using UnityEngine.UI;
using ProjectB.Player;

namespace ProjectB.UI
{
    public class HpBarUI : MonoBehaviour
    {
        [SerializeField] private HeroHealth heroHealth;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI hpText;

        private void OnEnable()
        {
            Subscribe();
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
            if (heroHealth == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    heroHealth = player.GetComponent<HeroHealth>();
                }
            }

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
