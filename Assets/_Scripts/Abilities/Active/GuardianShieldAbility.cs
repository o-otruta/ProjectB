using System.Collections.Generic;
using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class GuardianShieldAbility : ActiveAbility
    {
        private GuardianShieldAbilityData ShieldData => Data as GuardianShieldAbilityData;

        private const float SCAN_INTERVAL = 0.08f;
        private const float PHALANX_WAVE_RADIUS = 2.4f;
        private const float PHALANX_COOLDOWN = 0.15f;
        private static int cachedObstacleMask = -1;

        private readonly List<Transform> shields = new List<Transform>();
        private readonly Dictionary<EntityId, float> enemyHitCooldowns = new Dictionary<EntityId, float>(128);
        private readonly List<EntityId> keysToRemove = new List<EntityId>(128);
        private readonly Collider[] hitBuffer = new Collider[64];
        private readonly Collider[] phalanxHitBuffer = new Collider[48];

        private int currentCount;
        private float currentDamage;
        private float currentOrbitSpeed;
        private float currentOrbitRadius;
        private float currentKnockback;

        private float currentAngle;
        private float nextScanTime;
        private float nextCleanupTime;
        private float nextPhalanxTime;

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

            if (ShieldData != null)
            {
                currentCount = Mathf.Max(1, ShieldData.shieldCount);
                currentDamage = ShieldData.baseDamage;
                currentOrbitSpeed = ShieldData.orbitSpeed;
                currentOrbitRadius = ShieldData.orbitRadius;
                currentKnockback = ShieldData.knockbackDistance;
            }

            SpawnShields();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Count:
                    currentCount = Mathf.Max(1, currentCount + Mathf.RoundToInt(value));
                    SpawnShields();
                    break;
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Area:
                    currentKnockback += value;
                    break;
                case ModifierType.Speed:
                    currentOrbitSpeed += value;
                    break;
                case ModifierType.Radius:
                    currentOrbitRadius += value;
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentOrbitSpeed *= 1.2f;
                currentKnockback *= 1.2f;
                UpdateShieldScales();
            }
            else if (tier == 2)
            {
                currentCount += 1;
                SpawnShields();
                UpdateShieldScales();
            }
        }

        private void SpawnShields()
        {
            while (shields.Count > currentCount)
            {
                Transform last = shields[shields.Count - 1];
                if (last != null) SafeDestroy(last.gameObject);
                shields.RemoveAt(shields.Count - 1);
            }

            for (int i = shields.Count; i < currentCount; i++)
            {
                GameObject obj;
                if (ShieldData != null && ShieldData.shieldPrefab != null)
                {
                    obj = Instantiate(ShieldData.shieldPrefab, transform);
                }
                else
                {
                    obj = CreateVoxelShieldVisual(ShieldData != null ? ShieldData.shieldMaterial : null);
                    obj.transform.SetParent(transform, false);
                }

                obj.name = $"GuardianShield_{i}";
                obj.transform.localScale = Vector3.one * VisualScaleMultiplier;
                shields.Add(obj.transform);
            }

            RecalculatePositions();
        }

        private void UpdateShieldScales()
        {
            Vector3 scale = Vector3.one * VisualScaleMultiplier;
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i] != null)
                {
                    shields[i].localScale = scale;
                }
            }
        }

        private static GameObject CreateVoxelShieldVisual(Material shieldMat)
        {
            var root = new GameObject("VoxelShield");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.95f, 0.16f);
            SafeDestroy(body.GetComponent<Collider>());
            var bodyMr = body.GetComponent<MeshRenderer>();
            if (bodyMr != null && shieldMat != null) bodyMr.sharedMaterial = shieldMat;

            var boss = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boss.transform.SetParent(root.transform, false);
            boss.transform.localPosition = new Vector3(0f, 0.72f, 0.08f);
            boss.transform.localScale = new Vector3(0.3f, 0.35f, 0.12f);
            SafeDestroy(boss.GetComponent<Collider>());
            var bossMr = boss.GetComponent<MeshRenderer>();
            if (bossMr != null && shieldMat != null) bossMr.sharedMaterial = shieldMat;

            return root;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            currentAngle = (currentAngle + currentOrbitSpeed * Time.deltaTime) % 360f;
            RecalculatePositions();

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + SCAN_INTERVAL;
                ApplyShieldScan();
            }

            CleanupCooldowns();
        }

        private void RecalculatePositions()
        {
            if (shields.Count == 0) return;

            float angleStep = 360f / shields.Count;
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i] == null) continue;
                float angle = currentAngle + i * angleStep;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * currentOrbitRadius;
                shields[i].position = transform.position + offset;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    shields[i].rotation = Quaternion.LookRotation(offset);
                }
            }
        }

        public void ApplyShieldScan()
        {
            if (ShieldData == null) return;

            float hitRadius = Mathf.Max(0.3f, ShieldData.shieldHitRadius * VisualScaleMultiplier);
            float cooldown = GetEffectiveCooldown(ShieldData.hitCooldownPerEnemy, 0.15f);
            float now = Time.time;
            int obstacleMask = ObstacleMask;
            bool hasAegisPhalanx = HasSynergy(SynergyIds.AegisPhalanx);

            for (int s = 0; s < shields.Count; s++)
            {
                Transform shield = shields[s];
                if (shield == null) continue;

                int count = Physics.OverlapSphereNonAlloc(shield.position, hitRadius, hitBuffer, ShieldData.targetLayer);
                for (int i = 0; i < count; i++)
                {
                    Collider col = hitBuffer[i];
                    if (col == null) continue;

                    if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                    EntityId enemyId = col.gameObject.GetEntityId();
                    if (enemyHitCooldowns.TryGetValue(enemyId, out float nextTime) && now < nextTime) continue;

                    int dmg = RollScaledDamage(currentDamage, out bool isCrit);
                    DealDamage(damageable, dmg, isCrit, sourcePosition: shield.position, isAoE: false);
                    enemyHitCooldowns[enemyId] = now + cooldown;

                    if (col.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead && currentKnockback > 0f)
                    {
                        Vector3 pushDir = enemy.transform.position - transform.position;
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude < 0.001f)
                        {
                            pushDir = shield.position - transform.position;
                            pushDir.y = 0f;
                        }

                        if (pushDir.sqrMagnitude > 0.0001f)
                        {
                            pushDir.Normalize();
                            float safeDist = currentKnockback;
                            if (obstacleMask != 0 && Physics.SphereCast(
                                enemy.transform.position + Vector3.up * 0.5f,
                                0.35f,
                                pushDir,
                                out RaycastHit hit,
                                currentKnockback + 0.2f,
                                obstacleMask,
                                QueryTriggerInteraction.Ignore))
                            {
                                safeDist = Mathf.Max(0f, hit.distance - 0.25f);
                            }

                            if (safeDist > 0.01f)
                            {
                                enemy.ApplyPull(pushDir * safeDist);

                                if (hasAegisPhalanx && now >= nextPhalanxTime)
                                {
                                    nextPhalanxTime = now + PHALANX_COOLDOWN;
                                    TriggerAegisPhalanxWave(shield.position);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void TriggerAegisPhalanxWave(Vector3 shieldPosition)
        {
            Vector3 waveCenter = shieldPosition;
            OrbitalCompanionAbility orbital = heroAbilities != null
                ? heroAbilities.GetActiveAbility<OrbitalCompanionAbility>()
                : null;
            if (orbital != null && orbital.TryGetClosestInstancePosition(shieldPosition, out Vector3 swordPosition))
            {
                waveCenter = (shieldPosition + swordPosition) * 0.5f;
            }

            int hits = Physics.OverlapSphereNonAlloc(waveCenter, PHALANX_WAVE_RADIUS, phalanxHitBuffer, ShieldData.targetLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = phalanxHitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var target) || target.IsDead) continue;

                int waveDmg = RollScaledDamage(currentDamage * 1.35f, out bool isCrit);
                DealDamage(target, waveDmg, isCrit, SynergyIds.AegisPhalanx, isAoE: true, sourcePosition: waveCenter);
            }

            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.AegisPhalanxWave, waveCenter, PHALANX_WAVE_RADIUS));
        }

        private void CleanupCooldowns()
        {
            if (Time.time < nextCleanupTime) return;
            nextCleanupTime = Time.time + 5f;

            keysToRemove.Clear();
            float now = Time.time;
            foreach (var kvp in enemyHitCooldowns)
            {
                if (now >= kvp.Value + 1f)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                enemyHitCooldowns.Remove(keysToRemove[i]);
            }
        }
    }
}
