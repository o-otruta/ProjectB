using UnityEngine;
using ProjectB.Combat;
using ProjectB.Visuals;
using UnityEngine.Pool;

namespace ProjectB.Abilities
{
    public class LaserTurret : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Transform firePoint;
        [SerializeField] private VoxelMeshAnimator meshAnimator;

        [Header("Aiming Settings")]
        [SerializeField] private float rotationSpeed = 720f;

        private float searchRadius;
        private float dps;
        private float lifetime;
        private LayerMask enemyLayer;
        private IObjectPool<LaserTurret> pool;

        private float spawnTime;
        private Transform currentTarget;
        private Collider[] hitBuffer = new Collider[50];

        public void Initialize(float searchRadius, float dps, float lifetime, LayerMask enemyLayer, IObjectPool<LaserTurret> pool)
        {
            this.searchRadius = searchRadius;
            this.dps = dps;
            this.lifetime = lifetime;
            this.enemyLayer = enemyLayer;
            this.pool = pool;

            spawnTime = Time.time;
            currentTarget = null;

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                    lineRenderer.startWidth = 0.1f;
                    lineRenderer.endWidth = 0.1f;
                    // Fallback procedural material if not assigned via prefab
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = Color.cyan;
                    mat.SetFloat("_Surface", 1); 
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    lineRenderer.material = mat;
                }
            }
            lineRenderer.enabled = false;

            if (meshAnimator == null)
            {
                meshAnimator = GetComponentInChildren<VoxelMeshAnimator>();
            }
            meshAnimator?.Stop();
        }

        private void OnDisable()
        {
            currentTarget = null;
            accumulatedDamage = 0f;
            if (lineRenderer != null) lineRenderer.enabled = false;
            meshAnimator?.Stop();
        }

        private void Update()
        {
            if (Time.time >= spawnTime + lifetime)
            {
                pool.Release(this);
                return;
            }

            FindTarget();

            if (currentTarget != null)
            {
                Vector3 targetPos = currentTarget.position;
                Vector3 targetDir = targetPos - transform.position;
                targetDir.y = 0;
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }

                lineRenderer.enabled = true;
                Vector3 origin = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 0.5f);
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, targetPos + Vector3.up * 0.5f);

                meshAnimator?.Play();

                if (currentTarget.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    accumulatedDamage += dps * Time.deltaTime;
                    if (accumulatedDamage >= 1f)
                    {
                        int dmg = Mathf.FloorToInt(accumulatedDamage);
                        accumulatedDamage -= dmg;
                        damageable.TakeDamage(dmg);
                    }
                }
                else
                {
                    currentTarget = null;
                }
            }
            else
            {
                lineRenderer.enabled = false;
                accumulatedDamage = 0f;
                meshAnimator?.Stop();
            }
        }

        private float accumulatedDamage = 0f;

        private void FindTarget()
        {
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                if ((currentTarget.position - transform.position).sqrMagnitude <= searchRadius * searchRadius)
                {
                    if (currentTarget.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                    {
                        return; 
                    }
                }
            }

            currentTarget = null;

            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, enemyLayer);
            if (targetsFound == 0) return;

            float minSqrDist = float.MaxValue;

            for (int i = 0; i < targetsFound; i++)
            {
                var target = hitBuffer[i].transform;
                float sqrDist = (target.position - transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    currentTarget = target;
                }
            }
        }
    }
}
