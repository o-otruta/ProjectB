using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.UI
{
    /// <summary>
    /// In-game developer cheat menu for fast testing (waves, bosses, god mode, levelups, coins, speed, manual ability grant).
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
        private SecretItemSpawner secretItemSpawner;
        private SaveManager saveManager;
        private HeroAbilities heroAbilities;
        private UpgradeManager upgradeManager;
        private UpgradeApplier upgradeApplier;
        private ArenaGenerator arenaGenerator;
        private VacuumMagnetSpawner vacuumMagnetSpawner;
        private WaveEventManager waveEventManager;
        private CardCatalog cardCatalog;

        [Inject]
        public void Construct(
            WaveManager waveManager,
            HeroHealth heroHealth,
            HeroExperience heroExperience,
            HeroEconomy heroEconomy,
            HeroUltimate heroUltimate,
            GameManager gameManager,
            SecretItemSpawner secretItemSpawner = null,
            SaveManager saveManager = null,
            HeroAbilities heroAbilities = null,
            UpgradeManager upgradeManager = null,
            UpgradeApplier upgradeApplier = null,
            ArenaGenerator arenaGenerator = null,
            VacuumMagnetSpawner vacuumMagnetSpawner = null,
            WaveEventManager waveEventManager = null,
            CardCatalog cardCatalog = null)
        {
            this.waveManager = waveManager;
            this.heroHealth = heroHealth;
            this.heroExperience = heroExperience;
            this.heroEconomy = heroEconomy;
            this.heroUltimate = heroUltimate;
            this.gameManager = gameManager;
            this.secretItemSpawner = secretItemSpawner;
            this.saveManager = saveManager;
            this.heroAbilities = heroAbilities;
            this.upgradeManager = upgradeManager;
            this.upgradeApplier = upgradeApplier;
            this.arenaGenerator = arenaGenerator;
            this.vacuumMagnetSpawner = vacuumMagnetSpawner;
            this.waveEventManager = waveEventManager;
            this.cardCatalog = cardCatalog;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameObject menuPanel;
        private GameObject abilityPickerPanel;
        private TextMeshProUGUI infoText;
        private TextMeshProUGUI godModeLabel;
        private TextMeshProUGUI speedLabel;
        private TextMeshProUGUI pickerHeaderText;
        private Button activesTabBtn;
        private Button passivesTabBtn;
        private TextMeshProUGUI activesTabLabel;
        private TextMeshProUGUI passivesTabLabel;
        private ScrollRect pickerScrollRect;
        private Transform pickerContentTransform;

        private readonly List<Button> pickerButtons = new List<Button>();
        private readonly List<AbilityCardData> cachedCards = new List<AbilityCardData>();
        private bool showPassiveTab;
        private string pickerStatusNotice;

        // Enemy spawn picker + test mode
        private GameObject enemyPickerPanel;
        private Transform enemyPickerContent;
        private TextMeshProUGUI enemyPickerHeader;
        private TextMeshProUGUI testModeLabel;
        private TextMeshProUGUI spawnCountLabel;
        private TextMeshProUGUI spawnEliteLabel;
        private readonly List<EnemyData> cachedEnemyTypes = new List<EnemyData>();
        private readonly List<string> cachedEnemyLabels = new List<string>();
        private int spawnCount = 1;
        private bool spawnElite;
        private string enemyPickerNotice;

        private float timeSpeedScale = 1f;
        private float nextInfoRefreshTime;
        private TextMeshProUGUI telemetryLabel;

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

            if (Time.unscaledTime >= nextInfoRefreshTime)
            {
                if (menuPanel != null && menuPanel.activeSelf)
                {
                    nextInfoRefreshTime = Time.unscaledTime + 0.2f;
                    RefreshInfoHeader();
                }
                else if (enemyPickerPanel != null && enemyPickerPanel.activeSelf)
                {
                    nextInfoRefreshTime = Time.unscaledTime + 0.2f;
                    RefreshEnemyPickerHeader();
                }
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
            if (abilityPickerPanel != null && abilityPickerPanel.activeSelf)
            {
                abilityPickerPanel.SetActive(false);
                return;
            }
            if (enemyPickerPanel != null && enemyPickerPanel.activeSelf)
            {
                enemyPickerPanel.SetActive(false);
                return;
            }

            if (menuPanel == null) return;
            bool nextState = !menuPanel.activeSelf;
            menuPanel.SetActive(nextState);
            if (nextState)
            {
                RefreshInfoHeader();
            }
        }

        private void OpenAbilityPicker()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (abilityPickerPanel != null)
            {
                pickerStatusNotice = null;
                abilityPickerPanel.SetActive(true);
                RefreshAbilityPicker(true);
            }
        }

        private void BackToCheatMenu()
        {
            if (abilityPickerPanel != null) abilityPickerPanel.SetActive(false);
            if (enemyPickerPanel != null) enemyPickerPanel.SetActive(false);
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
                RefreshInfoHeader();
            }
        }

        public bool GrantOrUpgradeAbility(AbilityCardData abilityCard)
        {
            if (abilityCard == null || abilityCard.abilityData == null || heroAbilities == null) return false;

            AbilityData data = abilityCard.abilityData;
            if (!heroAbilities.HasAbility(data.id))
            {
                bool canAdd = data.type == AbilityType.Active
                    ? heroAbilities.CanAddActive()
                    : heroAbilities.CanAddPassive();

                if (!canAdd)
                {
                    pickerStatusNotice = data.type == AbilityType.Active
                        ? "Active slots full — tap +1 Slot"
                        : "Passive slots full — tap +1 Slot";
                    return false;
                }

                if (upgradeApplier != null)
                {
                    upgradeApplier.ApplyCard(abilityCard, upgradeManager);
                }
                else
                {
                    bool added = heroAbilities.AddAbility(data);
                    if (added && abilityCard.unlockedModifiers != null && abilityCard.unlockedModifiers.Count > 0 && upgradeManager != null)
                    {
                        upgradeManager.AddCardsToPool(abilityCard.unlockedModifiers);
                    }
                }

                pickerStatusNotice = null;
                return heroAbilities.HasAbility(data.id);
            }

            int level = Mathf.Max(1, heroAbilities.GetAbilityLevel(data.id));
            AbilityModifierCardData chosenMod = PickModifierForUpgrade(abilityCard, level);
            if (chosenMod != null)
            {
                if (upgradeApplier != null)
                {
                    upgradeApplier.ApplyCard(chosenMod, upgradeManager);
                }
                else
                {
                    heroAbilities.UpgradeAbility(
                        chosenMod.targetAbilityId,
                        chosenMod.modifierType,
                        chosenMod.value,
                        chosenMod.hasSecondaryModifier,
                        chosenMod.secondaryModifierType,
                        chosenMod.secondaryValue);
                }
            }
            else
            {
                heroAbilities.UpgradeAbility(data.id, ModifierType.Damage, 5f);
            }

            pickerStatusNotice = null;
            return true;
        }

        private static AbilityModifierCardData PickModifierForUpgrade(AbilityCardData abilityCard, int currentLevel)
        {
            if (abilityCard.unlockedModifiers == null || abilityCard.unlockedModifiers.Count == 0) return null;

            int validCount = 0;
            for (int i = 0; i < abilityCard.unlockedModifiers.Count; i++)
            {
                if (abilityCard.unlockedModifiers[i] is AbilityModifierCardData) validCount++;
            }
            if (validCount == 0) return null;

            int targetIdx = (currentLevel - 1) % validCount;
            int seen = 0;
            for (int i = 0; i < abilityCard.unlockedModifiers.Count; i++)
            {
                if (abilityCard.unlockedModifiers[i] is AbilityModifierCardData mod)
                {
                    if (seen == targetIdx) return mod;
                    seen++;
                }
            }
            return null;
        }

        private void RefreshInfoHeader()
        {
            if (infoText == null) return;
            int wave = waveManager != null ? waveManager.CurrentWave : 0;
            int enemies = waveManager != null ? waveManager.EnemiesAlive : 0;
            int lvl = heroExperience != null ? heroExperience.CurrentLevel : 1;
            int hp = heroHealth != null ? heroHealth.CurrentHp : 0;
            int maxHp = heroHealth != null ? heroHealth.MaxHp : 0;
            bool frozen = waveManager != null && waveManager.IsWaveFrozen;
            infoText.text = $"W:{wave}{(frozen ? " FROZEN" : "")} | Mobs:{enemies} | Lv:{lvl} | HP:{hp}/{maxHp}";
            if (testModeLabel != null) testModeLabel.text = frozen ? "Test Mode: ON" : "Test Mode: OFF";
        }

        /// <summary>Test mode: freezes the wave flow and clears the map, so enemies can be spawned by hand.</summary>
        public void SetTestMode(bool enabled)
        {
            if (waveManager == null) return;
            waveManager.SetWavesFrozen(enabled);
            if (enabled)
            {
                ClearAllMobs();
            }
            RefreshInfoHeader();
        }

        /// <summary>Removes every enemy without rewards and hides a leftover boss HP bar.</summary>
        public void ClearAllMobs()
        {
            waveManager?.DespawnAllActiveEnemies();
            HideBossBar();
        }

        private static void HideBossBar()
        {
            var bossBar = FindAnyObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
            if (bossBar != null) bossBar.Hide();
        }

        private void BuildOverlayUI()
        {
            // 1. Compact "DEV" toggle button in bottom-left corner, above the ability slot rows
            var toggleGo = CreateButton(transform, "CheatToggleBtn", "DEV", new Color(0.16f, 0.18f, 0.24f, 0.85f), 26, ToggleMenu);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0f, 0f);
            toggleRt.anchorMax = new Vector2(0f, 0f);
            toggleRt.pivot = new Vector2(0f, 0f);
            toggleRt.anchoredPosition = new Vector2(28f, 200f);
            toggleRt.sizeDelta = new Vector2(124f, 68f);

            // 2. Cheat menu panel (centered upper-mid)
            menuPanel = new GameObject("CheatMenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            menuPanel.transform.SetParent(transform, false);
            var panelRt = menuPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(0f, 50f);
            panelRt.sizeDelta = new Vector2(760f, 1185f);

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
            var testModeBtn = AddFlexButton(waveRow1.transform, "Test Mode: OFF", new Color(0.12f, 0.5f, 0.46f, 1f), () =>
            {
                SetTestMode(waveManager != null && !waveManager.IsWaveFrozen);
            });
            testModeLabel = testModeBtn.GetComponentInChildren<TextMeshProUGUI>();

            var waveRow2 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(waveRow2.transform, "W25 Golem", new Color(0.72f, 0.18f, 0.22f, 1f), () =>
            {
                waveManager?.JumpToWave(25);
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow2.transform, "W50 Necro", new Color(0.5f, 0.2f, 0.72f, 1f), () =>
            {
                waveManager?.JumpToWave(50);
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow2.transform, "W75 Lich", new Color(0.16f, 0.54f, 0.78f, 1f), () =>
            {
                waveManager?.JumpToWave(75);
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow2.transform, "W100 Demon", new Color(0.84f, 0.14f, 0.14f, 1f), () =>
            {
                waveManager?.JumpToWave(100);
                RefreshInfoHeader();
            });

            var waveRow3 = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(waveRow3.transform, "Kill All Enemies", new Color(0.65f, 0.22f, 0.22f, 1f), () =>
            {
                waveManager?.KillAllActiveEnemies();
                RefreshInfoHeader();
            });
            AddFlexButton(waveRow3.transform, "Clear Mobs (no loot)", new Color(0.36f, 0.26f, 0.3f, 1f), () =>
            {
                ClearAllMobs();
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

            var telemetryBtn = AddFlexButton(
                utilRow.transform,
                ProjectB.Core.Telemetry.RunTelemetrySession.IsEnabled ? "Telemetry: ON" : "Telemetry: OFF",
                ProjectB.Core.Telemetry.RunTelemetrySession.IsEnabled ? new Color(0.16f, 0.56f, 0.36f, 1f) : new Color(0.26f, 0.28f, 0.36f, 1f),
                () =>
                {
                    if (ProjectB.Core.Telemetry.RunTelemetrySession.IsEnabled)
                    {
                        ProjectB.Core.Telemetry.RunTelemetrySession.Disable();
                    }
                    else
                    {
                        ProjectB.Core.Telemetry.RunTelemetrySession.EnableManualSession();
                        DevLog.Log($"[RunTelemetry] Enabled -> {ProjectB.Core.Telemetry.RunTelemetrySession.SessionDirectory}");
                    }
                    if (telemetryLabel != null)
                    {
                        telemetryLabel.text = ProjectB.Core.Telemetry.RunTelemetrySession.IsEnabled ? "Telemetry: ON" : "Telemetry: OFF";
                    }
                });
            telemetryLabel = telemetryBtn.GetComponentInChildren<TextMeshProUGUI>();

            AddFlexButton(utilRow.transform, "Kill Hero (GameOver)", new Color(0.55f, 0.12f, 0.12f, 1f), () =>
            {
                menuPanel.SetActive(false);
                heroHealth?.SetGodMode(false);
                heroHealth?.SetInvulnerable(false);
                heroHealth?.DebugKill();
            });

            var secretRow = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(secretRow.transform, "Spawn All 3 Secrets", new Color(0.45f, 0.25f, 0.72f, 1f), () =>
            {
                secretItemSpawner?.SpawnAllSecretsNearHero(5.0f);
                menuPanel.SetActive(false);
            });
            AddFlexButton(secretRow.transform, "+1 Act/Pas Slot", new Color(0.24f, 0.56f, 0.62f, 1f), () =>
            {
                saveManager?.AddBonusActiveSlots(1, false);
                saveManager?.AddBonusPassiveSlots(1, false);
            });

            var abilityRow = CreateRow(menuPanel.transform, 72f, 14f);
            AddFlexButton(abilityRow.transform, "Grant Abilities...", new Color(0.76f, 0.42f, 0.14f, 1f), OpenAbilityPicker);
            AddFlexButton(abilityRow.transform, "Spawn Mobs...", new Color(0.62f, 0.2f, 0.32f, 1f), OpenEnemyPicker);

            var arenaRow = CreateRow(menuPanel.transform, 72f, 12f);
            AddFlexButton(arenaRow.transform, "Spawn Barrel", new Color(0.78f, 0.24f, 0.18f, 1f), () =>
            {
                arenaGenerator?.SpawnBarrelNearHero(4.5f);
                menuPanel.SetActive(false);
            });
            AddFlexButton(arenaRow.transform, "Spawn Magnet", new Color(0.18f, 0.64f, 0.82f, 1f), () =>
            {
                vacuumMagnetSpawner?.SpawnNearHero(3.5f);
                menuPanel.SetActive(false);
            });
            AddFlexButton(arenaRow.transform, "Spawn Shrine", new Color(0.56f, 0.22f, 0.78f, 1f), () =>
            {
                arenaGenerator?.SpawnShrineNearHero(5.5f);
                menuPanel.SetActive(false);
            });

            var eventRow = CreateRow(menuPanel.transform, 72f, 10f);
            AddFlexButton(eventRow.transform, "Swarm", new Color(0.84f, 0.44f, 0.14f, 1f), () =>
            {
                waveEventManager?.StartEvent(ProjectB.Data.Enemies.WaveEventType.SwarmRush);
                menuPanel.SetActive(false);
            });
            AddFlexButton(eventRow.transform, "GoldGob", new Color(0.82f, 0.64f, 0.14f, 1f), () =>
            {
                waveEventManager?.StartEvent(ProjectB.Data.Enemies.WaveEventType.GoldGoblin);
                menuPanel.SetActive(false);
            });
            AddFlexButton(eventRow.transform, "BloodMn", new Color(0.76f, 0.16f, 0.22f, 1f), () =>
            {
                waveEventManager?.StartEvent(ProjectB.Data.Enemies.WaveEventType.BloodMoon);
                menuPanel.SetActive(false);
            });
            AddFlexButton(eventRow.transform, "Ambush", new Color(0.58f, 0.24f, 0.78f, 1f), () =>
            {
                waveEventManager?.StartEvent(ProjectB.Data.Enemies.WaveEventType.EliteAmbush);
                menuPanel.SetActive(false);
            });

            var mutatorRow = CreateRow(menuPanel.transform, 72f, 10f);
            AddFlexButton(mutatorRow.transform, "Fog", new Color(0.22f, 0.56f, 0.78f, 1f), () =>
            {
                waveEventManager?.StartMutator(WaveMutatorType.Fog);
                menuPanel.SetActive(false);
            });
            AddFlexButton(mutatorRow.transform, "EliteSrg", new Color(0.64f, 0.28f, 0.82f, 1f), () =>
            {
                waveEventManager?.StartMutator(WaveMutatorType.EliteSurge);
                menuPanel.SetActive(false);
            });
            AddFlexButton(mutatorRow.transform, "GlassHrd", new Color(0.84f, 0.48f, 0.16f, 1f), () =>
            {
                waveEventManager?.StartMutator(WaveMutatorType.GlassHorde);
                menuPanel.SetActive(false);
            });
            AddFlexButton(mutatorRow.transform, "End Mut", new Color(0.32f, 0.36f, 0.44f, 1f), () =>
            {
                waveEventManager?.EndCurrentMutator(false);
                menuPanel.SetActive(false);
            });

            menuPanel.SetActive(false);

            BuildAbilityPickerPanel();
            BuildEnemyPickerPanel();
        }

        private void OpenEnemyPicker()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (enemyPickerPanel == null) return;
            enemyPickerNotice = null;
            enemyPickerPanel.SetActive(true);
            RebuildEnemyPickerButtons();
            RefreshEnemyPickerHeader();
        }

        private void BuildEnemyPickerPanel()
        {
            enemyPickerPanel = new GameObject("EnemyPickerPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            enemyPickerPanel.transform.SetParent(transform, false);
            var panelRt = enemyPickerPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(0f, 30f);
            panelRt.sizeDelta = new Vector2(880f, 1040f);
            enemyPickerPanel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.95f);

            var vlg = enemyPickerPanel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 18, 18);
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header: status + Back + Close
            var headerRow = CreateRow(enemyPickerPanel.transform, 60f, 10f);
            var headerLe = headerRow.AddComponent<LayoutElement>();
            headerLe.minHeight = 60f;
            headerLe.preferredHeight = 60f;
            enemyPickerHeader = CreateLabel(headerRow.transform, "SPAWN MOBS", 24, Color.yellow, TextAlignmentOptions.MidlineLeft);
            enemyPickerHeader.enableAutoSizing = true;
            enemyPickerHeader.fontSizeMin = 14;
            enemyPickerHeader.fontSizeMax = 24;
            enemyPickerHeader.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var backBtn = CreateButton(headerRow.transform, "BackBtn", "< Back", new Color(0.28f, 0.34f, 0.44f, 1f), 24, BackToCheatMenu);
            var backLe = backBtn.gameObject.AddComponent<LayoutElement>();
            backLe.preferredWidth = 130f;
            backLe.minWidth = 130f;
            var closeBtn = CreateButton(headerRow.transform, "CloseBtn", "X", new Color(0.75f, 0.18f, 0.18f, 1f), 26, ToggleMenu);
            var closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 70f;
            closeLe.minWidth = 70f;

            // Options: test mode, clear, count, elite, wave scaling
            var optRow1 = CreateRow(enemyPickerPanel.transform, 64f, 10f);
            var opt1Le = optRow1.AddComponent<LayoutElement>();
            opt1Le.minHeight = 64f;
            opt1Le.preferredHeight = 64f;
            AddFlexButton(optRow1.transform, "Test Mode", new Color(0.12f, 0.5f, 0.46f, 1f), () =>
            {
                SetTestMode(waveManager != null && !waveManager.IsWaveFrozen);
                RefreshEnemyPickerHeader();
            });
            AddFlexButton(optRow1.transform, "Clear Mobs", new Color(0.36f, 0.26f, 0.3f, 1f), () =>
            {
                ClearAllMobs();
                enemyPickerNotice = null;
                RefreshEnemyPickerHeader();
            });
            var countBtn = AddFlexButton(optRow1.transform, "Count: x1", new Color(0.26f, 0.28f, 0.36f, 1f), () =>
            {
                spawnCount = spawnCount == 1 ? 3 : (spawnCount == 3 ? 5 : (spawnCount == 5 ? 10 : 1));
                if (spawnCountLabel != null) spawnCountLabel.text = $"Count: x{spawnCount}";
            });
            spawnCountLabel = countBtn.GetComponentInChildren<TextMeshProUGUI>();
            var eliteBtn = AddFlexButton(optRow1.transform, "Elite: OFF", new Color(0.26f, 0.28f, 0.36f, 1f), () =>
            {
                spawnElite = !spawnElite;
                if (spawnEliteLabel != null) spawnEliteLabel.text = spawnElite ? "Elite: ON" : "Elite: OFF";
            });
            spawnEliteLabel = eliteBtn.GetComponentInChildren<TextMeshProUGUI>();

            var optRow2 = CreateRow(enemyPickerPanel.transform, 64f, 10f);
            var opt2Le = optRow2.AddComponent<LayoutElement>();
            opt2Le.minHeight = 64f;
            opt2Le.preferredHeight = 64f;
            AddWaveScaleButton(optRow2.transform, "W-5", -5);
            AddWaveScaleButton(optRow2.transform, "W-1", -1);
            AddWaveScaleButton(optRow2.transform, "W+1", 1);
            AddWaveScaleButton(optRow2.transform, "W+5", 5);
            AddFlexButton(optRow2.transform, "W25", new Color(0.72f, 0.18f, 0.22f, 1f), () => SetScalingWave(25));

            // Scrollable enemy grid
            var scrollGo = new GameObject("EnemyScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(enemyPickerPanel.transform, false);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.7f);
            var scrollLe = scrollGo.GetComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 640f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(398f, 76f);
            grid.spacing = new Vector2(14f, 10f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollGo.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            enemyPickerContent = contentGo.transform;
            enemyPickerPanel.SetActive(false);
        }

        private void AddWaveScaleButton(Transform parent, string label, int delta)
        {
            AddFlexButton(parent, label, new Color(0.22f, 0.36f, 0.58f, 1f), () =>
            {
                int current = waveManager != null ? waveManager.CurrentWave : 1;
                SetScalingWave(current + delta);
            });
        }

        /// <summary>Changes the current wave (HP/damage scaling for spawned mobs). Clears the map, like any wave jump.</summary>
        private void SetScalingWave(int wave)
        {
            if (waveManager == null) return;
            waveManager.JumpToWave(Mathf.Max(1, wave));
            if (waveManager.IsWaveFrozen) HideBossBar();
            enemyPickerNotice = waveManager.IsWaveFrozen ? null : "Waves are running: enable Test Mode to stop spawns";
            RefreshEnemyPickerHeader();
        }

        private void RefreshEnemyPickerHeader()
        {
            if (enemyPickerHeader == null) return;
            if (!string.IsNullOrEmpty(enemyPickerNotice))
            {
                enemyPickerHeader.text = enemyPickerNotice;
                enemyPickerHeader.color = new Color(1f, 0.6f, 0.35f, 1f);
                return;
            }

            int wave = waveManager != null ? waveManager.CurrentWave : 0;
            int mobs = waveManager != null ? waveManager.EnemiesAlive : 0;
            bool frozen = waveManager != null && waveManager.IsWaveFrozen;
            enemyPickerHeader.text = $"SPAWN  •  W{wave} scaling  •  Mobs {mobs}  •  {(frozen ? "TEST MODE" : "waves running")}";
            enemyPickerHeader.color = frozen ? new Color(0.45f, 1f, 0.75f, 1f) : Color.yellow;
        }

        private void RebuildEnemyPickerButtons()
        {
            if (enemyPickerContent == null) return;
            for (int i = enemyPickerContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = enemyPickerContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            CollectEnemyTypes();
            for (int i = 0; i < cachedEnemyTypes.Count; i++)
            {
                EnemyData data = cachedEnemyTypes[i];
                bool isBoss = data is BossData;
                Color bg = isBoss ? new Color(0.58f, 0.16f, 0.2f, 1f) : new Color(0.22f, 0.26f, 0.36f, 1f);
                CreateButton(enemyPickerContent, $"Spawn_{data.name}", cachedEnemyLabels[i], bg, 22, () => SpawnEnemyFromPicker(data));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyPickerContent as RectTransform);
        }

        private void SpawnEnemyFromPicker(EnemyData data)
        {
            if (waveManager == null || data == null) return;
            bool isBoss = data is BossData;
            int count = isBoss ? 1 : spawnCount;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (waveManager.DebugSpawnEnemyNearHero(data, 6f + i * 0.6f, spawnElite && !isBoss) != null)
                {
                    spawned++;
                }
            }

            enemyPickerNotice = spawned > 0 ? null : "Spawn failed (enemy limit reached?)";
            RefreshEnemyPickerHeader();
        }

        private void CollectEnemyTypes()
        {
            cachedEnemyTypes.Clear();
            cachedEnemyLabels.Clear();
            WaveConfig config = waveManager != null ? waveManager.Config : null;
            if (config == null) return;

            if (config.bosses != null)
            {
                for (int i = 0; i < config.bosses.Count; i++)
                {
                    BossData boss = config.bosses[i];
                    if (boss == null) continue;
                    int bossWave = config.bossInterval * (i + 1);
                    AddEnemyType(boss, $"BOSS {boss.bossDisplayName} · W{bossWave}");
                }
            }

            if (config.enemySchedule != null)
            {
                for (int i = 0; i < config.enemySchedule.Count; i++)
                {
                    EnemyWaveEntry entry = config.enemySchedule[i];
                    if (entry == null || entry.enemy == null) continue;
                    AddEnemyType(entry.enemy, $"{PrettyEnemyName(entry.enemy)} · W{entry.minWave}");
                }
            }

            // Summons and special spawns that are not in the regular schedule
            int scheduledCount = cachedEnemyTypes.Count;
            for (int i = 0; i < scheduledCount; i++)
            {
                EnemyData data = cachedEnemyTypes[i];
                if (data is SummonerEnemyData summoner) AddEnemyType(summoner.minionData, $"{PrettyEnemyName(summoner.minionData)} · summon");
                else if (data is SplitterEnemyData splitter) AddEnemyType(splitter.splitChildData, $"{PrettyEnemyName(splitter.splitChildData)} · split");
                else if (data is BossData boss) AddEnemyType(boss.minionData, $"{PrettyEnemyName(boss.minionData)} · boss summon");
            }
            AddEnemyType(config.goldGoblinData, "Gold Goblin · event");
            AddEnemyType(config.swarmRushEnemyData, $"{PrettyEnemyName(config.swarmRushEnemyData)} · swarm");
            AddEnemyType(config.corridorHordeEnemyData, $"{PrettyEnemyName(config.corridorHordeEnemyData)} · corridor");
        }

        private void AddEnemyType(EnemyData data, string label)
        {
            if (data == null || cachedEnemyTypes.Contains(data)) return;
            cachedEnemyTypes.Add(data);
            cachedEnemyLabels.Add(label);
        }

        private static string PrettyEnemyName(EnemyData data)
        {
            if (data == null) return string.Empty;
            string raw = data.name;
            if (raw.EndsWith("Data", StringComparison.Ordinal)) raw = raw.Substring(0, raw.Length - 4);
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
                sb.Append(c == '_' ? ' ' : c);
            }
            return sb.ToString();
        }

        private void BuildAbilityPickerPanel()
        {
            abilityPickerPanel = new GameObject("AbilityPickerPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            abilityPickerPanel.transform.SetParent(transform, false);
            var pickerRt = abilityPickerPanel.GetComponent<RectTransform>();
            pickerRt.anchorMin = new Vector2(0.5f, 0.5f);
            pickerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pickerRt.pivot = new Vector2(0.5f, 0.5f);
            pickerRt.anchoredPosition = new Vector2(0f, 30f);
            pickerRt.sizeDelta = new Vector2(880f, 1040f);

            var pickerImg = abilityPickerPanel.GetComponent<Image>();
            pickerImg.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);

            var vlg = abilityPickerPanel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 18, 18);
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header row
            var headerRow = CreateRow(abilityPickerPanel.transform, 60f, 10f);
            var headerLe = headerRow.AddComponent<LayoutElement>();
            headerLe.minHeight = 60f;
            headerLe.preferredHeight = 60f;
            headerLe.flexibleHeight = 0f;
            pickerHeaderText = CreateLabel(headerRow.transform, "ABILITIES", 28, Color.yellow, TextAlignmentOptions.MidlineLeft);
            pickerHeaderText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var backBtn = CreateButton(headerRow.transform, "BackBtn", "< Back", new Color(0.28f, 0.34f, 0.44f, 1f), 24, BackToCheatMenu);
            var backLe = backBtn.gameObject.AddComponent<LayoutElement>();
            backLe.preferredWidth = 130f;
            backLe.minWidth = 130f;

            var closeBtn = CreateButton(headerRow.transform, "CloseBtn", "X", new Color(0.75f, 0.18f, 0.18f, 1f), 26, ToggleMenu);
            var closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 70f;
            closeLe.minWidth = 70f;

            // Tab row
            var tabRow = CreateRow(abilityPickerPanel.transform, 64f, 12f);
            var tabLe = tabRow.AddComponent<LayoutElement>();
            tabLe.minHeight = 64f;
            tabLe.preferredHeight = 64f;
            tabLe.flexibleHeight = 0f;
            activesTabBtn = AddFlexButton(tabRow.transform, "ACTIVES", new Color(0.82f, 0.44f, 0.14f, 1f), () =>
            {
                showPassiveTab = false;
                pickerStatusNotice = null;
                RefreshAbilityPicker(true);
            });
            activesTabLabel = activesTabBtn.GetComponentInChildren<TextMeshProUGUI>();
            passivesTabBtn = AddFlexButton(tabRow.transform, "PASSIVES", new Color(0.22f, 0.28f, 0.36f, 1f), () =>
            {
                showPassiveTab = true;
                pickerStatusNotice = null;
                RefreshAbilityPicker(true);
            });
            passivesTabLabel = passivesTabBtn.GetComponentInChildren<TextMeshProUGUI>();
            var addSlotBtn = CreateButton(tabRow.transform, "AddSlotBtn", "+1 Slot", new Color(0.24f, 0.56f, 0.62f, 1f), 24, () =>
            {
                saveManager?.AddBonusActiveSlots(1, false);
                saveManager?.AddBonusPassiveSlots(1, false);
                pickerStatusNotice = null;
                RefreshAbilityPicker(false);
            });
            var slotLe = addSlotBtn.gameObject.AddComponent<LayoutElement>();
            slotLe.preferredWidth = 165f;
            slotLe.minWidth = 165f;

            // Scroll view
            var scrollGo = new GameObject("AbilityScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(abilityPickerPanel.transform, false);
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 856f);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.7f);
            var scrollLe = scrollGo.GetComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 780f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(398f, 84f);
            grid.spacing = new Vector2(14f, 12f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = scrollGo.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            pickerScrollRect = scroll;
            pickerContentTransform = contentGo.transform;
            abilityPickerPanel.SetActive(false);
        }

        private void RefreshAbilityPicker(bool resetScroll = false)
        {
            if (pickerContentTransform == null) return;

            for (int i = 0; i < pickerButtons.Count; i++)
            {
                if (pickerButtons[i] != null)
                {
                    GameObject oldGo = pickerButtons[i].gameObject;
                    oldGo.SetActive(false);
                    oldGo.transform.SetParent(null, false);
                    if (Application.isPlaying)
                    {
                        Destroy(oldGo);
                    }
                    else
                    {
                        DestroyImmediate(oldGo);
                    }
                }
            }
            pickerButtons.Clear();

            CollectAbilityCards();
            int activeCount = 0;
            int passiveCount = 0;
            for (int i = 0; i < cachedCards.Count; i++)
            {
                AbilityCardData card = cachedCards[i];
                if (card == null || card.abilityData == null) continue;
                if (card.abilityData.type == AbilityType.Active) activeCount++;
                else if (card.abilityData.type == AbilityType.Passive) passiveCount++;
            }

            if (activesTabLabel != null) activesTabLabel.text = $"ACTIVES ({activeCount})";
            if (passivesTabLabel != null) passivesTabLabel.text = $"PASSIVES ({passiveCount})";

            if (activesTabBtn != null)
            {
                var img = activesTabBtn.GetComponent<Image>();
                if (img != null) img.color = !showPassiveTab ? new Color(0.84f, 0.44f, 0.14f, 1f) : new Color(0.22f, 0.26f, 0.34f, 1f);
            }
            if (passivesTabBtn != null)
            {
                var img = passivesTabBtn.GetComponent<Image>();
                if (img != null) img.color = showPassiveTab ? new Color(0.16f, 0.62f, 0.74f, 1f) : new Color(0.22f, 0.26f, 0.34f, 1f);
            }

            if (pickerHeaderText != null)
            {
                if (!string.IsNullOrEmpty(pickerStatusNotice))
                {
                    pickerHeaderText.text = pickerStatusNotice;
                    pickerHeaderText.color = new Color(1f, 0.45f, 0.35f, 1f);
                }
                else
                {
                    int actCur = heroAbilities != null ? heroAbilities.ActiveAbilities.Count : 0;
                    int actMax = heroAbilities != null ? heroAbilities.UnlockedActiveSlots : 3;
                    int pasCur = heroAbilities != null ? heroAbilities.PassiveAbilities.Count : 0;
                    int pasMax = heroAbilities != null ? heroAbilities.UnlockedPassiveSlots : 3;
                    pickerHeaderText.text = $"ABILITIES  •  Act {actCur}/{actMax}  |  Pas {pasCur}/{pasMax}";
                    pickerHeaderText.color = Color.yellow;
                }
            }

            AbilityType targetType = showPassiveTab ? AbilityType.Passive : AbilityType.Active;

            for (int i = 0; i < cachedCards.Count; i++)
            {
                AbilityCardData card = cachedCards[i];
                if (card == null || card.abilityData == null || card.abilityData.type != targetType) continue;

                int level = heroAbilities != null ? heroAbilities.GetAbilityLevel(card.abilityData.id) : 0;
                bool owned = level > 0;
                string label = owned ? $"{card.abilityData.abilityName} [Lv {level}]" : card.abilityData.abilityName;
                Color bgColor = owned
                    ? new Color(0.18f, 0.56f, 0.30f, 1f)
                    : (targetType == AbilityType.Active ? new Color(0.22f, 0.26f, 0.36f, 1f) : new Color(0.18f, 0.28f, 0.34f, 1f));

                AbilityCardData captured = card;
                var btn = CreateButton(pickerContentTransform, $"Grant_{card.abilityData.id}", label, bgColor, 22, () =>
                {
                    GrantOrUpgradeAbility(captured);
                    RefreshAbilityPicker(false);
                });
                pickerButtons.Add(btn);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(pickerContentTransform as RectTransform);
            if (abilityPickerPanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(abilityPickerPanel.transform as RectTransform);
            }

            if (resetScroll && pickerScrollRect != null)
            {
                pickerScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void CollectAbilityCards()
        {
            cachedCards.Clear();
            IReadOnlyList<CardData> sourcePool = upgradeManager != null ? upgradeManager.ResolvedCardPool : null;
            if (sourcePool == null || sourcePool.Count == 0)
            {
                sourcePool = cardCatalog != null ? cardCatalog.cards : null;
            }
            if (sourcePool == null || sourcePool.Count == 0)
            {
                CardCatalog fallbackCatalog = Resources.Load<CardCatalog>(CardCatalog.DEFAULT_RESOURCE_PATH);
                sourcePool = fallbackCatalog != null ? fallbackCatalog.cards : null;
            }
            if (sourcePool == null) return;

            for (int i = 0; i < sourcePool.Count; i++)
            {
                if (sourcePool[i] is AbilityCardData abilityCard && abilityCard.abilityData != null)
                {
                    bool duplicate = false;
                    for (int j = 0; j < cachedCards.Count; j++)
                    {
                        if (cachedCards[j].abilityData.id == abilityCard.abilityData.id)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        cachedCards.Add(abilityCard);
                    }
                }
            }

            cachedCards.Sort((a, b) => string.Compare(a.abilityData.abilityName, b.abilityData.abilityName, StringComparison.OrdinalIgnoreCase));
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
            tmp.fontSizeMin = 15;
            tmp.fontSizeMax = fontSize;
            tmp.raycastTarget = false;

            return btn;
        }
#endif
    }
}
