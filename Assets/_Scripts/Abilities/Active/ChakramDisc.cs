using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Pooled ricocheting voxel disc fired by <see cref="BuzzsawChakramAbility"/>.
    /// Bounces between enemies without hitting the same target twice. Zero-GC.
    /// </summary>
    public class ChakramDisc : MonoBehaviour
    {
        private const float MAX_LIFETIME = 6f;
        private const float SPIN_DEGREES_PER_SECOND = 900f;
        private const int MAX_HISTORY = 12;

        private BuzzsawChakramAbility owner;
        private IObjectPool<ChakramDisc> pool;
        private Transform target;
        private IDamageable targetDamageable;
        private readonly EntityId[] hitHistory = new EntityId[MAX_HISTORY];
        private int hitCount;
        private float speed;
        private float damage;
        private int remainingRicochets;
        private float expireTime;
        private bool isReturned;
        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;

        public bool IsMini { get; private set; }
        public int RemainingRicochets => remainingRicochets;
        public int HitCount => hitCount;
        public float Speed => speed;
        public float Damage => damage;
        public Transform Target => target;
        public bool IsActive => !isReturned;

        private void Awake()
        {
            CaptureBaseScale();
        }

        private void CaptureBaseScale()
        {
            if (baseScaleCaptured) return;
            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }

        public void Launch(
            BuzzsawChakramAbility owner,
            IObjectPool<ChakramDisc> pool,
            Vector3 position,
            Transform firstTarget,
            float damage,
            float speed,
            int ricochets,
            bool isMini,
            float visualScale)
        {
            CaptureBaseScale();
            this.owner = owner;
            this.pool = pool;
            this.damage = damage;
            this.speed = speed;
            remainingRicochets = Mathf.Max(0, ricochets);
            IsMini = isMini;
            hitCount = 0;
            isReturned = false;
            expireTime = Time.time + MAX_LIFETIME;
            transform.position = position;
            transform.localScale = baseScale * Mathf.Max(0.2f, visualScale);
            SetTarget(firstTarget);
        }

        public void ExcludeTarget(Transform t)
        {
            if (t == null || hitCount >= MAX_HISTORY) return;
            hitHistory[hitCount++] = t.gameObject.GetEntityId();
        }

        public bool HasHit(Transform t)
        {
            if (t == null) return false;
            EntityId id = t.gameObject.GetEntityId();
            for (int i = 0; i < hitCount; i++)
            {
                if (hitHistory[i] == id) return true;
            }
            return false;
        }

        public void BoostSpeed(float factor, float maxSpeed)
        {
            speed = Mathf.Min(maxSpeed, speed * Mathf.Max(1f, factor));
        }

        private void SetTarget(Transform t)
        {
            target = t;
            targetDamageable = t != null ? t.GetComponent<IDamageable>() : null;
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float dt)
        {
            if (isReturned || dt <= 0f) return;

            transform.Rotate(0f, SPIN_DEGREES_PER_SECOND * dt, 0f, Space.World);

            if (Time.time >= expireTime)
            {
                ReturnToPool();
                return;
            }

            if (target == null || !target.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
            {
                if (!Retarget())
                {
                    ReturnToPool();
                }
                return;
            }

            Vector3 targetPos = target.position;
            targetPos.y = transform.position.y;
            Vector3 toTarget = targetPos - transform.position;
            float step = speed * dt;
            float hitRadius = owner != null ? owner.HitRadius : 0.55f;

            if (toTarget.sqrMagnitude <= (step + hitRadius) * (step + hitRadius))
            {
                transform.position = targetPos;
                ResolveHit();
                return;
            }

            transform.position += toTarget.normalized * step;
        }

        /// <summary>Applies the hit on the current target and bounces (exposed for tests).</summary>
        public void ResolveHit()
        {
            if (isReturned) return;

            Transform hitTarget = target;
            ExcludeTarget(hitTarget);

            if (owner != null && targetDamageable != null && !targetDamageable.IsDead)
            {
                owner.HandleDiscHit(this, targetDamageable, transform.position);
            }

            if (isReturned) return;

            if (remainingRicochets <= 0 || !Retarget())
            {
                ReturnToPool();
                return;
            }

            remainingRicochets--;
        }

        private bool Retarget()
        {
            if (owner == null) return false;
            Transform next = owner.FindRicochetTarget(transform.position, this);
            if (next == null) return false;
            SetTarget(next);
            return true;
        }

        public void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;
            target = null;
            targetDamageable = null;

            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
