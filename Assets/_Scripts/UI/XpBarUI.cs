using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    public class XpBarUI : MonoBehaviour
    {
        [SerializeField] private LevelUp.HeroExperience heroExperience;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI levelText;

        private void OnEnable()
        {
            if (heroExperience != null)
            {
                heroExperience.OnXPChanged += UpdateXpBar;
                heroExperience.OnLevelUp += UpdateLevelText;
            }
        }

        private void OnDisable()
        {
            if (heroExperience != null)
            {
                heroExperience.OnXPChanged -= UpdateXpBar;
                heroExperience.OnLevelUp -= UpdateLevelText;
            }
        }

        private void Start()
        {
            if (heroExperience == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    heroExperience = player.GetComponent<LevelUp.HeroExperience>();
                    if (heroExperience != null)
                    {
                        heroExperience.OnXPChanged += UpdateXpBar;
                        heroExperience.OnLevelUp += UpdateLevelText;
                    }
                }
            }

            if (heroExperience != null)
            {
                UpdateLevelText(heroExperience.CurrentLevel);
                UpdateXpBar(heroExperience.CurrentXP, heroExperience.XPToNextLevel);
            }
        }

        private void UpdateXpBar(int currentXp, int nextLevelXp)
        {
            if (xpSlider != null)
            {
                xpSlider.maxValue = nextLevelXp;
                xpSlider.value = currentXp;
            }
        }

        private void UpdateLevelText(int newLevel)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv {newLevel}";
            }
        }
    }
}
