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
        private int lastDisplayedHp = int.MinValue;
        private int lastDisplayedMaxHp = int.MinValue;
        private readonly char[] hpCharBuffer = new char[24];

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

            heroHealth.OnHealthChanged -= UpdateHpBar;
            heroHealth.OnHealthChanged += UpdateHpBar;
            UpdateHpBar(heroHealth.CurrentHp, heroHealth.MaxHp);
        }

        private void UpdateHpBar(int currentHp, int maxHp)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHp;
                hpSlider.value = currentHp;
            }
            if (hpText != null && (currentHp != lastDisplayedHp || maxHp != lastDisplayedMaxHp))
            {
                lastDisplayedHp = currentHp;
                lastDisplayedMaxHp = maxHp;
                int len = WriteRatioToBuffer(hpCharBuffer, currentHp, maxHp);
                hpText.SetCharArray(hpCharBuffer, 0, len);
            }
        }

        public static int WriteRatioToBuffer(char[] buffer, int current, int max)
        {
            int pos = WriteNonNegativeInt(buffer, 0, Mathf.Max(0, current));
            buffer[pos++] = ' ';
            buffer[pos++] = '/';
            buffer[pos++] = ' ';
            pos = WriteNonNegativeInt(buffer, pos, Mathf.Max(0, max));
            return pos;
        }

        private static int WriteNonNegativeInt(char[] buffer, int startIndex, int value)
        {
            if (value <= 0)
            {
                buffer[startIndex] = '0';
                return startIndex + 1;
            }

            int temp = value;
            int digits = 0;
            while (temp > 0)
            {
                temp /= 10;
                digits++;
            }

            int endIndex = startIndex + digits;
            int writeIdx = endIndex - 1;
            temp = value;
            while (temp > 0)
            {
                buffer[writeIdx--] = (char)('0' + (temp % 10));
                temp /= 10;
            }

            return endIndex;
        }
    }
}
