using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.UI
{
    public class GameOverUI : MonoBehaviour
    {
        private static readonly Color WeaponBarColor = new Color(0.95f, 0.78f, 0.25f, 1f);
        private static readonly Color UltimateBarColor = new Color(0.85f, 0.36f, 0.98f, 1f);
        private static readonly Color BarrelBarColor = new Color(1f, 0.44f, 0.16f, 1f);
        private static readonly Color SynergyBarColor = new Color(1f, 0.84f, 0.2f, 1f);
        private static readonly Color AbilityBarColor = new Color(0.28f, 0.78f, 1f, 1f);

        [Header("Core Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Damage Breakdown Section")]
        [SerializeField] private GameObject damageBreakdownSection;
        [SerializeField] private Transform damageBreakdownContainer;
        [SerializeField, Min(1)] private int maxBreakdownRows = 12;
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private SynergyCatalog synergyCatalog;
        [SerializeField] private HeroCatalog heroCatalog;

        [Header("Run Achievements Section")]
        [SerializeField] private GameObject achievementsSection;
        [SerializeField] private Transform achievementsContainer;

        [Header("Ad Buttons")]
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button doubleCoinsButton;
        [SerializeField] private TextMeshProUGUI doubleCoinsButtonText;

        private GameManager gameManager;
        private AchievementManager achievementManager;
        private RunStatistics runStatistics;
        private RunComboTracker comboTracker;
        private SelectedHeroService selectedHeroService;
        private readonly List<GameObject> spawnedAchievementRows = new List<GameObject>();
        private readonly List<GameObject> spawnedBreakdownRows = new List<GameObject>();
        private readonly List<DamageSourceEntry> breakdownBuffer = new List<DamageSourceEntry>(16);

        [Inject]
        public void Construct(
            GameManager gameManager,
            AchievementManager achievementManager,
            RunStatistics runStatistics = null,
            SelectedHeroService selectedHeroService = null,
            RunComboTracker comboTracker = null)
        {
            this.gameManager = gameManager;
            this.achievementManager = achievementManager;
            this.runStatistics = runStatistics;
            this.selectedHeroService = selectedHeroService;
            this.comboTracker = comboTracker;

            if (this.gameManager != null)
            {
                this.gameManager.OnGameOver += Show;
                this.gameManager.OnHeroRevived += Hide;
                this.gameManager.OnCoinsDoubled += HandleCoinsDoubled;
            }
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameOver -= Show;
                gameManager.OnHeroRevived -= Hide;
                gameManager.OnCoinsDoubled -= HandleCoinsDoubled;
            }
        }

        public void Show(int currentWave, int coinsEarned)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (waveText != null)
            {
                waveText.text = $"Wave Reached: {currentWave}";
            }

            if (coinsText != null)
            {
                int shards = runStatistics != null ? runStatistics.BossShardsEarned : 0;
                coinsText.text = shards > 0
                    ? $"Coins Earned: {coinsEarned}   <color=#B98CFF>Boss Shards: +{shards}</color>"
                    : $"Coins Earned: {coinsEarned}";
            }

            if (statsText != null)
            {
                int kills = runStatistics != null ? runStatistics.EnemiesKilled : 0;
                float seconds = runStatistics != null ? runStatistics.PlayTime : 0f;
                int mins = Mathf.FloorToInt(seconds / 60f);
                int secs = Mathf.FloorToInt(seconds % 60f);
                int bestCombo = Mathf.Max(
                    runStatistics != null ? runStatistics.BestCombo : 0,
                    comboTracker != null ? comboTracker.BestCombo : 0);

                statsText.text = bestCombo >= 2
                    ? $"Kills: {kills}   •   Time: {mins:00}:{secs:00}   •   Best Combo: x{bestCombo}"
                    : $"Kills: {kills}   •   Time: {mins:00}:{secs:00}";
            }

            PopulateDamageBreakdown();
            PopulateRunAchievements();
            RefreshAdButtons();
        }

        public void PopulateDamageBreakdown()
        {
            for (int i = 0; i < spawnedBreakdownRows.Count; i++)
            {
                if (spawnedBreakdownRows[i] != null) Destroy(spawnedBreakdownRows[i]);
            }
            spawnedBreakdownRows.Clear();

            breakdownBuffer.Clear();
            if (runStatistics != null && runStatistics.TotalDamageDealt > 0)
            {
                runStatistics.GetTopDamageSources(breakdownBuffer, maxBreakdownRows);
            }

            bool hasData = breakdownBuffer.Count > 0;
            if (damageBreakdownSection != null)
            {
                damageBreakdownSection.SetActive(hasData);
            }

            if (!hasData || damageBreakdownContainer == null) return;

            TMP_FontAsset sharedFont = statsText != null ? statsText.font : null;
            HeroData hero = selectedHeroService != null ? selectedHeroService.GetSelectedHero() : null;
            SynergyCatalog synCat = synergyCatalog != null ? synergyCatalog : Resources.Load<SynergyCatalog>("SynergyCatalog");

            for (int i = 0; i < breakdownBuffer.Count; i++)
            {
                DamageSourceEntry entry = breakdownBuffer[i];
                ResolveSourceMetadata(
                    entry.SourceId,
                    hero,
                    abilityCatalog,
                    synCat,
                    out string displayName,
                    out Sprite icon,
                    out Color barColor);

                GameObject row = CreateBreakdownRow(entry, displayName, icon, barColor, sharedFont);
                row.transform.SetParent(damageBreakdownContainer, false);
                spawnedBreakdownRows.Add(row);
            }

            ResetScrollToTop(damageBreakdownContainer);
        }

        private static void ResetScrollToTop(Transform content)
        {
            var scrollRect = content != null ? content.GetComponentInParent<ScrollRect>(true) : null;
            if (scrollRect == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        private static void ResolveSourceMetadata(
            string sourceId,
            HeroData hero,
            AbilityCatalog abCatalog,
            SynergyCatalog synCatalog,
            out string displayName,
            out Sprite icon,
            out Color barColor)
        {
            icon = null;

            if (sourceId == DamageSourceIds.Weapon)
            {
                displayName = hero != null && !string.IsNullOrEmpty(hero.heroName)
                    ? $"{hero.heroName} Weapon"
                    : "Base Weapon";
                icon = hero != null ? hero.icon : null;
                barColor = WeaponBarColor;
                return;
            }

            if (sourceId == DamageSourceIds.Ultimate)
            {
                displayName = hero != null && !string.IsNullOrEmpty(hero.ultimateName)
                    ? $"Ult: {hero.ultimateName}"
                    : "Ultimate";
                icon = hero != null ? (hero.ultimateIcon != null ? hero.ultimateIcon : hero.icon) : null;
                barColor = UltimateBarColor;
                return;
            }

            if (sourceId == DamageSourceIds.Barrel)
            {
                displayName = "Explosive Barrel";
                barColor = BarrelBarColor;
                return;
            }

            if (synCatalog != null)
            {
                SynergyData syn = synCatalog.GetById(sourceId);
                if (syn != null)
                {
                    displayName = $"{syn.synergyName} (Synergy)";
                    icon = syn.icon;
                    barColor = SynergyBarColor;
                    return;
                }
            }

            if (abCatalog != null && abCatalog.abilities != null)
            {
                for (int i = 0; i < abCatalog.abilities.Count; i++)
                {
                    AbilityData ab = abCatalog.abilities[i];
                    if (ab != null && ab.id == sourceId)
                    {
                        displayName = ab.abilityName;
                        icon = ab.icon;
                        barColor = AbilityHudSlotUI.GetRarityColor(ab.rarity);
                        return;
                    }
                }
            }

            displayName = RewardDisplayFormatter.ResolveAbilityName(sourceId, abCatalog);
            barColor = AbilityBarColor;
        }

        private static GameObject CreateBreakdownRow(
            DamageSourceEntry entry,
            string displayName,
            Sprite icon,
            Color barColor,
            TMP_FontAsset sharedFont)
        {
            var row = new GameObject($"DmgRow_{entry.SourceId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;
            ((RectTransform)row.transform).sizeDelta = new Vector2(0f, 52f);

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.11f, 0.13f, 0.18f, 0.92f);
            bg.raycastTarget = false;

            // Left Icon Box
            var iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            iconBoxGo.transform.SetParent(row.transform, false);
            var iconBoxRt = (RectTransform)iconBoxGo.transform;
            iconBoxRt.anchorMin = new Vector2(0f, 0.5f);
            iconBoxRt.anchorMax = new Vector2(0f, 0.5f);
            iconBoxRt.pivot = new Vector2(0f, 0.5f);
            iconBoxRt.anchoredPosition = new Vector2(8f, 0f);
            iconBoxRt.sizeDelta = new Vector2(40f, 40f);
            var iconBoxImg = iconBoxGo.GetComponent<Image>();
            iconBoxImg.color = new Color(0.07f, 0.09f, 0.13f, 1f);
            iconBoxImg.raycastTarget = false;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(iconBoxGo.transform, false);
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(3f, 3f);
                iconRt.offsetMax = new Vector2(-3f, -3f);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                var initGo = new GameObject("Initials", typeof(RectTransform), typeof(TextMeshProUGUI));
                initGo.transform.SetParent(iconBoxGo.transform, false);
                var initRt = (RectTransform)initGo.transform;
                initRt.anchorMin = Vector2.zero;
                initRt.anchorMax = Vector2.one;
                initRt.offsetMin = Vector2.zero;
                initRt.offsetMax = Vector2.zero;
                var initTmp = initGo.GetComponent<TextMeshProUGUI>();
                if (sharedFont != null) initTmp.font = sharedFont;
                initTmp.text = AbilityHudSlotUI.GetInitials(displayName);
                initTmp.fontSize = 18f;
                initTmp.fontStyle = FontStyles.Bold;
                initTmp.alignment = TextAlignmentOptions.Center;
                initTmp.color = barColor;
                initTmp.raycastTarget = false;
            }

            // Right content area (x >= 58px)
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(row.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(58f, 5f);
            contentRt.offsetMax = new Vector2(-12f, -4f);

            // Top Left: Name
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(contentGo.transform, false);
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.anchorMin = new Vector2(0f, 0.44f);
            nameRt.anchorMax = new Vector2(0.56f, 1f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            if (sharedFont != null) nameTmp.font = sharedFont;
            nameTmp.text = displayName;
            nameTmp.fontSize = 20f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.color = Color.white;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 14f;
            nameTmp.fontSizeMax = 20f;
            nameTmp.raycastTarget = false;

            // Top Right: Damage, DPS, Share%
            var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
            statsGo.transform.SetParent(contentGo.transform, false);
            var statsRt = (RectTransform)statsGo.transform;
            statsRt.anchorMin = new Vector2(0.56f, 0.44f);
            statsRt.anchorMax = new Vector2(1f, 1f);
            statsRt.offsetMin = Vector2.zero;
            statsRt.offsetMax = Vector2.zero;
            var statsTmp = statsGo.GetComponent<TextMeshProUGUI>();
            if (sharedFont != null) statsTmp.font = sharedFont;
            int pct = Mathf.RoundToInt(entry.Share * 100f);
            string dmgStr = RewardDisplayFormatter.FormatCompactNumber(entry.TotalDamage);
            string dpsStr = RewardDisplayFormatter.FormatCompactNumber(Mathf.RoundToInt(entry.Dps));
            statsTmp.text = $"{dmgStr}  •  {dpsStr}/s  ({pct}%)";
            statsTmp.fontSize = 19f;
            statsTmp.fontStyle = FontStyles.Bold;
            statsTmp.alignment = TextAlignmentOptions.MidlineRight;
            statsTmp.color = new Color(0.88f, 0.92f, 0.98f, 1f);
            statsTmp.enableAutoSizing = true;
            statsTmp.fontSizeMin = 13f;
            statsTmp.fontSizeMax = 19f;
            statsTmp.raycastTarget = false;

            // Bottom Share Bar
            var barBgGo = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(contentGo.transform, false);
            var barBgRt = (RectTransform)barBgGo.transform;
            barBgRt.anchorMin = new Vector2(0f, 0.08f);
            barBgRt.anchorMax = new Vector2(1f, 0.34f);
            barBgRt.offsetMin = Vector2.zero;
            barBgRt.offsetMax = Vector2.zero;
            var barBgImg = barBgGo.GetComponent<Image>();
            barBgImg.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            barBgImg.raycastTarget = false;

            var barFillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRt = (RectTransform)barFillGo.transform;
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = new Vector2(Mathf.Clamp01(entry.Share), 1f);
            barFillRt.offsetMin = Vector2.zero;
            barFillRt.offsetMax = Vector2.zero;
            var barFillImg = barFillGo.GetComponent<Image>();
            barFillImg.color = barColor;
            barFillImg.raycastTarget = false;

            return row;
        }

        private void RefreshAdButtons()
        {
            bool canRevive = gameManager != null && gameManager.CanRevive;
            bool canDouble = gameManager != null && gameManager.CanDoubleCoins;

            if (reviveButton != null)
            {
                reviveButton.interactable = canRevive;
            }

            if (doubleCoinsButton != null)
            {
                doubleCoinsButton.interactable = canDouble;
            }

            if (doubleCoinsButtonText != null)
            {
                doubleCoinsButtonText.text = (gameManager != null && gameManager.HasDoubledCoinsThisRun)
                    ? "DOUBLED!"
                    : "x2 COINS (AD)";
            }
        }

        private void HandleCoinsDoubled(int newTotal)
        {
            if (coinsText != null)
            {
                coinsText.text = $"Coins Earned: {newTotal} (x2!)";
            }
            RefreshAdButtons();
        }

        private void PopulateRunAchievements()
        {
            for (int i = 0; i < spawnedAchievementRows.Count; i++)
            {
                if (spawnedAchievementRows[i] != null) Destroy(spawnedAchievementRows[i]);
            }
            spawnedAchievementRows.Clear();

            bool hasUnlocks = achievementManager != null
                && achievementManager.unlockedThisRun != null
                && achievementManager.unlockedThisRun.Count > 0;

            if (achievementsSection != null)
            {
                achievementsSection.SetActive(hasUnlocks);
            }

            if (!hasUnlocks || achievementsContainer == null) return;

            var unlocks = achievementManager.unlockedThisRun;
            for (int i = 0; i < unlocks.Count; i++)
            {
                var ach = unlocks[i];
                if (ach == null) continue;

                GameObject row = CreateAchievementRow(ach, abilityCatalog, heroCatalog);
                row.transform.SetParent(achievementsContainer, false);
                spawnedAchievementRows.Add(row);
            }
        }

        private static GameObject CreateAchievementRow(
            AchievementData ach,
            AbilityCatalog abilityCatalog = null,
            HeroCatalog heroCatalog = null)
        {
            var row = new GameObject($"Unlocked_{ach.id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.22f, 0.19f, 0.08f, 0.92f);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(row.transform, false);
            var rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 4f);
            rt.offsetMax = new Vector2(-16f, -4f);

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            string rewardStr = FormatReward(ach, abilityCatalog, heroCatalog);
            tmp.text = string.IsNullOrEmpty(rewardStr)
                ? $"<b>{ach.title}</b>"
                : $"<b>{ach.title}</b>  <color=#FFD54F>({rewardStr})</color>";
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 15f;
            tmp.fontSizeMax = 22f;
            tmp.raycastTarget = false;

            return row;
        }

        public static string FormatReward(
            AchievementData ach,
            AbilityCatalog abilityCatalog = null,
            HeroCatalog heroCatalog = null)
        {
            return RewardDisplayFormatter.FormatReward(ach, abilityCatalog, heroCatalog);
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void OnRestartClicked()
        {
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }

        public void OnMenuClicked()
        {
            if (gameManager != null)
            {
                gameManager.GoToMenu();
            }
        }

        public void OnReviveClicked()
        {
            if (gameManager != null)
            {
                gameManager.ReviveHero();
            }
        }

        public void OnDoubleCoinsClicked()
        {
            if (gameManager != null)
            {
                gameManager.TryDoubleCoins();
            }
        }
    }
}
