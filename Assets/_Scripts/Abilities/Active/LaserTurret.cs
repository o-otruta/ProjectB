using UnityEngine;
using ProjectB.Combat;
using UnityEngine.Pool;

namespace ProjectB.Abilities
{
    public class LaserTurret : MonoBehaviour
    {
        private float searchRadius;
        private float dps;
        private float lifetime;
        private LayerMask enemyLayer;
        private IObjectPool<LaserTurret> pool;

        private float spawnTime;
        private Transform currentTarget;
        private Collider[] hitBuffer = new Collider[50];
        private LineRenderer lineRenderer;

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
                    // TODO(Post-MVP): Убрать Shader.Find после добавления префабов
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
        }

        private void OnDisable()
        {
            currentTarget = null;
            accumulatedDamage = 0f;
            if (lineRenderer != null) lineRenderer.enabled = false;
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
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, transform.position + Vector3.up * 0.5f);
                lineRenderer.SetPosition(1, currentTarget.position + Vector3.up * 0.5f);

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
