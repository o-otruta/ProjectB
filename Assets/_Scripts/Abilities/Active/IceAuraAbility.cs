using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class IceAuraAbility : ActiveAbility
    {
        private IceAuraAbilityData IceData => Data as IceAuraAbilityData;

        private float currentDamage;
        private float currentRadius;
        private float currentSlowFactor;
        private float currentSlowDuration;
        
        private float nextTickTime;
        private Collider[] hitBuffer = new Collider[128];
        private GameObject visualAura;

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
            visualAura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualAura.name = "IceAura_Visual";
            visualAura.transform.SetParent(transform);
            visualAura.transform.localPosition = Vector3.zero;
            
            UpdateVisualSize();

            var col = visualAura.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var renderer = visualAura.GetComponent<Renderer>();
            if (renderer != null && IceData.auraMaterial != null)
            {
                renderer.sharedMaterial = IceData.auraMaterial;
            }
        }

        private void UpdateVisualSize()
        {
            if (visualAura != null)
            {
                visualAura.transform.localScale = new Vector3(currentRadius * 2f, 0.01f, currentRadius * 2f);
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
                    DealDamage(damageable, scaledDamage, isCrit);
                }
                
                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(currentSlowFactor, currentSlowDuration);
                }
            }
        }
    }
}
