using UnityEngine;
using ProjectB.Combat;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class BlackHoleAbility : ActiveAbility
    {
        public const float VISUAL_Y_OFFSET = 0.06f;

        private BlackHoleAbilityData HoleData => Data as BlackHoleAbilityData;

        private const float SCAN_INTERVAL = 0.1f;

        private static int cachedObstacleMask = -1;

        private float currentDamage;
        private float currentRadius;
        private float currentPullForce;
        
        private float nextTickTime;
        private float nextScanTime;
        private Collider[] hitBuffer = new Collider[128];
        private readonly System.Collections.Generic.List<EnemyBase> capturedEnemies = new System.Collections.Generic.List<EnemyBase>(128);
        private GameObject visualHole;
        private bool usesCustomVisualPrefab;

        public float CurrentRadius => currentRadius;
        public Vector3 Center => transform.position;
        public Transform VisualTransform => visualHole != null ? visualHole.transform : null;

        public override void SimulateFrameTick()
        {
            Update();
        }

        private static int ObstacleMask
        {
            get
            {
                if (cachedObstacleMask == -1)
                {
                    cachedObstacleMask = LayerMask.GetMask("Obstacles");
                }
                return cachedObstacleMask;
            }
        }

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
            if (HoleData != null && HoleData.visualPrefab != null)
            {
                visualHole = Instantiate(HoleData.visualPrefab, transform, false);
                visualHole.name = "BlackHole_Visual";
                usesCustomVisualPrefab = true;
            }
            else
            {
                visualHole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visualHole.name = "BlackHole_Visual";
                visualHole.transform.SetParent(transform, false);
                usesCustomVisualPrefab = false;

                var col = visualHole.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                var renderer = visualHole.GetComponent<Renderer>();
                if (renderer != null && HoleData != null && HoleData.auraMaterial != null)
                {
                    renderer.sharedMaterial = HoleData.auraMaterial;
                }
            }

            var renderers = visualHole.GetComponentsInChildren<Renderer>(true);
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
            if (visualHole == null) return;
            visualHole.transform.localPosition = new Vector3(0f, VISUAL_Y_OFFSET - transform.position.y, 0f);
        }

        private void UpdateVisualSize()
        {
            if (visualHole != null)
            {
                visualHole.transform.localScale = usesCustomVisualPrefab
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
                case ModifierType.Speed: currentPullForce += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentRadius *= 1.2f;
                currentPullForce *= 1.2f;
                UpdateVisualSize();
            }
            else if (tier == 2)
            {
                currentRadius *= 1.15f;
                currentPullForce *= 1.2f;
                UpdateVisualSize();
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
                nextTickTime = Time.time + GetEffectiveCooldown(HoleData.tickRate, 0.15f);
            }

            if (visualHole != null)
            {
                UpdateVisualVerticalOffset();
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
            Vector3 center = transform.position;
            float minDist = HoleData != null ? Mathf.Max(0.5f, HoleData.minPullDistance) : 2.5f;
            int obstacleMask = ObstacleMask;

            for (int i = 0; i < capturedEnemies.Count; i++)
            {
                EnemyBase enemy = capturedEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;

                Vector3 enemyPos = enemy.transform.position;
                Vector3 toCenter = center - enemyPos;
                toCenter.y = 0f;
                float dist = toCenter.magnitude;

                if (dist > minDist)
                {
                    Vector3 pullDir = toCenter / dist;
                    float pullStep = Mathf.Min(currentPullForce * dt, dist - minDist);

                    // Не протаскиваем врагов сквозь стены и препятствия
                    if (pullStep > 0.0001f &&
                        (obstacleMask == 0 || !Physics.SphereCast(enemyPos + Vector3.up * 0.5f, 0.35f, pullDir, out _, pullStep + 0.2f, obstacleMask, QueryTriggerInteraction.Ignore)))
                    {
                        enemy.ApplyPull(pullDir * pullStep);
                    }
                }

                if (applyDamage)
                {
                    int dmg = RollScaledDamage(currentDamage, out bool isCrit);
                    DealDamage(enemy, dmg, isCrit);
                }
            }
        }
    }
}
