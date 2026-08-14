using System.Collections.Generic;
using UnityEngine;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class OrbitalCompanionAbility : ActiveAbility
    {
        private OrbitalCompanionAbilityData OrbitalData => Data as OrbitalCompanionAbilityData;
        private List<Transform> instances = new List<Transform>();
        private float currentAngle = 0f;

        private float tickRate = 0.5f; 
        private Dictionary<EntityId, float> enemyDamageCooldowns = new Dictionary<EntityId, float>();
        private float nextCleanupTime = 0f;
        private List<EntityId> keysToRemove = new List<EntityId>();

        // Modifiable stats
        private int currentCount;
        private float currentDamage;
        private float currentSpeed;
        private float currentRadius;
        private float currentArea;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            
            currentCount = OrbitalData.count;
            currentDamage = OrbitalData.baseDamage;
            currentSpeed = OrbitalData.orbitSpeed;
            currentRadius = OrbitalData.orbitDistance;
            currentArea = OrbitalData.baseArea;

            SpawnInstances();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Count:
                    currentCount += (int)value;
                    SpawnInstances();
                    break;
                case ModifierType.Speed:
                    currentSpeed += value;
                    break;
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Radius:
                    currentRadius += value;
                    break;
                case ModifierType.Area:
                    currentArea += value;
                    break;
            }
        }

        private Collider[] hitBuffer = new Collider[32]; // Optimization buffer

        private void SpawnInstances()
        {
            // If we have more instances than we need, destroy the excess
            while (instances.Count > currentCount)
            {
                var inst = instances[instances.Count - 1];
                if (inst != null) Destroy(inst.gameObject);
                instances.RemoveAt(instances.Count - 1);
            }

            // If we need more instances, spawn the difference
            for (int i = instances.Count; i < currentCount; i++)
            {
                GameObject obj;
                if (OrbitalData.prefab != null)
                {
                    obj = Instantiate(OrbitalData.prefab, transform);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    obj.transform.SetParent(transform);
                    obj.transform.localScale = Vector3.one * OrbitalData.baseArea;
                }
                obj.name = $"Orbital_{i}";

                // Note: Realistically, you want a Rigidbody/Collider on the prefab itself, 
                // but we add a trigger if not present for the primitive.
                var col = obj.GetComponent<Collider>();
                if (col == null) col = obj.AddComponent<SphereCollider>();
                col.isTrigger = true;
                
                var rb = obj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = obj.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                }

                instances.Add(obj.transform);
            }
            RecalculatePositions();
        }

        private void Update()
        {
            if (Data == null) return;
            currentAngle += currentSpeed * Time.deltaTime;
            currentAngle %= 360f;
            RecalculatePositions();

            ApplyDamageTick();
            CleanupCooldowns();
        }

        private void RecalculatePositions()
        {
            if (instances.Count == 0) return;
            float angleStep = 360f / instances.Count;
            for (int i = 0; i < instances.Count; i++)
            {
                float angle = currentAngle + (i * angleStep);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * currentRadius;
                instances[i].position = transform.position + offset;
                if (offset != Vector3.zero)
                {
                    instances[i].rotation = Quaternion.LookRotation(offset); // Поворот наружу (полезно для мечей)
                }
            }
        }

        private void ApplyDamageTick()
        {
            float hitRadius = currentArea / 2f;
            float currentTime = Time.time;

            foreach (var inst in instances)
            {
                int count = Physics.OverlapSphereNonAlloc(inst.position, hitRadius, hitBuffer, ActiveData.targetLayer);
                for (int i = 0; i < count; i++)
                {
                    var col = hitBuffer[i];
                    var enemy = col.GetComponent<EnemyBase>();
                    if (enemy == null || enemy.IsDead) continue;

                    EntityId enemyId = col.gameObject.GetEntityId();

                    if (!enemyDamageCooldowns.TryGetValue(enemyId, out float nextDamageTime) || currentTime >= nextDamageTime)
                    {
                        enemy.TakeDamage((int)currentDamage);
                        enemyDamageCooldowns[enemyId] = currentTime + tickRate;
                    }
                }
            }
        }

        private void CleanupCooldowns()
        {
            if (Time.time < nextCleanupTime) return;
            nextCleanupTime = Time.time + 5f;

            keysToRemove.Clear();
            float currentTime = Time.time;
            foreach (var kvp in enemyDamageCooldowns)
            {
                if (currentTime >= kvp.Value + 1f)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                enemyDamageCooldowns.Remove(key);
            }
        }
    }
}
