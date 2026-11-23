using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Armored ShieldBearer orc that blocks 65% of non-AoE damage arriving within its frontal +/-60 degree arc,
    /// rewarding flanking, orbitals, boomerangs, and AoE explosions/spells.
    /// </summary>
    public class ShieldedEnemy : EnemyBase
    {
        private ShieldedEnemyData shieldData;

        public float FrontalBlockHalfAngle => shieldData != null ? shieldData.frontalBlockHalfAngle : 60f;
        public float FrontalDamageReduction => shieldData != null ? Mathf.Clamp(shieldData.frontalDamageReduction, 0f, 0.95f) : 0.65f;

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);
            shieldData = data as ShieldedEnemyData;
        }

        public bool IsAttackFromFront(Vector3 sourcePosition)
        {
            Vector3 toSource = sourcePosition - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude < 0.0001f) return false;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            float halfAngle = Mathf.Clamp(FrontalBlockHalfAngle, 1f, 179f);
            float cosThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            float dot = Vector3.Dot(forward.normalized, toSource.normalized);
            return dot >= cosThreshold - 0.0001f;
        }

        public int CalculateMitigatedDamage(int rawAmount, Vector3 sourcePosition, bool isAoE, out bool wasBlocked)
        {
            if (rawAmount <= 0 || isAoE)
            {
                wasBlocked = false;
                return rawAmount;
            }

            if (IsAttackFromFront(sourcePosition))
            {
                wasBlocked = true;
                float mult = 1f - FrontalDamageReduction;
                return Mathf.Max(1, Mathf.RoundToInt(rawAmount * mult));
            }

            wasBlocked = false;
            return rawAmount;
        }

        protected override int ModifyIncomingDamage(int rawAmount, bool hasSourcePosition, Vector3 sourcePosition, bool isAoE, out bool wasBlocked)
        {
            if (isAoE)
            {
                wasBlocked = false;
                return rawAmount;
            }

            if (!hasSourcePosition)
            {
                if (target == null)
                {
                    wasBlocked = false;
                    return rawAmount;
                }
                sourcePosition = target.position;
            }

            return CalculateMitigatedDamage(rawAmount, sourcePosition, isAoE, out wasBlocked);
        }
    }
}
