using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Zero-GC spatial hash grid separation system that prevents enemies from stacking on the same point.
    /// </summary>
    public sealed class EnemySeparationSystem
    {
        public const float DEFAULT_CELL_SIZE = 2.0f;
        public const float DEFAULT_SEPARATION_RADIUS = 0.85f;

        private const int TABLE_SIZE = 1024;
        private const int TABLE_MASK = TABLE_SIZE - 1;
        private const int MAX_ENEMIES = 512;

        private readonly int[] cellHeads = new int[TABLE_SIZE];
        private readonly int[] nextInCell = new int[MAX_ENEMIES];
        private readonly int[] usedCells = new int[MAX_ENEMIES];
        private readonly EnemyBase[] entries = new EnemyBase[MAX_ENEMIES];
        private readonly Vector3[] positions = new Vector3[MAX_ENEMIES];
        private readonly Vector2[] pushOffsets = new Vector2[MAX_ENEMIES];
        private readonly int[] cellX = new int[MAX_ENEMIES];
        private readonly int[] cellZ = new int[MAX_ENEMIES];

        public EnemySeparationSystem()
        {
            for (int i = 0; i < TABLE_SIZE; i++)
            {
                cellHeads[i] = -1;
            }
        }

        public int Step(
            HashSet<EnemyBase> activeEnemies,
            int obstacleMask,
            float maxArenaCoord = 0f,
            float separationRadius = DEFAULT_SEPARATION_RADIUS,
            float separationStrength = 0.45f,
            float maxPushPerStep = 0.4f)
        {
            if (activeEnemies == null || activeEnemies.Count < 2 || separationRadius <= 0f)
            {
                return 0;
            }

            int count = 0;
            foreach (EnemyBase enemy in activeEnemies)
            {
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
                if (count >= MAX_ENEMIES) break;

                entries[count] = enemy;
                positions[count] = enemy.transform.position;
                pushOffsets[count] = Vector2.zero;
                count++;
            }

            return SolveAndApply(count, obstacleMask, maxArenaCoord, separationRadius, separationStrength, maxPushPerStep);
        }

        public int Step(
            List<EnemyBase> activeEnemies,
            int obstacleMask,
            float maxArenaCoord = 0f,
            float separationRadius = DEFAULT_SEPARATION_RADIUS,
            float separationStrength = 0.45f,
            float maxPushPerStep = 0.4f)
        {
            if (activeEnemies == null || activeEnemies.Count < 2 || separationRadius <= 0f)
            {
                return 0;
            }

            int count = 0;
            int sourceCount = activeEnemies.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                EnemyBase enemy = activeEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
                if (count >= MAX_ENEMIES) break;

                entries[count] = enemy;
                positions[count] = enemy.transform.position;
                pushOffsets[count] = Vector2.zero;
                count++;
            }

            return SolveAndApply(count, obstacleMask, maxArenaCoord, separationRadius, separationStrength, maxPushPerStep);
        }

        private int SolveAndApply(
            int count,
            int obstacleMask,
            float maxArenaCoord,
            float separationRadius,
            float separationStrength,
            float maxPushPerStep)
        {
            if (count < 2)
            {
                for (int i = 0; i < count; i++)
                {
                    entries[i] = null;
                }
                return 0;
            }

            float invCellSize = 1f / DEFAULT_CELL_SIZE;
            int usedCellCount = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = positions[i];
                int cx = Mathf.FloorToInt(pos.x * invCellSize);
                int cz = Mathf.FloorToInt(pos.z * invCellSize);
                cellX[i] = cx;
                cellZ[i] = cz;

                int bucket = HashCell(cx, cz);
                if (cellHeads[bucket] == -1)
                {
                    usedCells[usedCellCount++] = bucket;
                }
                nextInCell[i] = cellHeads[bucket];
                cellHeads[bucket] = i;
            }

            float radiusSq = separationRadius * separationRadius;
            float halfStrength = 0.5f * Mathf.Clamp01(separationStrength);

            for (int i = 0; i < count; i++)
            {
                int cx = cellX[i];
                int cz = cellZ[i];
                Vector3 posI = positions[i];
                float pushX = 0f;
                float pushZ = 0f;

                for (int ox = -1; ox <= 1; ox++)
                {
                    int ncx = cx + ox;
                    for (int oz = -1; oz <= 1; oz++)
                    {
                        int ncz = cz + oz;
                        int bucket = HashCell(ncx, ncz);
                        int j = cellHeads[bucket];
                        while (j != -1)
                        {
                            if (j != i && cellX[j] == ncx && cellZ[j] == ncz)
                            {
                                Vector3 posJ = positions[j];
                                float dx = posI.x - posJ.x;
                                float dz = posI.z - posJ.z;
                                float distSq = dx * dx + dz * dz;

                                if (distSq < radiusSq)
                                {
                                    float dirX;
                                    float dirZ;
                                    float overlap;

                                    if (distSq < 0.00001f)
                                    {
                                        float angle = (i * 1.6180339f + j * 0.7548776f) * 6.2831853f;
                                        dirX = Mathf.Cos(angle);
                                        dirZ = Mathf.Sin(angle);
                                        overlap = separationRadius;
                                    }
                                    else
                                    {
                                        float dist = Mathf.Sqrt(distSq);
                                        float invDist = 1f / dist;
                                        dirX = dx * invDist;
                                        dirZ = dz * invDist;
                                        overlap = separationRadius - dist;
                                    }

                                    float impulse = overlap * halfStrength;
                                    pushX += dirX * impulse;
                                    pushZ += dirZ * impulse;
                                }
                            }
                            j = nextInCell[j];
                        }
                    }
                }

                pushOffsets[i] = new Vector2(pushX, pushZ);
            }

            int movedCount = 0;
            float maxPushSq = maxPushPerStep * maxPushPerStep;

            for (int i = 0; i < count; i++)
            {
                EnemyBase enemy = entries[i];
                entries[i] = null;

                if (enemy == null || enemy.IsDead) continue;
                if (enemy is ChargerEnemy charger &&
                    (charger.CurrentState == ChargerEnemy.ChargeState.Charging ||
                     charger.CurrentState == ChargerEnemy.ChargeState.WindingUp))
                {
                    continue;
                }

                Vector2 offset = pushOffsets[i];
                float resistanceScale = Mathf.Clamp(enemy.PullResistance, 0.25f, 1f);
                offset *= resistanceScale;

                float magSq = offset.sqrMagnitude;
                if (magSq < 0.000001f) continue;

                float mag = Mathf.Sqrt(magSq);
                Vector3 pushDir = new Vector3(offset.x / mag, 0f, offset.y / mag);
                if (magSq > maxPushSq)
                {
                    mag = maxPushPerStep;
                }

                Vector3 currentPos = positions[i];
                if (obstacleMask != 0)
                {
                    Vector3 castOrigin = new Vector3(currentPos.x, currentPos.y + 0.5f, currentPos.z);
                    if (Physics.SphereCast(
                        castOrigin,
                        0.35f,
                        pushDir,
                        out RaycastHit hit,
                        mag + 0.15f,
                        obstacleMask,
                        QueryTriggerInteraction.Ignore))
                    {
                        mag = Mathf.Max(0f, hit.distance - 0.15f);
                        if (mag <= 0.0001f) continue;
                    }
                }

                float nextX = currentPos.x + pushDir.x * mag;
                float nextZ = currentPos.z + pushDir.z * mag;
                if (maxArenaCoord > 0f)
                {
                    nextX = Mathf.Clamp(nextX, -maxArenaCoord, maxArenaCoord);
                    nextZ = Mathf.Clamp(nextZ, -maxArenaCoord, maxArenaCoord);
                }

                enemy.transform.position = new Vector3(nextX, currentPos.y, nextZ);
                movedCount++;
            }

            for (int i = 0; i < usedCellCount; i++)
            {
                cellHeads[usedCells[i]] = -1;
            }

            return movedCount;
        }

        private static int HashCell(int cx, int cz)
        {
            unchecked
            {
                return ((cx * 73856093) ^ (cz * 19349663)) & TABLE_MASK;
            }
        }
    }
}
