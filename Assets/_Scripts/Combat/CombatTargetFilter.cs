using UnityEngine;
using ProjectB.Arena;
using ProjectB.Player;

namespace ProjectB.Combat
{
    /// <summary>
    /// Filters auto-aim targets so weapons and ranged skills ignore isolated ExplosiveBarrels
    /// unless an alive enemy is within detonation range (3.5m).
    /// </summary>
    public static class CombatTargetFilter
    {
        public const float BARREL_ENEMY_PROXIMITY_RADIUS = 3.5f;
        private static readonly Collider[] ProximityBuffer = new Collider[32];

        public static bool IsValidAutoAimTarget(
            Collider candidate,
            IDamageable damageable,
            int enemyLayerMask,
            float barrelProximityRadius = BARREL_ENEMY_PROXIMITY_RADIUS)
        {
            if (candidate == null || damageable == null || damageable.IsDead)
            {
                return false;
            }

            if (damageable is ExplosiveBarrel barrel)
            {
                return HasAliveEnemyNearBarrel(barrel, enemyLayerMask, barrelProximityRadius);
            }

            return true;
        }

        public static bool HasAliveEnemyNearBarrel(
            ExplosiveBarrel barrel,
            int enemyLayerMask,
            float proximityRadius = BARREL_ENEMY_PROXIMITY_RADIUS)
        {
            if (barrel == null || barrel.IsDead || proximityRadius <= 0f)
            {
                return false;
            }

            int mask = enemyLayerMask != 0 ? enemyLayerMask : ResolveDefaultEnemyMask();
            int hits = Physics.OverlapSphereNonAlloc(
                barrel.transform.position,
                proximityRadius,
                ProximityBuffer,
                mask);

            for (int i = 0; i < hits; i++)
            {
                Collider col = ProximityBuffer[i];
                if (col == null) continue;

                if (!col.TryGetComponent<IDamageable>(out var nearbyDamageable))
                {
                    nearbyDamageable = col.GetComponentInParent<IDamageable>();
                }

                if (nearbyDamageable == null || nearbyDamageable.IsDead) continue;
                if (nearbyDamageable is ExplosiveBarrel || nearbyDamageable is HeroHealth) continue;

                return true;
            }

            return false;
        }

        private static int ResolveDefaultEnemyMask()
        {
            int mask = LayerMask.GetMask("Enemy", "Enemies");
            return mask != 0 ? mask : (1 << 8);
        }
    }
}
