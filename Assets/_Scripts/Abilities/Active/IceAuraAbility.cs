using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class IceAuraAbility : ActiveAbility
    {
        public const float VISUAL_Y_OFFSET = 0.03f;
        private const float PULSE_DURATION = 0.32f;
        private static readonly int PulsePropertyId = Shader.PropertyToID("_Pulse");

        private IceAuraAbilityData IceData => Data as IceAuraAbilityData;

        private float currentDamage;
        private float currentRadius;
        public float EffectiveRadius => ScaleArea(currentRadius);
        private float currentSlowFactor;
        private float currentSlowDuration;
        
        private float nextTickTime;
        private Collider[] hitBuffer = new Collider[128];
        private GameObject visualAura;
        private bool usesCustomVisualPrefab;
        private Transform groundFieldTransform;
        private MeshRenderer groundFieldRenderer;
        private Transform crystalRingTransform;
        private Transform[] crystalTransforms;
        private float[] crystalBaseAngles;
        private ParticleSystem snowParticles;
        private MaterialPropertyBlock pulsePropBlock;
        private float pulseTimer;
        private float lastPushedPulse = -1f;
        private float crystalTierScale = 1f;

        public Transform VisualTransform => visualAura != null ? visualAura.transform : null;

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            
            currentDamage = IceData.baseDamage;
            currentRadius = IceData.auraRadius;
            currentSlowFactor = IceData.slowFactor;
            currentSlowDuration = IceData.slowDuration;

            CreateVisual();
        }

        private void CreateVisual()
        {
            if (IceData != null && IceData.visualPrefab != null)
            {
                visualAura = Instantiate(IceData.visualPrefab, transform, false);
                visualAura.name = "IceAura_Visual";
                usesCustomVisualPrefab = true;
            }
            else
            {
                visualAura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visualAura.name = "IceAura_Visual";
                visualAura.transform.SetParent(transform, false);
                usesCustomVisualPrefab = false;

                var col = visualAura.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                var renderer = visualAura.GetComponent<Renderer>();
                if (renderer != null && IceData != null && IceData.auraMaterial != null)
                {
                    renderer.sharedMaterial = IceData.auraMaterial;
                }
            }

            var renderers = visualAura.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers[i].receiveShadows = false;
                }
            }

            CacheVisualParts();
            UpdateVisualVerticalOffset();
            UpdateVisualSize();
        }

        private void CacheVisualParts()
        {
            groundFieldTransform = visualAura.transform.Find("GroundField");
            groundFieldRenderer = groundFieldTransform != null ? groundFieldTransform.GetComponent<MeshRenderer>() : null;
            crystalRingTransform = visualAura.transform.Find("CrystalRing");
            snowParticles = visualAura.GetComponentInChildren<ParticleSystem>(true);

            if (crystalRingTransform != null)
            {
                int count = crystalRingTransform.childCount;
                crystalTransforms = new Transform[count];
                crystalBaseAngles = new float[count];
                float step = count > 0 ? (Mathf.PI * 2f) / count : 0f;
                for (int i = 0; i < count; i++)
                {
                    crystalTransforms[i] = crystalRingTransform.GetChild(i);
                    crystalBaseAngles[i] = step * i;
                }
            }
            else
            {
                crystalTransforms = null;
                crystalBaseAngles = null;
            }

            if (pulsePropBlock == null)
            {
                pulsePropBlock = new MaterialPropertyBlock();
            }
            pulseTimer = 0f;
            lastPushedPulse = -1f;
            PushPulseProperty(0f);
        }

        private void UpdateVisualVerticalOffset()
        {
            if (visualAura == null) return;
            visualAura.transform.localPosition = new Vector3(0f, VISUAL_Y_OFFSET - transform.position.y, 0f);
        }

        private void UpdateVisualSize()
        {
            if (visualAura == null) return;

            float r = EffectiveRadius;
            if (groundFieldTransform != null)
            {
                visualAura.transform.localScale = Vector3.one;
                groundFieldTransform.localScale = new Vector3(r * 2f, r * 2f, 1f);

                if (crystalTransforms != null)
                {
                    float ringRadius = r * 0.95f;
                    Vector3 cScale = Vector3.one * crystalTierScale;
                    for (int i = 0; i < crystalTransforms.Length; i++)
                    {
                        Transform crystal = crystalTransforms[i];
                        if (crystal == null) continue;
                        float angle = crystalBaseAngles[i];
                        float y = crystal.localPosition.y;
                        crystal.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius);
                        crystal.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
                        crystal.localScale = cScale;
                    }
                }

                if (snowParticles != null)
                {
                    var shape = snowParticles.shape;
                    shape.radius = Mathf.Max(0.5f, r * 0.92f);
                    var emission = snowParticles.emission;
                    emission.rateOverTime = Mathf.Clamp(r * 7.5f, 18f, 36f);
                }
            }
            else
            {
                visualAura.transform.localScale = usesCustomVisualPrefab
                    ? new Vector3(r, 1f, r)
                    : new Vector3(r * 2f, 0.01f, r * 2f);
            }
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Radius: 
                    currentRadius += value; 
                    UpdateVisualSize();
                    break;
                case ModifierType.SlowAmount: 
                    currentSlowFactor = Mathf.Clamp(currentSlowFactor - value, 0.1f, 0.9f);
                    break;
                case ModifierType.Duration: currentSlowDuration += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentRadius *= 1.2f;
                crystalTierScale = 1.15f;
                UpdateVisualSize();
            }
            else if (tier == 2)
            {
                currentRadius *= 1.15f;
                currentSlowFactor = Mathf.Max(0.1f, currentSlowFactor - 0.1f);
                crystalTierScale = 1.28f;
                UpdateVisualSize();
            }
        }

        private float lastAreaMultiplier = 1f;

        private void Update()
        {
            if (!Mathf.Approximately(lastAreaMultiplier, AreaMultiplier))
            {
                lastAreaMultiplier = AreaMultiplier;
                UpdateVisualSize();
            }

            if (Data == null || Time.deltaTime <= 0f) return;

            UpdateVisualVerticalOffset();
            if (crystalRingTransform != null)
            {
                crystalRingTransform.Rotate(Vector3.up, 26f * Time.deltaTime, Space.Self);
                if (crystalTransforms != null)
                {
                    float now = Time.time;
                    for (int i = 0; i < crystalTransforms.Length; i++)
                    {
                        Transform crystal = crystalTransforms[i];
                        if (crystal == null) continue;
                        Vector3 pos = crystal.localPosition;
                        pos.y = Mathf.Sin(now * 2.6f + i * 0.85f) * 0.045f;
                        crystal.localPosition = pos;
                    }
                }
            }
            else if (visualAura != null)
            {
                visualAura.transform.Rotate(Vector3.up, 18f * Time.deltaTime, Space.Self);
            }

            if (pulseTimer > 0f)
            {
                pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime);
                PushPulseProperty(pulseTimer / PULSE_DURATION);
            }
            else if (lastPushedPulse > 0.0001f)
            {
                PushPulseProperty(0f);
            }

            if (Time.time >= nextTickTime)
            {
                ApplyAuraTick();
                nextTickTime = Time.time + GetEffectiveCooldown(IceData.tickRate, 0.15f);
            }
        }

        private void PushPulseProperty(float pulseValue)
        {
            if (groundFieldRenderer == null) return;
            float clamped = Mathf.Clamp01(pulseValue);
            if (Mathf.Approximately(lastPushedPulse, clamped)) return;
            lastPushedPulse = clamped;
            if (pulsePropBlock == null) pulsePropBlock = new MaterialPropertyBlock();
            pulsePropBlock.SetFloat(PulsePropertyId, clamped);
            groundFieldRenderer.SetPropertyBlock(pulsePropBlock);
        }

        private void ApplyAuraTick()
        {
            pulseTimer = PULSE_DURATION;
            PushPulseProperty(1f);

            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, EffectiveRadius, hitBuffer, IceData.targetLayer);
            
            for (int i = 0; i < targetsFound; i++)
            {
                var col = hitBuffer[i];
                if (col == null) continue;
                if (col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    int scaledDamage = RollScaledDamage(currentDamage, out bool isCrit);
                    DealDamage(damageable, scaledDamage, isCrit, isAoE: true);
                }
                
                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(currentSlowFactor, currentSlowDuration);
                }
            }
        }
    }
}
