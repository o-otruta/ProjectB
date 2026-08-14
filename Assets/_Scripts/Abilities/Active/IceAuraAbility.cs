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
        private Collider[] hitBuffer = new Collider[100];
        private GameObject visualAura;

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

            Destroy(visualAura.GetComponent<CapsuleCollider>());
            
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0f, 0.5f, 1f, 0.3f);
            
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            
            visualAura.GetComponent<Renderer>().material = mat;
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

        private void Update()
        {
            if (Data == null) return;

            if (Time.time >= nextTickTime)
            {
                ApplyAuraTick();
                nextTickTime = Time.time + IceData.tickRate;
            }
        }

        private void ApplyAuraTick()
        {
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, hitBuffer, IceData.targetLayer);
            
            for (int i = 0; i < targetsFound; i++)
            {
                var col = hitBuffer[i];
                if (col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    damageable.TakeDamage((int)currentDamage);
                }
                
                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(currentSlowFactor, currentSlowDuration);
                }
            }
        }
    }
}
