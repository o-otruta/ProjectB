using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Boss 2: Necromancer — teleports around the hero, fires fan volleys of projectiles, and summons minions.
    /// </summary>
    public class NecromancerBoss : BossBase
    {
        private float nextVolleyTime;
        private float nextTeleportTime;

        public override void InitializeBoss(
            BossData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            EnemyProjectilePool projectilePool,
            System.Action<EnemyData, Vector3> onSummonMinion,
            System.Func<Vector3, float, float, Vector3> safePositionProvider,
            float difficultyMultiplier = 1f)
        {
            base.InitializeBoss(data, heroTarget, enemyPool, bus, projectilePool, onSummonMinion, safePositionProvider, difficultyMultiplier);

            nextVolleyTime = Time.time + (data != null ? data.volleyCooldown * 0.5f : 1.5f);
            nextTeleportTime = Time.time + (data != null ? data.teleportCooldown : 7f);
        }

        protected override void UpdateMovement()
        {
            if (bossData == null)
            {
                base.UpdateMovement();
                return;
            }

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            float stopDist = bossData.stopDistance;

            if (direction.sqrMagnitude > stopDist * stopDist)
            {
                base.UpdateMovement();
            }
            else if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }

        protected override void UpdateAttack()
        {
            base.UpdateAttack();

            if (bossData == null || target == null) return;

            if (Time.time >= nextTeleportTime)
            {
                PerformTeleport();
                nextTeleportTime = Time.time + Mathf.Max(2.5f, bossData.teleportCooldown);
            }

            if (Time.time >= nextVolleyTime)
            {
                FireVolley();
                nextVolleyTime = Time.time + Mathf.Max(1f, bossData.volleyCooldown);
            }
        }

        private void PerformTeleport()
        {
            if (target == null) return;

            Vector3 dest;
            if (getSafePositionNearTarget != null)
            {
                dest = getSafePositionNearTarget.Invoke(target.position, bossData.teleportMinDistance, bossData.teleportMaxDistance);
            }
            else
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(bossData.teleportMinDistance, bossData.teleportMaxDistance);
                dest = target.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                dest.y = 0f;
            }

            Teleport(dest);
        }

        private void FireVolley()
        {
            if (sharedProjectilePool == null || bossData.volleyWeaponData == null || target == null) return;

            Vector3 baseDir = target.position - transform.position;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude > 0.001f)
            {
                baseDir.Normalize();
            }
            else
            {
                baseDir = transform.forward;
            }

            int count = Mathf.Max(1, bossData.volleyProjectileCount);
            float spread = bossData.volleySpreadAngle;
            float startAngle = count > 1 ? -spread * 0.5f : 0f;
            float step = count > 1 ? spread / (count - 1) : 0f;

            float shootHeight = target.position.y;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(bossData.volleyWeaponData.damage * currentDamageMultiplier));

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                Vector3 spawnPos = new Vector3(transform.position.x, shootHeight, transform.position.z) + dir * 0.8f;

                sharedProjectilePool.Spawn(
                    bossData.volleyWeaponData,
                    spawnPos,
                    dir,
                    finalDamage,
                    target,
                    targetDamageable);
            }
        }
    }
}
