using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
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

        private Camera mainCamera;
        private IObjectPool<DamageNumberItem> damagePool;
        private ParticleSystem burstParticles;
        private bool isSubscribed;

        private static readonly Color DeathColorNormal = new Color(0.95f, 0.35f, 0.28f, 1f);
        private static readonly Color DeathColorElite = new Color(0.75f, 0.22f, 0.95f, 1f);
        private static readonly Color DeathColorBoss = new Color(1f, 0.72f, 0.15f, 1f);
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

        [Inject]
        public void Construct(
            GameEventBus eventBus,
            AudioManager audioManager = null,
            CameraController cameraController = null,
            HeroHealth heroHealth = null,
            HeroUltimate heroUltimate = null)
        {
            this.eventBus = eventBus;
            this.audioManager = audioManager;
            this.cameraController = cameraController;
            this.heroHealth = heroHealth;
            this.heroUltimate = heroUltimate;
        }

        private void Start()
        {
            mainCamera = cameraController != null
                ? cameraController.GetComponent<Camera>()
                : Camera.main;
            if (mainCamera == null) mainCamera = Camera.main;

            InitializeDamagePool();
            InitializeBurstParticles();
            Subscribe();

            audioManager?.PlayMusic(MusicType.Gameplay);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void InitializeDamagePool()
        {
            var canvasGo = new GameObject("DamageNumbersCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

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
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.raycastTarget = false;
                    tmp.outlineWidth = 0.22f;
                    tmp.outlineColor = new Color32(10, 10, 14, 220);

                    var item = go.AddComponent<DamageNumberItem>();
                    item.SetTextMesh(tmp);
                    return item;
                },
                actionOnGet: item => item.gameObject.SetActive(true),
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item => Destroy(item.gameObject),
                collectionCheck: false,
                defaultCapacity: 40,
                maxSize: 120);
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
                Destroy(tempCube);
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
                var item = damagePool.Get();
                Color col = evt.IsCrit
                    ? critDamageColor
                    : (evt.IsBossOrMiniBoss ? bossDamageColor : normalDamageColor);
                float scale = evt.IsCrit
                    ? 1.45f
                    : (evt.IsBossOrMiniBoss ? 1.25f : 1.0f);
                item.Show(evt.Position, evt.Damage, col, scale, mainCamera, damagePool, evt.IsCrit);
            }

            audioManager?.PlaySfx(SfxType.Hit, volumeScale: evt.IsCrit ? 0.8f : 0.65f, randomizePitch: true);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            int count = evt.WasBoss ? 26 : (evt.WasMiniBoss ? 18 : (evt.WasElite ? 12 : 7));
            Color col = evt.WasBoss || evt.WasMiniBoss ? DeathColorBoss : (evt.WasElite ? DeathColorElite : DeathColorNormal);
            EmitBurst(evt.Position + Vector3.up * 0.6f, col, count, evt.WasBoss ? 8f : 5.2f, evt.WasBoss ? 0.45f : 0.28f);

            audioManager?.PlaySfx(SfxType.EnemyDeath, volumeScale: evt.WasBoss ? 1.0f : 0.75f, randomizePitch: true);

            if (evt.WasBoss)
            {
                cameraController?.Shake(0.45f, 0.35f);
            }
            else if (evt.WasMiniBoss)
            {
                cameraController?.Shake(0.26f, 0.2f);
            }
        }

        private void OnHeroDamaged(int amount)
        {
            if (heroHealth != null && damagePool != null && amount > 0)
            {
                var item = damagePool.Get();
                item.Show(heroHealth.transform.position, amount, heroDamageColor, 1.3f, mainCamera, damagePool);
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
            cameraController?.Shake(evt.IsMiniBoss ? 0.22f : 0.42f, evt.IsMiniBoss ? 0.2f : 0.35f);
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
                        var item = damagePool.Get();
                        item.ShowText(heroPos, label, SecretRewardTextColor, 1.35f, 1.6f, mainCamera, damagePool);
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
            audioManager?.PlaySfx(SfxType.BossSpawn, volumeScale: 0.65f, randomizePitch: true);
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
                var item = damagePool.Get();
                item.ShowText(heroPos, evt.DisplayName, SynergyGoldColor, 1.45f, 1.8f, mainCamera, damagePool);
            }

            audioManager?.PlaySfx(SfxType.LevelUp, volumeScale: 0.95f, randomizePitch: false);
            audioManager?.PlaySfx(SfxType.SecretPickup, volumeScale: 0.85f, randomizePitch: false);
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
            audioManager?.PlaySfx(SfxType.BossSpawn, volumeScale: 0.6f, randomizePitch: true);
            cameraController?.Shake(0.22f, 0.16f);
        }

        private void OnUltimateStateChanged()
        {
            if (heroUltimate != null && heroUltimate.IsActive && heroHealth != null)
            {
                HeroUltimateType ultType = heroUltimate.ActiveHeroData != null
                    ? heroUltimate.ActiveHeroData.ultimateType
                    : HeroUltimateType.Shield;

                Color burstColor = ultType == HeroUltimateType.MeteorStrike ? MeteorUltColor : ShieldUltColor;
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
