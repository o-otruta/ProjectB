using UnityEngine;
using ProjectB.Combat;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class BlackHoleAbility : ActiveAbility
    {
        private BlackHoleAbilityData HoleData => Data as BlackHoleAbilityData;

        private float currentDamage;
        private float currentRadius;
        private float currentPullForce;
        
        private float nextTickTime;
        private Collider[] hitBuffer = new Collider[100];
        private GameObject visualHole;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            
            currentDamage = HoleData.baseDamage;
            currentRadius = HoleData.pullRadius;
            currentPullForce = HoleData.pullForce;

            CreateVisual();
        }

        private void CreateVisual()
        {
            visualHole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualHole.name = "BlackHole_Visual";
            visualHole.transform.SetParent(transform);
            visualHole.transform.localPosition = Vector3.zero;
            
            UpdateVisualSize();

            Destroy(visualHole.GetComponent<CapsuleCollider>());
            
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0f, 0f, 0f, 0.8f);
            
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            
            visualHole.GetComponent<Renderer>().material = mat;
        }

        private void UpdateVisualSize()
        {
            if (visualHole != null)
            {
                visualHole.transform.localScale = new Vector3(currentRadius * 2f, 0.01f, currentRadius * 2f);
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
                case ModifierType.Speed: currentPullForce += value; break;
            }
        }

        private void Update()
        {
            if (Data == null) return;

            bool applyDamage = Time.time >= nextTickTime;
            ApplyPullAndDamage(applyDamage);

            if (applyDamage)
            {
                nextTickTime = Time.time + HoleData.tickRate;
            }

            if (visualHole != null)
            {
                visualHole.transform.Rotate(Vector3.up * 45f * Time.deltaTime);
            }
        }

        private void ApplyPullAndDamage(bool applyDamage)
        {
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, hitBuffer, HoleData.targetLayer);
            
            for (int i = 0; i < targetsFound; i++)
            {
                var col = hitBuffer[i];
                if (col.TryGetComponent<EnemyBase>(out var enemy))
                {
                    Vector3 direction = (transform.position - enemy.transform.position).normalized;
                    direction.y = 0;
                    Vector3 pullDelta = direction * currentPullForce * Time.deltaTime;
                    enemy.ApplyPull(pullDelta);
                }
                
                if (applyDamage && col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    damageable.TakeDamage((int)currentDamage);
                }
            }
        }
    }
}
