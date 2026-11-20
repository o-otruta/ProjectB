using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.UI
{
    /// <summary>
    /// In-game developer cheat menu for fast testing (waves, bosses, god mode, levelups, coins, speed).
    /// Automatically disabled in Release builds (only active in Unity Editor and Development Builds).
    /// Toggle via on-screen "DEV" button (bottom-left) or F1 key.
    /// </summary>
    public class DebugCheatMenu : MonoBehaviour
    {
        private WaveManager waveManager;
        private HeroHealth heroHealth;
        private HeroExperience heroExperience;
        private HeroEconomy heroEconomy;
        private HeroUltimate heroUltimate;
        private GameManager gameManager;

        [Inject]
        public void Construct(
            WaveManager waveManager,
            HeroHealth heroHealth,
            HeroExperience heroExperience,
            HeroEconomy heroEconomy,
            HeroUltimate heroUltimate,
            GameManager gameManager)
        {
            this.waveManager = waveManager;
            this.heroHealth = heroHealth;
            this.heroExperience = heroExperience;
            this.heroEconomy = heroEconomy;
            this.heroUltimate = heroUltimate;
            this.gameManager = gameManager;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameObject menuPanel;
        private TextMeshProUGUI infoText;
        private TextMeshProUGUI godModeLabel;
        private TextMeshProUGUI speedLabel;

        private float timeSpeedScale = 1f;
        private float nextInfoRefreshTime;

        private void Start()
        {
            BuildOverlayUI();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleMenu();
            }

            if (menuPanel != null && menuPanel.activeSelf && Time.unscaledTime >= nextInfoRefreshTime)
            {
                nextInfoRefreshTime = Time.unscaledTime + 0.2f;
                RefreshInfoHeader();
            }
        }

        private void OnDestroy()
        {
            if (Mathf.Abs(timeSpeedScale - 1f) > 0.01f && gameManager != null && gameManager.CurrentState == GameState.Playing)
            {
                Time.timeScale = 1f;
            }
        }

        private void ToggleMenu()
        {
            if (menuPanel == null) return;
            bool nextState = !menuPanel.activeSelf;
            menuPanel.SetActive(nextState);
            if (nextState)
            {
                RefreshInfoHeader();
            }
        }

        private void RefreshInfoHeader()
        {
            if (infoText == null) return;
            int wave = waveManager != null ? waveManager.CurrentWave : 0;
            int enemies = waveManager != null ? waveManager.EnemiesAlive : 0;
            int lvl = heroExperience != null ? heroExperience.CurrentLevel : 1;
            int hp = heroHealth != null ? heroHealth.CurrentHp : 0;
            int maxHp = heroHealth != null ? heroHealth.MaxHp : 0;
            infoText.text = $"W:{wave} | Mobs:{enemies} | Lv:{lvl} | HP:{hp}/{maxHp}";
        }

        private void BuildOverlayUI()
        {
            // 1. Compact "DEV" toggle button in bottom-left corner
            var toggleGo = CreateButton(transform, "CheatToggleBtn", "DEV", new Color(0.16f, 0.18f, 0.24f, 0.85f), 26, ToggleMenu);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0f, 0f);
            toggleRt.anchorMax = new Vector2(0f, 0f);
            toggleRt.pivot = new Vector2(0f, 0f);
            toggleRt.anchoredPosition = new Vector2(28f, 140f);
            toggleRt.sizeDelta = new Vector2(124f, 68f);

            // 2. Cheat menu panel (centered upper-mid)
            menuPanel = new GameObject("CheatMenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            menuPanel.transform.SetParent(transform, false);
            var panelRt = menuPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(0f, 80f);
            panelRt.sizeDelta = new Vector2(760f, 760f);

            var panelImg = menuPanel.GetComponent<Image>();
            panelImg.color = new Color(0.05f, 0.06f, 0.09f, 0.93f);

            var vlg = menuPanel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header row (Title + Close X)
            var headerRow = CreateRow(menuPanel.transform, 58f, 12f);
            var titleTmp = CreateLabel(headerRow.transform, "DEV CHEAT MENU (F1)", 32, Color.yellow, TextAlignmentOptions.MidlineLeft);
            titleTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var closeBtn = CreateButton(headerRow.transform, "CloseBtn", "X", new Color(0.75f, 0.18f, 0.18f, 1f), 28, ToggleMenu);
            var closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 70f;
            closeLe.minWidth = 70f;

            // Live status line
            infoText = CreateLabel(menuPanel.transform, "W:1 | Mobs:0 | Lv:1 | HP:200/200", 24, new Color(0.6f, 0.9f, 1f, 1f), TextAlignmentOptions.Center);
            infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

            // Section: Waves & Bosses
            var waveRow1 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(waveRow1.transform, "+1 Wave", new Color(0.22f, 0.42f, 0.68f, 1f), () =>
            {
                if (waveManager != null) waveManager.JumpToWave(waveManager.CurrentWave + 1);
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow1.transform, "Wave 5 (Mini-Boss)", new Color(0.82f, 0.45f, 0.14f, 1f), () =>
            {
                waveManager?.JumpToWave(5);
                RefreshInfoHeader();
            });

            var waveRow2 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(waveRow2.transform, "Wave 25 (Golem)", new Color(0.72f, 0.18f, 0.22f, 1f), () =>
            {
                waveManager?.JumpToWave(25);
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow2.transform, "Wave 50 (Necro)", new Color(0.5f, 0.2f, 0.72f, 1f), () =>
            {
                waveManager?.JumpToWave(50);
                RefreshInfoHeader();
            });

            var waveRow3 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(waveRow3.transform, "Kill All Enemies", new Color(0.65f, 0.22f, 0.22f, 1f), () =>
            {
                waveManager?.KillAllActiveEnemies();
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow3.transform, "+1 Level Up", new Color(0.2f, 0.62f, 0.34f, 1f), () =>
            {
                menuPanel.SetActive(false);
                heroExperience?.ForceLevelUp();
            });

            // Section: Hero & Combat
            var heroRow1 = CreateRow(menuPanel.transform, 72f, 14f);
            var godBtn = AddFlexButton(heroRow1.transform, "God Mode: OFF", new Color(0.26f, 0.28f, 0.36f, 1f), () =>
            {
                if (heroHealth == null) return;
                bool next = !heroHealth.IsGodMode;
                heroHealth.SetGodMode(next);
                if (godModeLabel != null) godModeLabel.text = next ? "God Mode: ON" : "God Mode: OFF";
            });
            godModeLabel = godBtn.GetComponentInChildren<TextMeshProUGUI>();

            AddFlexButton(heroRow1.transform, "Heal Full HP", new Color(0.18f, 0.58f, 0.45f, 1f), () =>
            {
                if (heroHealth != null) heroHealth.Heal(heroHealth.MaxHp);
                RefreshInfoHeader();
            });

            var heroRow2 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(heroRow2.transform, "Reset Ult CD", new Color(0.18f, 0.56f, 0.82f, 1f), () =>
            {
                heroUltimate?.ResetCooldown();
            });
            AddFlexButton(heroRow2.transform, "+1000 Coins", new Color(0.75f, 0.6f, 0.14f, 1f), () =>
            {
                heroEconomy?.AddCoin(1000);
            });

            var utilRow = CreateRow(menuPanel.transform, 72f, 14f);
            var speedBtn = AddFlexButton(utilRow.transform, "Speed: 1x", new Color(0.26f, 0.28f, 0.36f, 1f), () =>
            {
                timeSpeedScale = timeSpeedScale < 1.5f ? 2f : (timeSpeedScale < 2.5f ? 3f : 1f);
                if (gameManager == null || gameManager.CurrentState == GameState.Playing)
                {
                    Time.timeScale = timeSpeedScale;
                }
                if (speedLabel != null) speedLabel.text = $"Speed: {timeSpeedScale:0}x";
            });
            speedLabel = speedBtn.GetComponentInChildren<TextMeshProUGUI>();

            AddFlexButton(utilRow.transform, "Kill Hero (GameOver)", new Color(0.55f, 0.12f, 0.12f, 1f), () =>
            {
                menuPanel.SetActive(false);
                heroHealth?.SetGodMode(false);
                heroHealth?.SetInvulnerable(false);
                heroHealth?.DebugKill();
            });

            menuPanel.SetActive(false);
        }

        private static GameObject CreateRow(Transform parent, float height, float spacing)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return row;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button AddFlexButton(Transform parent, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var btn = CreateButton(parent, label, label, bgColor, 24, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            return btn;
        }

        private static Button CreateButton(Transform parent, string goName, string label, Color bgColor, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bgColor;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(8f, 4f);
            txtRt.offsetMax = new Vector2(-8f, -4f);
            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 16;
            tmp.fontSizeMax = fontSize;
            tmp.raycastTarget = false;

            return btn;
        }
#endif
    }
}
