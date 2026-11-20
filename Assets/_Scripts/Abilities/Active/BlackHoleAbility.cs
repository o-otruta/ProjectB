using UnityEngine;
using ProjectB.Combat;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class BlackHoleAbility : ActiveAbility
    {
        private BlackHoleAbilityData HoleData => Data as BlackHoleAbilityData;

        private const float SCAN_INTERVAL = 0.1f;

        private float currentDamage;
        private float currentRadius;
        private float currentPullForce;
        
        private float nextTickTime;
        private float nextScanTime;
        private Collider[] hitBuffer = new Collider[100];
        private readonly System.Collections.Generic.List<EnemyBase> capturedEnemies = new System.Collections.Generic.List<EnemyBase>(64);
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

            var renderer = visualHole.GetComponent<Renderer>();
            if (renderer != null && HoleData.auraMaterial != null)
            {
                renderer.sharedMaterial = HoleData.auraMaterial;
            }
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
            if (Data == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + SCAN_INTERVAL;
                RefreshCapturedEnemies();
            }

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

        private void RefreshCapturedEnemies()
        {
            capturedEnemies.Clear();
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, hitBuffer, HoleData.targetLayer);
            for (int i = 0; i < targetsFound; i++)
            {
                var col = hitBuffer[i];
                if (col != null && col.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                {
                    capturedEnemies.Add(enemy);
                }
            }
        }

        private void ApplyPullAndDamage(bool applyDamage)
        {
            float dt = Time.deltaTime;
            int dmg = Mathf.RoundToInt(currentDamage);
            Vector3 center = transform.position;

            for (int i = 0; i < capturedEnemies.Count; i++)
            {
                EnemyBase enemy = capturedEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;

                Vector3 toCenter = center - enemy.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.001f)
                {
                    Vector3 pullDelta = toCenter.normalized * (currentPullForce * dt);
                    enemy.ApplyPull(pullDelta);
                }

                if (applyDamage)
                {
                    enemy.TakeDamage(dmg);
                }
            }
        }
    }
}
