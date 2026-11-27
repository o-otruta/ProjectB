using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float BREAKDOWN_ROW_HEIGHT = 80f;
        private const float BREAKDOWN_ROW_SPACING = 6f;

        internal enum StatFormat : byte
        {
            Grouped = 0,
            Clock = 1,
            Combo = 2,
            Template = 3
        }

        private struct StatCounter
        {
            public bool active;
            public TMP_Text text;
            public StatFormat format;
            public int targetValue;
            public int lastWritten;
            public float elapsed;
            public float duration;
            public float delay;
            public float pulseAmplitude;
            public float pulseCycles;
            public string prefix;
            public string suffix;
        }

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

        [Header("Navigation Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;

        [Header("Sprint 13: redesigned layout")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private TMP_Text killsValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text comboValueText;
        [SerializeField] private GameObject comboChip;
        [SerializeField] private CurrencyLabel coinsLabel;
        [SerializeField] private CurrencyLabel shardsLabel;
        [SerializeField] private GameObject shardsChip;
        [Tooltip("LayoutElement of the breakdown section: height follows the row count up to the max, then scrolls.")]
        [SerializeField] private LayoutElement breakdownLayout;
        [SerializeField] private float breakdownHeaderHeight = 86f;
        [SerializeField] private float breakdownMaxHeight = 420f;
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Count-Up Animation")]
        [SerializeField, Min(0f)] private float countUpDuration = 0.90f;
        [SerializeField, Min(0f)] private float countStaggerDelay = 0.07f;
        [SerializeField, Range(0f, 0.45f)] private float countPulseAmplitude = 0.24f;
        [SerializeField, Min(0.5f)] private float countPulseCycles = 4f;
        [SerializeField, Range(0f, 0.35f)] private float countFinishPunch = 0.18f;

        private GameManager gameManager;
        private AchievementManager achievementManager;
        private RunStatistics runStatistics;
        private RunComboTracker comboTracker;
        private SelectedHeroService selectedHeroService;
        private SaveManager saveManager;
        private InputModeService inputModeService;
        private Action<InputMode> cachedInputModeHandler;
        private int bestWaveBeforeRun = -1;
        private readonly char[] numberBuffer = new char[64];
        private readonly StatCounter[] statCounters = new StatCounter[4];
        private int activeCounterCount;
        private readonly List<GameObject> spawnedAchievementRows = new List<GameObject>();
        private readonly List<GameObject> spawnedBreakdownRows = new List<GameObject>();
        private readonly List<DamageSourceEntry> breakdownBuffer = new List<DamageSourceEntry>(16);

        public bool IsCounting => activeCounterCount > 0;

        [Inject]
        public void Construct(
            GameManager gameManager,
            AchievementManager achievementManager,
            RunStatistics runStatistics = null,
            SelectedHeroService selectedHeroService = null,
            RunComboTracker comboTracker = null,
            SaveManager saveManager = null,
            InputModeService inputModeService = null)
        {
            UnsubscribeInputMode();
            this.saveManager = saveManager;
            // Snapshot the record before this run so Game Over can show NEW BEST!
            // (GameManager writes bestWave before raising OnGameOver).
            bestWaveBeforeRun = saveManager != null && saveManager.Data != null ? saveManager.Data.bestWave : -1;
            this.gameManager = gameManager;
            this.achievementManager = achievementManager;
            this.runStatistics = runStatistics;
            this.selectedHeroService = selectedHeroService;
            this.comboTracker = comboTracker;
            this.inputModeService = inputModeService;

            if (this.gameManager != null)
            {
                this.gameManager.OnGameOver += Show;
                this.gameManager.OnHeroRevived += Hide;
                this.gameManager.OnCoinsDoubled += HandleCoinsDoubled;
            }
            SubscribeInputMode();
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            StopCounters(snapToTarget: true);
        }

        private void OnDestroy()
        {
            UnsubscribeInputMode();
            if (gameManager != null)
            {
                gameManager.OnGameOver -= Show;
                gameManager.OnHeroRevived -= Hide;
                gameManager.OnCoinsDoubled -= HandleCoinsDoubled;
            }
        }

        private void Update()
        {
            if (activeCounterCount <= 0) return;
            TickCounters(Time.unscaledDeltaTime);
        }

        private void SubscribeInputMode()
        {
            if (inputModeService == null) return;
            cachedInputModeHandler ??= HandleInputModeChanged;
            inputModeService.InputModeChanged -= cachedInputModeHandler;
            inputModeService.InputModeChanged += cachedInputModeHandler;
        }

        private void UnsubscribeInputMode()
        {
            if (inputModeService != null && cachedInputModeHandler != null)
            {
                inputModeService.InputModeChanged -= cachedInputModeHandler;
            }
        }

        private void HandleInputModeChanged(InputMode mode)
        {
            if (mode == InputMode.Touch)
            {
                UiFocusHelper.ClearSelection();
            }
            else if (panel != null && panel.activeSelf && EventSystem.current != null &&
                     EventSystem.current.currentSelectedGameObject == null)
            {
                FocusPrimaryAction();
            }
        }

        private void FocusPrimaryAction()
        {
            Selectable first = restartButton != null ? (Selectable)restartButton : menuButton;
            if (first == null)
            {
                first = UiFocusHelper.FindFirstInteractable(panel != null ? panel.transform : transform);
            }
            UiFocusHelper.SetFirstSelected(first, inputModeService);
        }

        public void Show(int currentWave, int coinsEarned)
        {
            StopCounters(snapToTarget: false);

            if (panel != null)
            {
                panel.SetActive(true);
            }

            UiTheme theme = UiTheme.Current;
            if (titleText != null)
            {
                titleText.text = Loc.Get("ui.gameover.defeated", "DEFEATED");
                titleText.color = theme.danger;
                UiTween.Scale(titleText.transform, Vector3.one * 1.6f, Vector3.one, theme.durationSlow, 0f, UiEase.OutBack);
            }
            if (panelGroup != null) UiTween.Fade(panelGroup, 0f, 1f, theme.durationSlow);

            if (waveText != null)
            {
                string waveKey = titleText != null ? "ui.gameover.wave_upper" : "ui.gameover.wave_reached";
                string waveFallback = titleText != null ? "WAVE {0}" : "Wave Reached: {0}";
                string waveTemplate = Loc.Get(waveKey, waveFallback);
                if (SplitFormatTemplate(waveTemplate, out string wavePrefix, out string waveSuffix))
                {
                    StartCounter(0, waveText, StatFormat.Template, currentWave, 0.05f, countUpDuration * 0.88f, countPulseAmplitude * 0.85f, wavePrefix, waveSuffix);
                }
                else
                {
                    ClearCounter(0);
                    waveText.text = Loc.Format(waveKey, currentWave);
                }
            }
            else
            {
                ClearCounter(0);
            }

            bool isNewBest = bestWaveBeforeRun >= 0 && currentWave > bestWaveBeforeRun;
            if (newBestBadge != null)
            {
                newBestBadge.SetActive(isNewBest);
                if (isNewBest)
                {
                    float waveDur = currentWave <= 2 ? Mathf.Min(countUpDuration * 0.88f, 0.40f) : countUpDuration * 0.88f;
                    float badgeDelay = (currentWave > 0 && countUpDuration > 0f) ? Mathf.Max(0.25f, 0.05f + waveDur - 0.04f) : 0.25f;
                    UiTween.Scale(newBestBadge.transform, Vector3.zero, Vector3.one, theme.durationSlow, badgeDelay, UiEase.OutBack);
                }
            }

            int shardsEarned = runStatistics != null ? runStatistics.BossShardsEarned : 0;
            if (coinsLabel != null)
            {
                coinsLabel.SetValue(0, animate: false);
                coinsLabel.SetValue(coinsEarned, animate: true, delay: 0.05f + countStaggerDelay * 4f);
            }
            if (shardsChip != null) shardsChip.SetActive(shardsEarned > 0);
            if (shardsLabel != null)
            {
                shardsLabel.SetValue(0, animate: false);
                if (shardsEarned > 0)
                {
                    shardsLabel.SetValue(shardsEarned, animate: true, delay: 0.05f + countStaggerDelay * 4.5f);
                }
            }

            int killCount = runStatistics != null ? runStatistics.EnemiesKilled : 0;
            float playSeconds = runStatistics != null ? runStatistics.PlayTime : 0f;
            int comboBest = Mathf.Max(
                runStatistics != null ? runStatistics.BestCombo : 0,
                comboTracker != null ? comboTracker.BestCombo : 0);
            StartCounter(1, killsValueText, StatFormat.Grouped, killCount, 0.05f + countStaggerDelay, countUpDuration, countPulseAmplitude);
            StartCounter(2, timeValueText, StatFormat.Clock, Mathf.FloorToInt(playSeconds), 0.05f + countStaggerDelay * 2f, countUpDuration, countPulseAmplitude);
            if (comboChip != null) comboChip.SetActive(comboBest >= 2);
            if (comboBest >= 2)
            {
                StartCounter(3, comboValueText, StatFormat.Combo, comboBest, 0.05f + countStaggerDelay * 3f, countUpDuration * 0.9f, countPulseAmplitude);
            }
            else
            {
                ClearCounter(3);
                if (comboValueText != null)
                {
                    UiTween.Kill(comboValueText.transform, complete: false);
                    comboValueText.transform.localScale = Vector3.one;
                    numberBuffer[0] = 'x';
                    int len = UiFormat.WriteInt(numberBuffer, 1, comboBest);
                    comboValueText.SetCharArray(numberBuffer, 0, len);
                }
            }

            if (coinsText != null)
            {
                int shards = runStatistics != null ? runStatistics.BossShardsEarned : 0;
                string coinsEarnedStr = Loc.Format("ui.gameover.coins_earned", coinsEarned);
                coinsText.text = shards > 0
                    ? $"{coinsEarnedStr}   <color=#B98CFF>{Loc.Format("ui.gameover.boss_shards_plus", shards)}</color>"
                    : coinsEarnedStr;
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

                string timeClock = $"{mins:00}:{secs:00}";
                statsText.text = bestCombo >= 2
                    ? Loc.Format("ui.gameover.stats_with_combo", kills, timeClock, bestCombo)
                    : Loc.Format("ui.gameover.stats_no_combo", kills, timeClock);
            }

            PopulateDamageBreakdown();
            PopulateRunAchievements();
            RefreshAdButtons();
            FocusPrimaryAction();
        }

        /// <summary>Snaps any active stat &amp; reward count-up animations to their final values.</summary>
        public void CompleteCountAnimations()
        {
            StopCounters(snapToTarget: true);
            if (coinsLabel != null) coinsLabel.CompleteAnimation();
            if (shardsLabel != null) shardsLabel.CompleteAnimation();
        }

        internal void StartCounter(
            int slot,
            TMP_Text text,
            StatFormat format,
            int targetValue,
            float delay,
            float baseDuration,
            float pulseAmp,
            string prefix = null,
            string suffix = null,
            bool allowInEditMode = false)
        {
            ClearCounter(slot);
            if (text == null) return;

            UiTween.Kill(text.transform, complete: false);
            text.transform.localScale = Vector3.one;

            var entry = new StatCounter
            {
                active = false,
                text = text,
                format = format,
                targetValue = Mathf.Max(0, targetValue),
                lastWritten = int.MinValue,
                prefix = prefix,
                suffix = suffix
            };

            if (entry.targetValue <= 0 || baseDuration <= 0f || (!allowInEditMode && (!Application.isPlaying || !isActiveAndEnabled)))
            {
                WriteCounterValue(ref entry, entry.targetValue);
                return;
            }

            entry.active = true;
            entry.delay = Mathf.Max(0f, delay);
            entry.elapsed = 0f;
            entry.duration = entry.targetValue <= 2 ? Mathf.Min(baseDuration, 0.40f) : baseDuration;
            entry.pulseAmplitude = pulseAmp;
            entry.pulseCycles = entry.targetValue <= 1 ? 1f : (entry.targetValue == 2 ? 2f : countPulseCycles);
            WriteCounterValue(ref entry, 0);
            statCounters[slot] = entry;
            activeCounterCount++;
        }

        internal void TickCounters(float dt)
        {
            for (int i = 0; i < statCounters.Length; i++)
            {
                if (!statCounters[i].active) continue;
                StepCounter(ref statCounters[i], dt);
            }
        }

        private void StepCounter(ref StatCounter c, float dt)
        {
            if (c.text == null)
            {
                c.active = false;
                activeCounterCount = Mathf.Max(0, activeCounterCount - 1);
                return;
            }

            float stepDt = dt;
            if (c.delay > 0f)
            {
                c.delay -= stepDt;
                if (c.delay > 0f) return;
                stepDt = -c.delay;
                c.delay = 0f;
            }

            c.elapsed += stepDt;
            float t = c.duration > 0f ? Mathf.Clamp01(c.elapsed / c.duration) : 1f;
            int val = t >= 1f
                ? c.targetValue
                : Mathf.RoundToInt(Mathf.Lerp(0f, c.targetValue, UiTween.Evaluate(UiEase.OutCubic, t)));

            if (val != c.lastWritten)
            {
                WriteCounterValue(ref c, val);
            }

            if (t < 1f)
            {
                c.text.transform.localScale = Vector3.one * UiTween.EvaluateCountPulseScale(t, c.pulseAmplitude, c.pulseCycles);
            }
            else
            {
                c.text.transform.localScale = Vector3.one;
                c.active = false;
                activeCounterCount = Mathf.Max(0, activeCounterCount - 1);
                if (countFinishPunch > 0f)
                {
                    UiTween.Punch(c.text.transform, countFinishPunch, 0.22f);
                }
            }
        }

        private void ClearCounter(int slot)
        {
            if (slot < 0 || slot >= statCounters.Length) return;
            if (statCounters[slot].active)
            {
                activeCounterCount = Mathf.Max(0, activeCounterCount - 1);
            }
            statCounters[slot] = default;
        }

        private void StopCounters(bool snapToTarget)
        {
            for (int i = 0; i < statCounters.Length; i++)
            {
                if (statCounters[i].text != null)
                {
                    UiTween.Kill(statCounters[i].text.transform, complete: false);
                    statCounters[i].text.transform.localScale = Vector3.one;
                    if (snapToTarget && statCounters[i].active)
                    {
                        WriteCounterValue(ref statCounters[i], statCounters[i].targetValue);
                    }
                }
                statCounters[i] = default;
            }
            activeCounterCount = 0;
        }

        private void WriteCounterValue(ref StatCounter c, int value)
        {
            if (c.text == null || value == c.lastWritten) return;
            c.lastWritten = value;
            int len;
            switch (c.format)
            {
                case StatFormat.Clock:
                    len = UiFormat.WriteClock(numberBuffer, 0, value);
                    break;
                case StatFormat.Combo:
                    numberBuffer[0] = 'x';
                    len = UiFormat.WriteInt(numberBuffer, 1, value);
                    break;
                case StatFormat.Template:
                    len = UiFormat.WriteString(numberBuffer, 0, c.prefix);
                    len = UiFormat.WriteInt(numberBuffer, len, value);
                    len = UiFormat.WriteString(numberBuffer, len, c.suffix);
                    break;
                default:
                    len = UiFormat.WriteGrouped(numberBuffer, 0, value);
                    break;
            }
            c.text.SetCharArray(numberBuffer, 0, len);
        }

        private static bool SplitFormatTemplate(string template, out string prefix, out string suffix)
        {
            if (string.IsNullOrEmpty(template))
            {
                prefix = string.Empty;
                suffix = string.Empty;
                return false;
            }
            int idx = template.IndexOf("{0}", StringComparison.Ordinal);
            if (idx < 0)
            {
                prefix = template;
                suffix = string.Empty;
                return false;
            }
            prefix = idx > 0 ? template.Substring(0, idx) : string.Empty;
            int after = idx + 3;
            suffix = after < template.Length ? template.Substring(after) : string.Empty;
            return true;
        }

        /// <summary>Breakdown section height for <paramref name="rows"/> rows, clamped to the scroll maximum.</summary>
        public static float CalculateBreakdownHeight(int rows, float headerHeight, float maxHeight)
        {
            if (rows <= 0) return 0f;
            float content = headerHeight + rows * BREAKDOWN_ROW_HEIGHT + Mathf.Max(0, rows - 1) * BREAKDOWN_ROW_SPACING + 16f;
            return Mathf.Min(content, maxHeight);
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

            if (breakdownLayout != null)
            {
                float h = CalculateBreakdownHeight(breakdownBuffer.Count, breakdownHeaderHeight, breakdownMaxHeight);
                breakdownLayout.preferredHeight = h;
                breakdownLayout.minHeight = h;
            }

            if (!hasData || damageBreakdownContainer == null) return;

            TMP_FontAsset sharedFont = UiTheme.Current.bodyFont != null
                ? UiTheme.Current.bodyFont
                : (statsText != null ? statsText.font : null);
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
                    ? Loc.Format("ui.gameover.hero_weapon", Loc.Name(hero))
                    : Loc.Get("ui.gameover.base_weapon", "Base Weapon");
                icon = hero != null ? hero.icon : null;
                barColor = UiTheme.Current.gold;
                return;
            }

            if (sourceId == DamageSourceIds.Ultimate)
            {
                displayName = hero != null && !string.IsNullOrEmpty(hero.ultimateName)
                    ? Loc.Format("ui.gameover.ult_prefix", Loc.UltimateName(hero))
                    : Loc.Get("ui.gameover.ultimate", "Ultimate");
                icon = hero != null ? (hero.ultimateIcon != null ? hero.ultimateIcon : hero.icon) : null;
                barColor = UiTheme.Current.rarityEpic;
                return;
            }

            if (sourceId == DamageSourceIds.Barrel)
            {
                displayName = Loc.Get("ui.gameover.barrel", "Explosive Barrel");
                barColor = UiTheme.Current.warning;
                return;
            }

            if (synCatalog != null)
            {
                SynergyData syn = synCatalog.GetById(sourceId);
                if (syn != null)
                {
                    displayName = Loc.Format("ui.gameover.synergy_suffix", Loc.Name(syn));
                    icon = syn.icon;
                    barColor = UiTheme.Current.shard;
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
                        displayName = Loc.Name(ab);
                        icon = ab.icon;
                        barColor = AbilityHudSlotUI.GetRarityColor(ab.rarity);
                        return;
                    }
                }
            }

            displayName = RewardDisplayFormatter.ResolveAbilityName(sourceId, abCatalog);
            barColor = UiTheme.Current.xpFill;
        }

        private static GameObject CreateBreakdownRow(
            DamageSourceEntry entry,
            string displayName,
            Sprite icon,
            Color barColor,
            TMP_FontAsset sharedFont)
        {
            var row = new GameObject($"DmgRow_{entry.SourceId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            UiTheme theme = UiTheme.Current;
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = BREAKDOWN_ROW_HEIGHT;
            le.preferredHeight = BREAKDOWN_ROW_HEIGHT;
            ((RectTransform)row.transform).sizeDelta = new Vector2(0f, BREAKDOWN_ROW_HEIGHT);

            var bg = row.GetComponent<Image>();
            bg.sprite = theme.rowSprite;
            bg.type = theme.rowSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = theme.surfaceRaised;
            bg.raycastTarget = false;

            // Left Icon Box
            var iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            iconBoxGo.transform.SetParent(row.transform, false);
            var iconBoxRt = (RectTransform)iconBoxGo.transform;
            iconBoxRt.anchorMin = new Vector2(0f, 0.5f);
            iconBoxRt.anchorMax = new Vector2(0f, 0.5f);
            iconBoxRt.pivot = new Vector2(0f, 0.5f);
            iconBoxRt.anchoredPosition = new Vector2(14f, 10f);
            iconBoxRt.sizeDelta = new Vector2(40f, 40f);
            var iconBoxImg = iconBoxGo.GetComponent<Image>();
            Sprite boxSprite = theme.squircleSprite != null ? theme.squircleSprite : theme.cardSprite;
            iconBoxImg.sprite = boxSprite;
            iconBoxImg.type = boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            iconBoxImg.color = theme.surfaceSunken;
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

            // Right content area (clears kit_row 27.5px bottom ledge & 7.5px top border)
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(row.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(66f, 29f);
            contentRt.offsetMax = new Vector2(-16f, -9f);

            // Top Left: Name
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(contentGo.transform, false);
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.anchorMin = new Vector2(0f, 0.40f);
            nameRt.anchorMax = new Vector2(0.56f, 1f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            if (sharedFont != null) nameTmp.font = sharedFont;
            nameTmp.text = displayName;
            nameTmp.fontSize = 20f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.color = theme.textPrimary;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 14f;
            nameTmp.fontSizeMax = 20f;
            nameTmp.raycastTarget = false;

            // Top Right: Damage, DPS, Share%
            var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
            statsGo.transform.SetParent(contentGo.transform, false);
            var statsRt = (RectTransform)statsGo.transform;
            statsRt.anchorMin = new Vector2(0.56f, 0.40f);
            statsRt.anchorMax = new Vector2(1f, 1f);
            statsRt.offsetMin = Vector2.zero;
            statsRt.offsetMax = Vector2.zero;
            var statsTmp = statsGo.GetComponent<TextMeshProUGUI>();
            if (sharedFont != null) statsTmp.font = sharedFont;
            int pct = Mathf.RoundToInt(entry.Share * 100f);
            string dmgStr = RewardDisplayFormatter.FormatCompactNumber(entry.TotalDamage);
            string dpsStr = RewardDisplayFormatter.FormatCompactNumber(Mathf.RoundToInt(entry.Dps));
            string dpsUnit = Loc.Get("ui.unit.per_second", "/s");
            statsTmp.text = $"{dmgStr}  •  {dpsStr}{dpsUnit}  ({pct}%)";
            statsTmp.fontSize = 19f;
            statsTmp.fontStyle = FontStyles.Bold;
            statsTmp.alignment = TextAlignmentOptions.MidlineRight;
            statsTmp.color = theme.textSecondary;
            statsTmp.enableAutoSizing = true;
            statsTmp.fontSizeMin = 13f;
            statsTmp.fontSizeMax = 19f;
            statsTmp.raycastTarget = false;

            // Bottom Share Bar
            var barBgGo = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(contentGo.transform, false);
            var barBgRt = (RectTransform)barBgGo.transform;
            barBgRt.anchorMin = new Vector2(0f, 0.04f);
            barBgRt.anchorMax = new Vector2(1f, 0.32f);
            barBgRt.offsetMin = Vector2.zero;
            barBgRt.offsetMax = Vector2.zero;
            var barBgImg = barBgGo.GetComponent<Image>();
            barBgImg.color = theme.surfaceSunken;
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
            bool adsSupported = gameManager == null || gameManager.IsAdsAvailable;
            bool canRevive = gameManager != null && gameManager.CanRevive;
            bool canDouble = gameManager != null && gameManager.CanDoubleCoins;

            if (reviveButton != null)
            {
                reviveButton.gameObject.SetActive(adsSupported);
                if (adsSupported)
                {
                    reviveButton.interactable = canRevive;
                }
            }

            if (doubleCoinsButton != null)
            {
                doubleCoinsButton.gameObject.SetActive(adsSupported);
                if (adsSupported)
                {
                    doubleCoinsButton.interactable = canDouble;
                }
            }

            if (doubleCoinsButtonText != null && adsSupported)
            {
                doubleCoinsButtonText.text = (gameManager != null && gameManager.HasDoubledCoinsThisRun)
                    ? Loc.Get("ui.gameover.doubled", "DOUBLED!")
                    : Loc.Get("ui.gameover.double_coins", "<sprite name=\"ad\"> x2 Coins");
            }
        }

        private void HandleCoinsDoubled(int newTotal)
        {
            if (coinsText != null)
            {
                coinsText.text = Loc.Format("ui.gameover.coins_doubled", newTotal);
            }
            if (coinsLabel != null) coinsLabel.SetValue(newTotal, animate: true);
            UiAudio.Play(SfxType.CoinPickup);
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
            le.minHeight = 72f;
            le.preferredHeight = 72f;

            UiTheme theme = UiTheme.Current;
            var bg = row.GetComponent<Image>();
            bg.sprite = theme.rowSprite;
            bg.type = theme.rowSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = UiTheme.Shade(theme.gold, 0.32f);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(row.transform, false);
            var rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18f, 28f);
            rt.offsetMax = new Vector2(-18f, -8f);

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            string rewardStr = FormatReward(ach, abilityCatalog, heroCatalog);
            string achTitle = Loc.Title(ach);
            tmp.text = string.IsNullOrEmpty(rewardStr)
                ? $"<b>{achTitle}</b>"
                : $"<b>{achTitle}</b>  <color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>({rewardStr})</color>";
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = theme.textPrimary;
            if (theme.bodyFont != null) tmp.font = theme.bodyFont;
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
            CompleteCountAnimations();
            if (panel != null)
            {
                panel.SetActive(false);
            }
            UiFocusHelper.ClearSelection();
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
