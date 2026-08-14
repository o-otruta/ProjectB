using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class FireballProjectile : AbilityProjectile
    {
        private float explosionRadius;
        private Collider[] explosionHits = new Collider[20];

        public void InitializeFireball(float damage, float speed, float explosionRadius, Transform target, LayerMask enemyLayer, UnityEngine.Pool.IObjectPool<AbilityProjectile> pool)
        {
            base.Initialize(damage, speed, target, enemyLayer, pool);
            this.explosionRadius = explosionRadius;
        }

        protected override void HitTarget()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionHits, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                if (explosionHits[i].TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    damageable.TakeDamage((int)damage);
                }
            }

            ReturnToPool();
        }
    }
}
