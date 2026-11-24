using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Player;

namespace ProjectB.Visuals
{
    /// <summary>
    /// Centralized gameplay juice/feedback manager:
    /// - Pooled floating damage numbers (zero combat GC)
    /// - Single shared ParticleSystem using Emit(EmitParams, count) for 1-draw-call bursts
    /// - AudioManager SFX & gameplay music triggers
    /// - CameraController screen shake triggers
    /// </summary>
    public class VfxFeedbackManager : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material burstParticleMaterial;

        [Header("Floating Damage Numbers")]
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color bossDamageColor = new Color(1f, 0.84f, 0.25f, 1f);
        [SerializeField] private Color critDamageColor = new Color(1f, 0.56f, 0.08f, 1f);
        [SerializeField] private Color heroDamageColor = new Color(1f, 0.28f, 0.28f, 1f);

        private GameEventBus eventBus;
        private AudioManager audioManager;
        private CameraController cameraController;
        private HeroHealth heroHealth;
        private HeroUltimate heroUltimate;
        private GameManager gameManager;

        private Camera mainCamera;
        private IObjectPool<DamageNumberItem> damagePool;
        private Canvas damageNumbersCanvas;
        private ParticleSystem burstParticles;
        private bool isSubscribed;
        private readonly WaitForSecondsRealtime hitStopWait = new WaitForSecondsRealtime(0.03f);
        private Coroutine hitStopCoroutine;
        private bool isHitStopActive;

        public bool IsHitStopActive => isHitStopActive;

        public const int MAX_ACTIVE_DAMAGE_NUMBERS = 80;
        private readonly System.Collections.Generic.List<DamageNumberItem> activeDamageNumbers =
            new System.Collections.Generic.List<DamageNumberItem>(MAX_ACTIVE_DAMAGE_NUMBERS);
        private System.Action<DamageNumberItem> cachedDamageItemReleaseHandler;

        private static readonly string[] CachedShrineProgressStrings =
        {
            "SHRINE: 0/6",
            "SHRINE: 1/6",
            "SHRINE: 2/6",
            "SHRINE: 3/6",
            "SHRINE: 4/6",
            "SHRINE: 5/6",
            "SHRINE: 6/6"
        };
        private static readonly string[] CachedComboMilestoneLabels =
        {
            string.Empty,
            "COMBO x15! +10%",
            "COMBO x30! +15%",
            "COMBO x50! +20%",
            "COMBO x100! +30%"
        };
        private const string CachedShrineStartText = "CHALLENGE! Defeat 6 Elites!";
        private const string CachedShrineCompleteText = "SHRINE CLEARED! +100 Coins";

        public int ActiveDamageNumberCount => activeDamageNumbers.Count;

        private static readonly Color DeathColorNormal = new Color(0.95f, 0.35f, 0.28f, 1f);
        private static readonly Color DeathColorElite = new Color(0.75f, 0.22f, 0.95f, 1f);
        private static readonly Color DeathColorJuggernaut = new Color(0.96f, 0.72f, 0.18f, 1f);
        private static readonly Color DeathColorHasteAura = new Color(0.24f, 0.88f, 1f, 1f);
        private static readonly Color DeathColorBoss = new Color(1f, 0.72f, 0.15f, 1f);
        private static readonly Color ExploderPrimaryColor = new Color(1f, 0.24f, 0.16f, 1f);
        private static readonly Color ExploderSecondaryColor = new Color(1f, 0.76f, 0.2f, 1f);
        private static readonly Color FrostPrimaryColor = new Color(0.45f, 0.92f, 1f, 1f);
        private static readonly Color FrostSecondaryColor = new Color(0.88f, 0.98f, 1f, 1f);
        private static readonly Color XpSparkColor = new Color(0.32f, 1f, 0.45f, 1f);
        private static readonly Color CoinSparkColor = new Color(1f, 0.86f, 0.2f, 1f);
        private static readonly Color LevelUpColor = new Color(1f, 0.92f, 0.35f, 1f);
        private static readonly Color ShieldUltColor = new Color(0.28f, 0.88f, 1f, 1f);
        private static readonly Color MeteorUltColor = new Color(1f, 0.48f, 0.14f, 1f);
        private static readonly Color FireballPrimaryColor = new Color(1f, 0.45f, 0.12f, 1f);
        private static readonly Color FireballSecondaryColor = new Color(1f, 0.88f, 0.22f, 1f);
        private static readonly Color MeteorPrimaryColor = new Color(1f, 0.25f, 0.12f, 1f);
        private static readonly Color MeteorSecondaryColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SecretPickupColor = new Color(0.88f, 0.4f, 1f, 1f);
        private static readonly Color SecretRewardTextColor = new Color(1f, 0.9f, 0.32f, 1f);
        private static readonly Color SynergyGoldColor = new Color(1f, 0.84f, 0.2f, 1f);
        private static readonly Color SynergyPurpleColor = new Color(0.75f, 0.32f, 1f, 1f);
        private static readonly Color SeismicPrimaryColor = new Color(0.92f, 0.58f, 0.16f, 1f);
        private static readonly Color SeismicSecondaryColor = new Color(0.68f, 0.44f, 0.22f, 1f);
        private static readonly Color ArrowRainPrimaryColor = new Color(0.32f, 0.95f, 0.45f, 1f);
        private static readonly Color ArrowRainSecondaryColor = new Color(0.95f, 0.88f, 0.28f, 1f);
        private static readonly Color SoulNovaPrimaryColor = new Color(0.28f, 0.92f, 1f, 1f);
        private static readonly Color SoulNovaSecondaryColor = new Color(0.62f, 0.34f, 1f, 1f);
        private static readonly Color SoulHealTextColor = new Color(0.38f, 1f, 0.55f, 1f);
        private static readonly Color BarrelPrimaryColor = new Color(1f, 0.42f, 0.12f, 1f);
        private static readonly Color BarrelSecondaryColor = new Color(1f, 0.88f, 0.2f, 1f);
        private static readonly Color VacuumPrimaryColor = new Color(0.25f, 0.95f, 1f, 1f);
        private static readonly Color VacuumSecondaryColor = new Color(1f, 0.35f, 0.85f, 1f);
        private static readonly Color ShrineStartColor = new Color(1f, 0.24f, 0.36f, 1f);
        private static readonly Color ShrineCompleteColor = new Color(1f, 0.86f, 0.24f, 1f);
        private static readonly Color OverdriveGoldColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color AwakenedCyanColor = new Color(0.35f, 0.96f, 1f, 1f);
        private static readonly Color MythicMagentaColor = new Color(0.95f, 0.36f, 1f, 1f);
        private static readonly Color SteamWhiteColor = new Color(0.92f, 0.98f, 1f, 1f);
        private static readonly Color ComboTextColor = new Color(1f, 0.68f, 0.16f, 1f);
        private static readonly Color BlockedDamageColor = new Color(0.68f, 0.78f, 0.88f, 1f);
        private static readonly Color DemonEnrageColor = new Color(1f, 0.18f, 0.14f, 1f);
        private static readonly Color BossShardColor = new Color(0.78f, 0.55f, 1f, 1f);
        private static readonly Color HolyGoldColor = new Color(1f, 0.9f, 0.45f, 1f);
        private static readonly Color DecoyShadowColor = new Color(0.55f, 0.3f, 0.95f, 1f);
        private static readonly Color EngineerOrangeColor = new Color(1f, 0.62f, 0.18f, 1f);
        private static readonly string[] CachedShardLabels = BuildShardLabels();

        private static string[] BuildShardLabels()
        {
            var labels = new string[11];
            for (int i = 0; i < labels.Length; i++) labels[i] = i == 1 ? "+1 BOSS SHARD" : $"+{i} BOSS SHARDS";
            return labels;
        }

        [Inject]
        public void Construct(
            GameEventBus eventBus,
            AudioManager audioManager = null,
            CameraController cameraController = null,
            HeroHealth heroHealth = null,
            HeroUltimate heroUltimate = null,
            GameManager gameManager = null)
        {
            this.eventBus = eventBus;
            this.audioManager = audioManager;
            this.cameraController = cameraController;
            this.heroHealth = heroHealth;
            this.heroUltimate = heroUltimate;
            this.gameManager = gameManager;
        }

        private void Start()
        {
            EnsureInitialized();
            audioManager?.PlayMusic(MusicType.Gameplay);
        }

        public void EnsureInitialized()
        {
            if (mainCamera == null)
            {
                mainCamera = cameraController != null
                    ? cameraController.GetComponent<Camera>()
                    : Camera.main;
                if (mainCamera == null) mainCamera = Camera.main;
            }

            if (damagePool == null)
            {
                InitializeDamagePool();
            }

            if (burstParticles == null)
            {
                InitializeBurstParticles();
            }

            Subscribe();
        }

        private void LateUpdate()
        {
            if (damageNumbersCanvas == null) return;

            // Hide floating numbers while any modal UI (pause / level-up / game over) is open.
            bool shouldShow = gameManager == null || gameManager.CurrentState == GameState.Playing;
            if (damageNumbersCanvas.enabled != shouldShow)
            {
                damageNumbersCanvas.enabled = shouldShow;
            }
        }

        private void OnDisable()
        {
            CancelHitStopIfNeeded();
        }

        private void OnDestroy()
        {
            CancelHitStopIfNeeded();
            Unsubscribe();
            activeDamageNumbers.Clear();
        }

        public bool TryTriggerHitStop(float duration = 0.03f)
        {
            if (!Application.isPlaying || isHitStopActive || duration <= 0f || Time.timeScale <= 0.01f)
            {
                return false;
            }

            if (gameManager != null && gameManager.CurrentState != GameState.Playing)
            {
                return false;
            }

            hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
            return true;
        }

        private System.Collections.IEnumerator HitStopCoroutine(float duration)
        {
            isHitStopActive = true;
            Time.timeScale = 0f;
            hitStopWait.waitTime = duration;
            hitStopWait.Reset();
            yield return hitStopWait;
            isHitStopActive = false;
            hitStopCoroutine = null;

            if (gameManager == null || gameManager.CurrentState == GameState.Playing)
            {
                Time.timeScale = 1f;
            }
        }

        private void CancelHitStopIfNeeded()
        {
            if (!isHitStopActive) return;

            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                hitStopCoroutine = null;
            }
            isHitStopActive = false;
            if (gameManager == null || gameManager.CurrentState == GameState.Playing)
            {
                Time.timeScale = 1f;
            }
        }

        private void InitializeDamagePool()
        {
            cachedDamageItemReleaseHandler = HandleDamageItemReleased;

            var canvasGo = new GameObject("DamageNumbersCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the main UI canvas (order 0) so HUD, pause, level-up and game-over panels always cover the numbers.
            canvas.sortingOrder = -10;
            damageNumbersCanvas = canvas;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            // Sprint 14.1: same Portrait/Landscape reference + short-side match as the scene canvases.
            canvasGo.AddComponent<ProjectB.UI.AdaptiveCanvasScaler>();

            damagePool = new ObjectPool<DamageNumberItem>(
                createFunc: () =>
                {
                    var go = new GameObject("DmgNum", typeof(RectTransform));
                    go.transform.SetParent(canvasGo.transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.sizeDelta = new Vector2(520f, 90f);

                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.fontSize = 44f;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.raycastTarget = false;

                    // Sprint 13: shared "Number" preset (thick outline) from UiTheme — one material for every
                    // damage number instead of a per-text outline instance, so they batch.
                    ProjectB.UI.UiTheme theme = ProjectB.UI.UiTheme.Current;
                    if (theme.displayFont != null && theme.numberMaterial != null)
                    {
                        tmp.font = theme.displayFont;
                        tmp.fontSharedMaterial = theme.numberMaterial;
                    }
                    else
                    {
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.outlineWidth = 0.22f;
                        tmp.outlineColor = new Color32(10, 10, 14, 220);
                    }
                    tmp.text = "00000!"; // Pre-allocate internal TMP char buffer at pool creation time

                    var item = go.AddComponent<DamageNumberItem>();
                    item.SetTextMesh(tmp);
                    item.SetReleaseCallback(cachedDamageItemReleaseHandler);
                    return item;
                },
                actionOnGet: item => item.gameObject.SetActive(true),
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item =>
                {
                    if (item != null)
                    {
                        if (Application.isPlaying) Destroy(item.gameObject);
                        else DestroyImmediate(item.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: MAX_ACTIVE_DAMAGE_NUMBERS,
                maxSize: MAX_ACTIVE_DAMAGE_NUMBERS);

            // Pre-warm all pooled DamageNumberItems upfront so combat never instantiates TextMeshProUGUI
            var prewarmBuffer = new DamageNumberItem[MAX_ACTIVE_DAMAGE_NUMBERS];
            for (int i = 0; i < MAX_ACTIVE_DAMAGE_NUMBERS; i++)
            {
                prewarmBuffer[i] = damagePool.Get();
            }
            for (int i = 0; i < MAX_ACTIVE_DAMAGE_NUMBERS; i++)
            {
                damagePool.Release(prewarmBuffer[i]);
            }
        }

        private void HandleDamageItemReleased(DamageNumberItem item)
        {
            activeDamageNumbers.Remove(item);
        }

        private DamageNumberItem AcquireDamageNumberItem()
        {
            if (damagePool == null) return null;

            if (activeDamageNumbers.Count >= MAX_ACTIVE_DAMAGE_NUMBERS)
            {
                DamageNumberItem oldest = activeDamageNumbers[0];
                if (oldest != null)
                {
                    oldest.ReturnToPool();
                }
                else
                {
                    activeDamageNumbers.RemoveAt(0);
                }
            }

            DamageNumberItem item = damagePool.Get();
            if (item != null)
            {
                activeDamageNumbers.Add(item);
            }
            return item;
        }

        public void TickDamageNumbers(float deltaTime)
        {
            for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
            {
                if (i >= activeDamageNumbers.Count) continue;
                var item = activeDamageNumbers[i];
                if (item != null)
                {
                    item.Tick(deltaTime);
                }
            }
        }

        private void InitializeBurstParticles()
        {
            var psGo = new GameObject("SharedBurstParticles");
            psGo.transform.SetParent(transform, false);

            burstParticles = psGo.AddComponent<ParticleSystem>();
            var main = burstParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.gravityModifier = 0.65f;
            main.maxParticles = 400;

            var emission = burstParticles.emission;
            emission.enabled = false;

            var shape = burstParticles.shape;
            shape.enabled = false;

            var sizeOverLifetime = burstParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                // Use Unity's built-in cube mesh for crisp voxel-style debris bursts!
                GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                psRenderer.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
                if (burstParticleMaterial != null)
                {
                    psRenderer.sharedMaterial = burstParticleMaterial;
                }
                else
                {
                    psRenderer.sharedMaterial = tempCube.GetComponent<MeshRenderer>().sharedMaterial;
                }
                if (Application.isPlaying) Destroy(tempCube);
                else DestroyImmediate(tempCube);
                psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                psRenderer.receiveShadows = false;
                psRenderer.enableGPUInstancing = true;
            }
        }

        private void Subscribe()
        {
            if (isSubscribed) return;
            isSubscribed = true;

            if (eventBus != null)
            {
                eventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
                eventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
                eventBus.Subscribe<XpCollectedEvent>(OnXpCollected);
                eventBus.Subscribe<CoinCollectedEvent>(OnCoinCollected);
                eventBus.Subscribe<HeroLeveledUpEvent>(OnHeroLeveledUp);
                eventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
                eventBus.Subscribe<SecretItemCollectedEvent>(OnSecretItemCollected);
                eventBus.Subscribe<FireballExplodedEvent>(OnFireballExploded);
                eventBus.Subscribe<MeteorImpactEvent>(OnMeteorImpact);
                eventBus.Subscribe<SynergyActivatedEvent>(OnSynergyActivated);
                eventBus.Subscribe<SeismicSlamEvent>(OnSeismicSlam);
                eventBus.Subscribe<EnemyExplodedEvent>(OnEnemyExploded);
                eventBus.Subscribe<FrostLichNovaEvent>(OnFrostLichNova);
                eventBus.Subscribe<ArrowRainWaveEvent>(OnArrowRainWave);
                eventBus.Subscribe<SoulNovaCastEvent>(OnSoulNovaCast);
                eventBus.Subscribe<BarrelExplodedEvent>(OnBarrelExploded);
                eventBus.Subscribe<VacuumMagnetCollectedEvent>(OnVacuumMagnetCollected);
                eventBus.Subscribe<ChallengeShrineEvent>(OnChallengeShrine);
                eventBus.Subscribe<ShieldShockwaveEvent>(OnShieldShockwave);
                eventBus.Subscribe<AbilityOverdriveEvent>(OnAbilityOverdrive);
                eventBus.Subscribe<ComboMilestoneEvent>(OnComboMilestone);
                eventBus.Subscribe<SynergyReactionVfxEvent>(OnSynergyReactionVfx);
                eventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
                eventBus.Subscribe<AbilitySfxEvent>(OnAbilitySfx);
                eventBus.Subscribe<WaveEventStartedEvent>(OnWaveEventStarted);
                eventBus.Subscribe<WaveEventEndedEvent>(OnWaveEventEnded);
                eventBus.Subscribe<WaveMutatorStartedEvent>(OnWaveMutatorStarted);
                eventBus.Subscribe<WaveMutatorEndedEvent>(OnWaveMutatorEnded);
                eventBus.Subscribe<EnemySummonHealEvent>(OnEnemySummonHeal);
                eventBus.Subscribe<DemonLordEnragedEvent>(OnDemonLordEnraged);
                eventBus.Subscribe<AbilityVfxEvent>(OnAbilityVfx);
                eventBus.Subscribe<BossShardsGainedEvent>(OnBossShardsGained);
                eventBus.Subscribe<SecondWindTriggeredEvent>(OnSecondWind);
                eventBus.Subscribe<EnemySplitEvent>(OnEnemySplit);
                eventBus.Subscribe<FrostWispZoneSpawnedEvent>(OnFrostWispZoneSpawned);
                eventBus.Subscribe<SiegeShellBrokenEvent>(OnSiegeShellBroken);
            }

            if (heroHealth != null)
            {
                heroHealth.OnDamageTaken += OnHeroDamaged;
            }

            if (heroUltimate != null)
            {
                heroUltimate.OnStateChanged += OnUltimateStateChanged;
            }
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;
            isSubscribed = false;

            if (eventBus != null)
            {
                eventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
                eventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
                eventBus.Unsubscribe<XpCollectedEvent>(OnXpCollected);
                eventBus.Unsubscribe<CoinCollectedEvent>(OnCoinCollected);
                eventBus.Unsubscribe<HeroLeveledUpEvent>(OnHeroLeveledUp);
                eventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
                eventBus.Unsubscribe<SecretItemCollectedEvent>(OnSecretItemCollected);
                eventBus.Unsubscribe<FireballExplodedEvent>(OnFireballExploded);
                eventBus.Unsubscribe<MeteorImpactEvent>(OnMeteorImpact);
                eventBus.Unsubscribe<SynergyActivatedEvent>(OnSynergyActivated);
                eventBus.Unsubscribe<SeismicSlamEvent>(OnSeismicSlam);
                eventBus.Unsubscribe<EnemyExplodedEvent>(OnEnemyExploded);
                eventBus.Unsubscribe<FrostLichNovaEvent>(OnFrostLichNova);
                eventBus.Unsubscribe<ArrowRainWaveEvent>(OnArrowRainWave);
                eventBus.Unsubscribe<SoulNovaCastEvent>(OnSoulNovaCast);
                eventBus.Unsubscribe<BarrelExplodedEvent>(OnBarrelExploded);
                eventBus.Unsubscribe<VacuumMagnetCollectedEvent>(OnVacuumMagnetCollected);
                eventBus.Unsubscribe<ChallengeShrineEvent>(OnChallengeShrine);
                eventBus.Unsubscribe<ShieldShockwaveEvent>(OnShieldShockwave);
                eventBus.Unsubscribe<AbilityOverdriveEvent>(OnAbilityOverdrive);
                eventBus.Unsubscribe<ComboMilestoneEvent>(OnComboMilestone);
                eventBus.Unsubscribe<SynergyReactionVfxEvent>(OnSynergyReactionVfx);
                eventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
                eventBus.Unsubscribe<AbilitySfxEvent>(OnAbilitySfx);
                eventBus.Unsubscribe<WaveEventStartedEvent>(OnWaveEventStarted);
                eventBus.Unsubscribe<WaveEventEndedEvent>(OnWaveEventEnded);
                eventBus.Unsubscribe<WaveMutatorStartedEvent>(OnWaveMutatorStarted);
                eventBus.Unsubscribe<WaveMutatorEndedEvent>(OnWaveMutatorEnded);
                eventBus.Unsubscribe<EnemySummonHealEvent>(OnEnemySummonHeal);
                eventBus.Unsubscribe<DemonLordEnragedEvent>(OnDemonLordEnraged);
                eventBus.Unsubscribe<AbilityVfxEvent>(OnAbilityVfx);
                eventBus.Unsubscribe<BossShardsGainedEvent>(OnBossShardsGained);
                eventBus.Unsubscribe<SecondWindTriggeredEvent>(OnSecondWind);
                eventBus.Unsubscribe<EnemySplitEvent>(OnEnemySplit);
                eventBus.Unsubscribe<FrostWispZoneSpawnedEvent>(OnFrostWispZoneSpawned);
                eventBus.Unsubscribe<SiegeShellBrokenEvent>(OnSiegeShellBroken);
            }

            if (heroHealth != null)
            {
                heroHealth.OnDamageTaken -= OnHeroDamaged;
            }

            if (heroUltimate != null)
            {
                heroUltimate.OnStateChanged -= OnUltimateStateChanged;
            }
        }

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            if (damagePool != null && evt.Damage > 0)
            {
                var item = AcquireDamageNumberItem();
                if (item != null)
                {
                    Color col = evt.IsBlocked
                        ? BlockedDamageColor
                        : (evt.IsCrit ? critDamageColor : (evt.IsBossOrMiniBoss ? bossDamageColor : normalDamageColor));
                    float scale = evt.IsBlocked
                        ? 0.85f
                        : (evt.IsCrit ? 1.45f : (evt.IsBossOrMiniBoss ? 1.25f : 1.0f));
                    item.Show(evt.Position, evt.Damage, col, scale, mainCamera, damagePool, evt.IsCrit);
                }
            }

            if (evt.IsBlocked)
            {
                EmitBurst(evt.Position + Vector3.up * 0.7f, BlockedDamageColor, 3, 2.8f, 0.16f);
            }

            audioManager?.PlaySfx(evt.IsCrit ? SfxType.CritHit : SfxType.Hit, volumeScale: evt.IsCrit ? 0.85f : (evt.IsBlocked ? 0.45f : 0.65f), randomizePitch: true);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            int count = evt.WasBoss ? 26 : (evt.WasMiniBoss ? 18 : (evt.WasElite ? 12 : 7));
            Color col = evt.WasBoss || evt.WasMiniBoss
                ? DeathColorBoss
                : (evt.WasElite ? GetEliteDeathColor(evt.EliteArchetype) : DeathColorNormal);
            EmitBurst(evt.Position + Vector3.up * 0.6f, col, count, evt.WasBoss ? 8f : 5.2f, evt.WasBoss ? 0.45f : 0.28f);

            if (evt.WasBoss)
            {
                audioManager?.PlaySfx(SfxType.BossDeath, volumeScale: 1.0f, randomizePitch: false);
                cameraController?.Shake(0.45f, 0.35f);
                TryTriggerHitStop(0.03f);
            }
            else if (evt.WasMiniBoss)
            {
                audioManager?.PlaySfx(SfxType.EnemyDeath, volumeScale: 0.9f, randomizePitch: true);
                cameraController?.Shake(0.26f, 0.2f);
            }
            else
            {
                audioManager?.PlaySfx(SfxType.EnemyDeath, volumeScale: 0.75f, randomizePitch: true);
            }
        }

        private static Color GetEliteDeathColor(EliteArchetype archetype)
        {
            return archetype switch
            {
                EliteArchetype.Juggernaut => DeathColorJuggernaut,
                EliteArchetype.HasteAura => DeathColorHasteAura,
                _ => DeathColorElite
            };
        }

        private void OnHeroDamaged(int amount)
        {
            if (heroHealth != null && damagePool != null && amount > 0)
            {
                var item = AcquireDamageNumberItem();
                item?.Show(heroHealth.transform.position, amount, heroDamageColor, 1.3f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.HeroHit, volumeScale: 0.9f, randomizePitch: true);
            cameraController?.Shake(0.24f, 0.16f);
        }

        private void OnXpCollected(XpCollectedEvent _)
        {
            if (heroHealth != null)
            {
                EmitBurst(heroHealth.transform.position + Vector3.up * 0.8f, XpSparkColor, 3, 3.2f, 0.18f);
            }
            audioManager?.PlaySfx(SfxType.XpPickup, volumeScale: 0.55f, randomizePitch: true);
        }

        private void OnCoinCollected(CoinCollectedEvent _)
        {
            if (heroHealth != null)
            {
                EmitBurst(heroHealth.transform.position + Vector3.up * 0.9f, CoinSparkColor, 4, 3.6f, 0.2f);
            }
            audioManager?.PlaySfx(SfxType.CoinPickup, volumeScale: 0.7f, randomizePitch: true);
        }

        private void OnHeroLeveledUp(HeroLeveledUpEvent _)
        {
            if (heroHealth != null)
            {
                EmitBurst(heroHealth.transform.position + Vector3.up * 0.8f, LevelUpColor, 22, 6.8f, 0.32f);
            }
            audioManager?.PlaySfx(SfxType.LevelUp, volumeScale: 0.9f, randomizePitch: false);
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            audioManager?.PlaySfx(SfxType.BossSpawn, volumeScale: evt.IsMiniBoss ? 0.75f : 1.0f, randomizePitch: false);
            if (!evt.IsMiniBoss)
            {
                audioManager?.PlayMusic(MusicType.Boss);
            }
            cameraController?.Shake(evt.IsMiniBoss ? 0.22f : 0.42f, evt.IsMiniBoss ? 0.2f : 0.35f);
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            if (!evt.IsMiniBoss)
            {
                audioManager?.PlayMusic(MusicType.Gameplay);
            }
        }

        private void OnAbilitySfx(AbilitySfxEvent evt)
        {
            audioManager?.PlaySfx(evt.SfxType, volumeScale: evt.VolumeScale, randomizePitch: true);
        }

        private void OnWaveEventStarted(WaveEventStartedEvent evt)
        {
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            Color accent = ProjectB.UI.WaveEventBannerUI.GetEventAccentColor(evt.EventType);
            EmitExplosionRing(heroPos + Vector3.up * 0.7f, 2.8f, accent, LevelUpColor, 20, 6, 5.8f, 0.28f);
            audioManager?.PlaySfx(evt.EventType == WaveEventType.GoldGoblin ? SfxType.ComboMilestone : SfxType.ShrineStart, volumeScale: 0.9f, randomizePitch: false);
            cameraController?.Shake(0.2f, 0.14f);
        }

        private void OnWaveEventEnded(WaveEventEndedEvent evt)
        {
            if (!evt.Completed) return;
            Color accent = ProjectB.UI.WaveEventBannerUI.GetEventAccentColor(evt.EventType);
            EmitExplosionRing(evt.RewardPosition + Vector3.up * 0.6f, 2.6f, accent, CoinSparkColor, 22, 8, 6.0f, 0.3f);
            audioManager?.PlaySfx(SfxType.SynergyFanfare, volumeScale: 0.85f, randomizePitch: false);
        }

        private void OnWaveMutatorStarted(WaveMutatorStartedEvent evt)
        {
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            Color accent = ProjectB.UI.WaveEventBannerUI.GetMutatorAccentColor(evt.MutatorType);
            EmitExplosionRing(heroPos + Vector3.up * 0.7f, 3.0f, accent, LevelUpColor, 20, 6, 5.8f, 0.28f);
            audioManager?.PlaySfx(SfxType.ShrineStart, volumeScale: 0.9f, randomizePitch: false);
            cameraController?.Shake(0.18f, 0.14f);
        }

        private void OnWaveMutatorEnded(WaveMutatorEndedEvent evt)
        {
            if (!evt.Completed) return;
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            Color accent = ProjectB.UI.WaveEventBannerUI.GetMutatorAccentColor(evt.MutatorType);
            EmitExplosionRing(heroPos + Vector3.up * 0.6f, 2.4f, accent, CoinSparkColor, 16, 6, 5.2f, 0.24f);
        }

        private void OnEnemySummonHeal(EnemySummonHealEvent evt)
        {
            EmitExplosionRing(evt.Position + Vector3.up * 0.5f, Mathf.Max(1.5f, evt.Radius * 0.5f), SoulNovaSecondaryColor, SoulNovaPrimaryColor, 16, 4, 4.4f, 0.24f);
        }

        private void OnAbilityVfx(AbilityVfxEvent evt)
        {
            Vector3 p = evt.Position;
            switch (evt.VfxType)
            {
                case AbilityVfxType.SkyfallTelegraph:
                    EmitBurst(p + Vector3.up * 0.2f, MeteorPrimaryColor, 6, 2.2f, 0.18f);
                    break;
                case AbilityVfxType.SkyfallStrike:
                    EmitExplosionRing(p + Vector3.up * 0.4f, Mathf.Max(1.5f, evt.Radius), MeteorPrimaryColor, FireballSecondaryColor, 22, 10, 6.4f, 0.34f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.7f, randomizePitch: true);
                    cameraController?.Shake(0.16f, 0.1f);
                    break;
                case AbilityVfxType.GlacialFreeze:
                    EmitExplosionRing(p + Vector3.up * 0.3f, Mathf.Max(1.5f, evt.Radius), FrostPrimaryColor, FrostSecondaryColor, 20, 4, 6.8f, 0.24f);
                    audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.45f, randomizePitch: true);
                    break;
                case AbilityVfxType.GlacialShatter:
                    EmitBurst(p + Vector3.up * 0.6f, FrostSecondaryColor, 8, 4.2f, 0.2f);
                    audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.35f, randomizePitch: true);
                    break;
                case AbilityVfxType.DecoyDeploy:
                    EmitBurst(p + Vector3.up * 0.8f, DecoyShadowColor, 12, 3.6f, 0.24f);
                    break;
                case AbilityVfxType.DecoyDetonate:
                    EmitExplosionRing(p + Vector3.up * 0.6f, Mathf.Max(1.5f, evt.Radius), DecoyShadowColor, SoulNovaPrimaryColor, 20, 8, 6.0f, 0.3f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.6f, randomizePitch: true);
                    break;
                case AbilityVfxType.ChakramRicochet:
                    EmitBurst(p + Vector3.up * 0.5f, SteamWhiteColor, 3, 3.0f, 0.12f);
                    break;
                case AbilityVfxType.ConsecrationCast:
                    EmitExplosionRing(p + Vector3.up * 0.3f, Mathf.Max(2f, evt.Radius), HolyGoldColor, LevelUpColor, 26, 8, 6.0f, 0.32f);
                    audioManager?.PlaySfx(SfxType.ShieldUlt, volumeScale: 0.9f, randomizePitch: false);
                    cameraController?.Shake(0.18f, 0.12f);
                    break;
                case AbilityVfxType.ConsecrationPulse:
                    EmitBurst(p + Vector3.up * 0.25f, HolyGoldColor, 6, 3.2f, 0.18f);
                    break;
                case AbilityVfxType.TurretDrop:
                    EmitExplosionRing(p + Vector3.up * 0.4f, Mathf.Max(1.5f, evt.Radius), EngineerOrangeColor, SteamWhiteColor, 18, 6, 6.0f, 0.3f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.55f, randomizePitch: true);
                    cameraController?.Shake(0.14f, 0.1f);
                    break;
                case AbilityVfxType.VampiricHeal:
                    EmitBurst(p + Vector3.up * 1.0f, CrimsonHealColor, 6, 2.6f, 0.16f);
                    break;
            }
        }

        private static readonly Color CrimsonHealColor = new Color(1f, 0.25f, 0.38f, 1f);

        private void OnBossShardsGained(BossShardsGainedEvent evt)
        {
            EmitBurst(evt.Position + Vector3.up * 1.2f, BossShardColor, 16, 5.0f, 0.26f);
            if (damagePool != null && evt.Amount > 0)
            {
                string label = evt.Amount < CachedShardLabels.Length ? CachedShardLabels[evt.Amount] : CachedShardLabels[CachedShardLabels.Length - 1];
                var item = AcquireDamageNumberItem();
                item?.ShowText(evt.Position, label, BossShardColor, 1.35f, 1.7f, mainCamera, damagePool);
            }
            audioManager?.PlaySfx(SfxType.SecretPickup, volumeScale: 0.8f, randomizePitch: false);
        }

        private void OnSecondWind(SecondWindTriggeredEvent evt)
        {
            EmitExplosionRing(evt.Position + Vector3.up * 0.8f, Mathf.Max(2f, evt.Radius), ShieldUltColor, LevelUpColor, 30, 12, 7.5f, 0.4f);
            if (damagePool != null)
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(evt.Position, "SECOND WIND!", ShieldUltColor, 1.6f, 2.0f, mainCamera, damagePool);
            }
            audioManager?.PlaySfx(SfxType.SynergyFanfare, volumeScale: 1.0f, randomizePitch: false);
            cameraController?.Shake(0.35f, 0.25f);
        }

        private void OnDemonLordEnraged(DemonLordEnragedEvent evt)
        {
            EmitExplosionRing(evt.Position + Vector3.up * 0.8f, 4.0f, DemonEnrageColor, FireballSecondaryColor, 28, 12, 7.5f, 0.4f);
            if (damagePool != null)
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(evt.Position, "DEMON LORD ENRAGED!", DemonEnrageColor, 1.55f, 2.0f, mainCamera, damagePool);
            }
            audioManager?.PlaySfx(SfxType.BossSpawn, volumeScale: 1.0f, randomizePitch: false);
            cameraController?.Shake(0.42f, 0.3f);
        }

        private void OnSecretItemCollected(SecretItemCollectedEvent evt)
        {
            if (heroHealth != null)
            {
                Vector3 heroPos = heroHealth.transform.position;
                EmitBurst(heroPos + Vector3.up * 1.0f, SecretPickupColor, 18, 6.0f, 0.28f);

                if (damagePool != null)
                {
                    string label = !string.IsNullOrEmpty(evt.RewardSummary) ? evt.RewardSummary : evt.DisplayName;
                    if (!string.IsNullOrEmpty(label))
                    {
                        var item = AcquireDamageNumberItem();
                        item?.ShowText(heroPos, label, SecretRewardTextColor, 1.35f, 1.6f, mainCamera, damagePool);
                    }
                }
            }
            audioManager?.PlaySfx(SfxType.SecretPickup, volumeScale: 0.9f, randomizePitch: false);
        }

        private void OnFireballExploded(FireballExplodedEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.35f,
                Mathf.Max(1f, evt.Radius),
                FireballPrimaryColor,
                FireballSecondaryColor,
                ringCount: 16,
                centerCount: 4,
                speed: 4.5f,
                size: 0.3f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.55f, randomizePitch: true);
            cameraController?.Shake(0.08f, 0.08f);
        }

        private void OnMeteorImpact(MeteorImpactEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.4f,
                Mathf.Max(1.5f, evt.Radius),
                MeteorPrimaryColor,
                MeteorSecondaryColor,
                ringCount: 20,
                centerCount: 8,
                speed: 6.5f,
                size: 0.4f);
            cameraController?.Shake(0.28f, 0.18f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.78f, randomizePitch: true);
        }

        private void OnSynergyActivated(SynergyActivatedEvent evt)
        {
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            EmitExplosionRing(
                heroPos + Vector3.up * 0.9f,
                2.8f,
                SynergyGoldColor,
                SynergyPurpleColor,
                ringCount: 24,
                centerCount: 12,
                speed: 6.2f,
                size: 0.34f);

            if (damagePool != null && !string.IsNullOrEmpty(evt.DisplayName))
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(heroPos, evt.DisplayName, SynergyGoldColor, 1.45f, 1.8f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.SynergyFanfare, volumeScale: 0.95f, randomizePitch: false);
            cameraController?.Shake(0.24f, 0.16f);
        }

        private void OnSeismicSlam(SeismicSlamEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.35f,
                Mathf.Max(1.5f, evt.Radius),
                SeismicPrimaryColor,
                SeismicSecondaryColor,
                ringCount: 22,
                centerCount: 8,
                speed: 6.0f,
                size: 0.38f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.72f, randomizePitch: true);
            cameraController?.Shake(0.22f, 0.16f);
        }

        private void OnEnemyExploded(EnemyExplodedEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.35f,
                Mathf.Max(1.2f, evt.Radius),
                ExploderPrimaryColor,
                ExploderSecondaryColor,
                ringCount: 18,
                centerCount: 8,
                speed: 5.5f,
                size: 0.34f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.62f, randomizePitch: true);
            cameraController?.Shake(0.18f, 0.14f);
        }

        private void OnEnemySplit(EnemySplitEvent evt)
        {
            EmitBurst(evt.Position + Vector3.up * 0.35f, XpSparkColor, count: 14, speed: 4.5f, size: 0.28f);
            EmitBurst(evt.Position + Vector3.up * 0.45f, FrostPrimaryColor, count: 8, speed: 3.4f, size: 0.22f);
        }

        private void OnFrostWispZoneSpawned(FrostWispZoneSpawnedEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.25f,
                Mathf.Max(1.2f, evt.Radius),
                FrostPrimaryColor,
                FrostSecondaryColor,
                ringCount: 16,
                centerCount: 6,
                speed: 4.2f,
                size: 0.26f);
            audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.45f, randomizePitch: true);
        }

        private void OnSiegeShellBroken(SiegeShellBrokenEvent evt)
        {
            float scale = Mathf.Max(0.8f, evt.Scale);
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.65f,
                2.2f * scale,
                SeismicSecondaryColor,
                FireballPrimaryColor,
                ringCount: 22,
                centerCount: 10,
                speed: 6.4f,
                size: 0.38f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.55f, randomizePitch: true);
            cameraController?.Shake(0.18f, 0.12f);
            TryTriggerHitStop(0.025f);
        }

        private void OnFrostLichNova(FrostLichNovaEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.35f,
                Mathf.Max(2f, evt.Radius),
                FrostPrimaryColor,
                FrostSecondaryColor,
                ringCount: evt.IsBlizzard ? 28 : 22,
                centerCount: 10,
                speed: 6.2f,
                size: 0.36f);
            audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.8f, randomizePitch: true);
            cameraController?.Shake(evt.IsBlizzard ? 0.22f : 0.18f, 0.15f);
        }

        private void OnArrowRainWave(ArrowRainWaveEvent evt)
        {
            if (burstParticles != null)
            {
                float radius = Mathf.Max(2f, evt.Radius);
                var emitParams = new ParticleSystem.EmitParams
                {
                    startSize = 0.26f
                };

                for (int i = 0; i < 18; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * radius;
                    emitParams.position = new Vector3(
                        evt.Position.x + offset.x,
                        evt.Position.y + Random.Range(5.5f, 7.2f),
                        evt.Position.z + offset.y);
                    emitParams.velocity = new Vector3(
                        Random.Range(-0.8f, 0.8f),
                        Random.Range(-19f, -15f),
                        Random.Range(-0.8f, 0.8f));
                    emitParams.startColor = (i & 1) == 0 ? ArrowRainPrimaryColor : ArrowRainSecondaryColor;
                    burstParticles.Emit(emitParams, 1);
                }
            }

            EmitExplosionRing(
                evt.Position + Vector3.up * 0.2f,
                Mathf.Max(2f, evt.Radius * 0.65f),
                ArrowRainPrimaryColor,
                ArrowRainSecondaryColor,
                ringCount: 14,
                centerCount: 0,
                speed: 4.2f,
                size: 0.22f);
            audioManager?.PlaySfx(SfxType.ArrowWhoosh, volumeScale: 0.85f, randomizePitch: true);
            cameraController?.Shake(0.14f, 0.1f);
        }

        private void OnSoulNovaCast(SoulNovaCastEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.6f,
                2.4f,
                SoulNovaPrimaryColor,
                SoulNovaSecondaryColor,
                ringCount: 24,
                centerCount: 8,
                speed: 6.5f,
                size: 0.34f);

            if (evt.HealAmount > 0 && damagePool != null)
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(evt.Position, DamageNumberItem.GetCachedHealText(evt.HealAmount), SoulHealTextColor, 1.3f, 1.4f, mainCamera, damagePool);
            }
        }

        private void OnBarrelExploded(BarrelExplodedEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.5f,
                Mathf.Max(1.5f, evt.Radius),
                BarrelPrimaryColor,
                BarrelSecondaryColor,
                ringCount: 26,
                centerCount: 12,
                speed: 7.5f,
                size: 0.42f);
            audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.95f, randomizePitch: true);
            audioManager?.PlaySfx(SfxType.EnemyDeath, volumeScale: 0.85f, randomizePitch: true);
            cameraController?.Shake(0.32f, 0.24f);
            TryTriggerHitStop(0.03f);
        }

        private void OnVacuumMagnetCollected(VacuumMagnetCollectedEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.7f,
                2.6f,
                VacuumPrimaryColor,
                VacuumSecondaryColor,
                ringCount: 22,
                centerCount: 8,
                speed: 6.0f,
                size: 0.3f);

            if (damagePool != null)
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(evt.Position, "VACUUM MAGNET!", VacuumPrimaryColor, 1.35f, 1.6f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.SecretPickup, volumeScale: 0.95f, randomizePitch: false);
            cameraController?.Shake(0.16f, 0.12f);
        }

        private void OnShieldShockwave(ShieldShockwaveEvent evt)
        {
            EmitExplosionRing(
                evt.Position + Vector3.up * 0.6f,
                Mathf.Max(1.5f, evt.Radius),
                ShieldUltColor,
                LevelUpColor,
                ringCount: 22,
                centerCount: 6,
                speed: 6.2f,
                size: 0.32f);
            cameraController?.Shake(0.18f, 0.12f);
        }

        private void OnAbilityOverdrive(AbilityOverdriveEvent evt)
        {
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            Color primary = evt.Tier == 1
                ? OverdriveGoldColor
                : (evt.Tier == 2 ? AwakenedCyanColor : MythicMagentaColor);

            EmitExplosionRing(
                heroPos + Vector3.up * 0.9f,
                2.6f,
                primary,
                SynergyGoldColor,
                ringCount: 24,
                centerCount: 10,
                speed: 6.4f,
                size: 0.34f);

            if (damagePool != null && !string.IsNullOrEmpty(evt.TierLabel))
            {
                string label = !string.IsNullOrEmpty(evt.AbilityName)
                    ? $"{evt.AbilityName.ToUpperInvariant()} {evt.TierLabel}"
                    : evt.TierLabel;
                var item = AcquireDamageNumberItem();
                item?.ShowText(heroPos, label, primary, 1.45f, 1.8f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.LevelUp, volumeScale: 0.95f, randomizePitch: false);
            cameraController?.Shake(0.24f, 0.16f);
        }

        private void OnComboMilestone(ComboMilestoneEvent evt)
        {
            Vector3 heroPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            EmitBurst(heroPos + Vector3.up * 1.1f, ComboTextColor, 14, 5.2f, 0.24f);

            if (damagePool != null &&
                evt.MilestoneTier > 0 &&
                evt.MilestoneTier < CachedComboMilestoneLabels.Length)
            {
                var item = AcquireDamageNumberItem();
                item?.ShowText(heroPos, CachedComboMilestoneLabels[evt.MilestoneTier], ComboTextColor, 1.3f, 1.45f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.ComboMilestone, volumeScale: 0.85f, randomizePitch: false);
        }

        private void OnSynergyReactionVfx(SynergyReactionVfxEvent evt)
        {
            switch (evt.ReactionType)
            {
                case SynergyReactionVfxType.ToxicInfernoIgnite:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.35f, Mathf.Max(1.5f, evt.Radius), FireballPrimaryColor, XpSparkColor, 20, 8, 5.4f, 0.32f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.65f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.SteamBlastNova:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.5f, Mathf.Max(2f, evt.Radius), SteamWhiteColor, FrostPrimaryColor, 22, 8, 6.2f, 0.34f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.65f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.ThunderstormShatter:
                    EmitBurst(evt.Position + Vector3.up * 0.6f, FrostPrimaryColor, 12, 4.8f, 0.24f);
                    audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.6f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.AegisPhalanxWave:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.5f, Mathf.Max(1.4f, evt.Radius), SynergyGoldColor, ShieldUltColor, 16, 4, 4.8f, 0.26f);
                    audioManager?.PlaySfx(SfxType.CritHit, volumeScale: 0.55f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.CataclysmShards:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.6f, Mathf.Max(1.6f, evt.Radius), SynergyPurpleColor, SoulNovaPrimaryColor, 18, 6, 5.8f, 0.3f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.65f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.DragonBurn:
                    EmitBurst(evt.Position + Vector3.up * 0.2f, FireballPrimaryColor, 6, 3.2f, 0.2f);
                    break;

                case SynergyReactionVfxType.ShatterNova:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.6f, Mathf.Max(1.5f, evt.Radius), FrostPrimaryColor, SteamWhiteColor, 20, 8, 6.4f, 0.3f);
                    audioManager?.PlaySfx(SfxType.IceShatter, volumeScale: 0.7f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.RicochetBarrage:
                    EmitBurst(evt.Position + Vector3.up * 0.6f, SynergyGoldColor, 10, 4.6f, 0.2f);
                    audioManager?.PlaySfx(SfxType.CritHit, volumeScale: 0.5f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.SolarEclipse:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.5f, Mathf.Max(2f, evt.Radius), SynergyPurpleColor, FireballSecondaryColor, 24, 6, 5.0f, 0.3f);
                    audioManager?.PlaySfx(SfxType.SynergyFanfare, volumeScale: 0.5f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.PhantomLegion:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.7f, Mathf.Max(1.5f, evt.Radius), FireballPrimaryColor, DecoyShadowColor, 18, 6, 6.0f, 0.3f);
                    audioManager?.PlaySfx(SfxType.Explosion, volumeScale: 0.6f, randomizePitch: true);
                    break;

                case SynergyReactionVfxType.BladeStormVolley:
                    EmitExplosionRing(evt.Position + Vector3.up * 0.6f, Mathf.Max(1.5f, evt.Radius), ShieldUltColor, SteamWhiteColor, 14, 0, 4.8f, 0.22f);
                    audioManager?.PlaySfx(SfxType.ArrowWhoosh, volumeScale: 0.55f, randomizePitch: true);
                    break;
            }
        }

        private void OnChallengeShrine(ChallengeShrineEvent evt)
        {
            switch (evt.EventType)
            {
                case ShrineEventType.Started:
                    EmitExplosionRing(
                        evt.Position + Vector3.up * 0.6f,
                        3.2f,
                        ShrineStartColor,
                        SoulNovaSecondaryColor,
                        ringCount: 24,
                        centerCount: 10,
                        speed: 6.2f,
                        size: 0.34f);
                    if (damagePool != null)
                    {
                        var item = AcquireDamageNumberItem();
                        string startMsg = evt.TotalElites == 6
                            ? CachedShrineStartText
                            : $"CHALLENGE! Defeat {evt.TotalElites} Elites!";
                        item?.ShowText(evt.Position, startMsg, ShrineStartColor, 1.35f, 1.8f, mainCamera, damagePool);
                    }
                    audioManager?.PlaySfx(SfxType.ShrineStart, volumeScale: 0.9f, randomizePitch: false);
                    cameraController?.Shake(0.26f, 0.2f);
                    break;

                case ShrineEventType.Progress:
                    EmitBurst(evt.Position + Vector3.up * 0.8f, ShrineStartColor, 8, 4.2f, 0.24f);
                    if (damagePool != null)
                    {
                        int defeated = Mathf.Max(0, evt.TotalElites - evt.RemainingElites);
                        string progMsg = evt.TotalElites == 6 && defeated >= 0 && defeated < CachedShrineProgressStrings.Length
                            ? CachedShrineProgressStrings[defeated]
                            : $"SHRINE: {defeated}/{evt.TotalElites}";
                        var item = AcquireDamageNumberItem();
                        item?.ShowText(evt.Position, progMsg, ShrineStartColor, 1.15f, 1.1f, mainCamera, damagePool);
                    }
                    break;

                case ShrineEventType.Completed:
                    EmitExplosionRing(
                        evt.Position + Vector3.up * 0.8f,
                        3.6f,
                        ShrineCompleteColor,
                        SynergyGoldColor,
                        ringCount: 28,
                        centerCount: 12,
                        speed: 6.8f,
                        size: 0.38f);
                    if (damagePool != null)
                    {
                        var item = AcquireDamageNumberItem();
                        string completeMsg = evt.CoinReward == 100
                            ? CachedShrineCompleteText
                            : $"SHRINE CLEARED! +{evt.CoinReward} Coins";
                        item?.ShowText(evt.Position, completeMsg, ShrineCompleteColor, 1.45f, 2.0f, mainCamera, damagePool);
                    }
                    audioManager?.PlaySfx(SfxType.LevelUp, volumeScale: 0.95f, randomizePitch: false);
                    audioManager?.PlaySfx(SfxType.SecretPickup, volumeScale: 0.9f, randomizePitch: false);
                    cameraController?.Shake(0.3f, 0.22f);
                    break;
            }
        }

        private void OnUltimateStateChanged()
        {
            if (heroUltimate != null && heroUltimate.IsActive && heroHealth != null)
            {
                HeroUltimateType ultType = heroUltimate.ActiveHeroData != null
                    ? heroUltimate.ActiveHeroData.ultimateType
                    : HeroUltimateType.Shield;

                Color burstColor = ultType switch
                {
                    HeroUltimateType.MeteorStrike => MeteorUltColor,
                    HeroUltimateType.ArrowRain => ArrowRainPrimaryColor,
                    HeroUltimateType.SoulNova => SoulNovaPrimaryColor,
                    _ => ShieldUltColor
                };
                EmitBurst(heroHealth.transform.position + Vector3.up * 1.0f, burstColor, 18, 6.5f, 0.3f);
                audioManager?.PlaySfx(SfxType.ShieldUlt, volumeScale: 0.9f, randomizePitch: false);
                cameraController?.Shake(0.18f, 0.14f);
            }
        }

        public void EmitExplosionRing(
            Vector3 worldPos,
            float radius,
            Color primaryColor,
            Color secondaryColor,
            int ringCount,
            int centerCount,
            float speed,
            float size)
        {
            if (burstParticles == null) return;

            var emitParams = new ParticleSystem.EmitParams
            {
                startSize = size
            };

            float step = (Mathf.PI * 2f) / Mathf.Max(1, ringCount);
            for (int i = 0; i < ringCount; i++)
            {
                float angle = step * i + Random.Range(-0.12f, 0.12f);
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float spawnDist = radius * Random.Range(0.35f, 0.85f);

                emitParams.position = new Vector3(
                    worldPos.x + cos * spawnDist,
                    worldPos.y,
                    worldPos.z + sin * spawnDist);
                emitParams.velocity = new Vector3(cos, Random.Range(0.45f, 0.85f), sin).normalized * Random.Range(speed * 0.7f, speed);
                emitParams.startColor = (i & 1) == 0 ? primaryColor : secondaryColor;
                burstParticles.Emit(emitParams, 1);
            }

            if (centerCount > 0)
            {
                EmitBurst(worldPos, secondaryColor, centerCount, speed * 0.8f, size * 1.1f);
            }
        }

        public void EmitBurst(Vector3 worldPos, Color color, int count, float speed, float size)
        {
            if (burstParticles == null || count <= 0) return;

            var emitParams = new ParticleSystem.EmitParams
            {
                startColor = color,
                startSize = size
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.85f + 0.35f;
                emitParams.position = worldPos;
                emitParams.velocity = dir.normalized * Random.Range(speed * 0.55f, speed);
                burstParticles.Emit(emitParams, 1);
            }
        }
    }
}
