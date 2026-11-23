using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class IceAuraAbility : ActiveAbility
    {
        public const float VISUAL_Y_OFFSET = 0.03f;

        private IceAuraAbilityData IceData => Data as IceAuraAbilityData;

        private float currentDamage;
        private float currentRadius;
        private float currentSlowFactor;
        private float currentSlowDuration;
        
        private float nextTickTime;
        private Collider[] hitBuffer = new Collider[128];
        private GameObject visualAura;
        private bool usesCustomVisualPrefab;

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

            UpdateVisualVerticalOffset();
            UpdateVisualSize();
        }

        private void UpdateVisualVerticalOffset()
        {
            if (visualAura == null) return;
            visualAura.transform.localPosition = new Vector3(0f, VISUAL_Y_OFFSET - transform.position.y, 0f);
        }

        private void UpdateVisualSize()
        {
            if (visualAura != null)
            {
                visualAura.transform.localScale = usesCustomVisualPrefab
                    ? new Vector3(currentRadius, 1f, currentRadius)
                    : new Vector3(currentRadius * 2f, 0.01f, currentRadius * 2f);
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
                UpdateVisualSize();
            }
            else if (tier == 2)
            {
                currentRadius *= 1.15f;
                currentSlowFactor = Mathf.Max(0.1f, currentSlowFactor - 0.1f);
                UpdateVisualSize();
            }
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            UpdateVisualVerticalOffset();
            if (visualAura != null)
            {
                visualAura.transform.Rotate(Vector3.up, 18f * Time.deltaTime, Space.Self);
            }

            if (Time.time >= nextTickTime)
            {
                ApplyAuraTick();
                nextTickTime = Time.time + GetEffectiveCooldown(IceData.tickRate, 0.15f);
            }
        }

        private void ApplyAuraTick()
        {
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, hitBuffer, IceData.targetLayer);
            
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
