using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class MiniDragonAbility : ActiveAbility
    {
        private MiniDragonAbilityData DragonData => Data as MiniDragonAbilityData;

        private readonly List<DragonFollower> followers = new List<DragonFollower>();
        private readonly Collider[] hitBuffer = new Collider[128];

        private float currentDamage;
        private float currentCooldown;
        private float currentSearchRadius;
        private int currentDragonCount;

        private float nextFireTime;
        private Transform dragonContainer;
        private IObjectPool<DragonFlameProjectile> flamePool;

        public int ActiveDragonCount => followers.Count;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            if (DragonData != null)
            {
                currentDamage = DragonData.baseDamage;
                currentCooldown = DragonData.baseCooldown;
                currentSearchRadius = DragonData.searchRadius;
                currentDragonCount = Mathf.Max(1, DragonData.dragonCount);
            }

            dragonContainer = new GameObject($"Dragons_{Data.id}").transform;
            dragonContainer.SetParent(null);

            InitializePool();
            SyncFollowers();
        }

        private void InitializePool()
        {
            flamePool = new ObjectPool<DragonFlameProjectile>(
                createFunc: CreateFlameInstance,
                actionOnGet: f => { if (f != null) f.gameObject.SetActive(true); },
                actionOnRelease: f => { if (f != null) f.gameObject.SetActive(false); },
                actionOnDestroy: f => { if (f != null) SafeDestroy(f.gameObject); },
                collectionCheck: true,
                defaultCapacity: 9,
                maxSize: 60);

            if (Application.isPlaying)
            {
                PrewarmPools(9);
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            if (flamePool == null) return;
            int targetCount = count > 0 ? count : 9;
            var temp = new DragonFlameProjectile[targetCount];
            for (int i = 0; i < targetCount; i++) temp[i] = flamePool.Get();
            for (int i = 0; i < targetCount; i++) if (temp[i] != null) flamePool.Release(temp[i]);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private DragonFlameProjectile CreateFlameInstance()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DragonFlame";
            cube.transform.SetParent(dragonContainer, false);
            cube.transform.localScale = new Vector3(0.28f, 0.28f, 0.42f);
            SafeDestroy(cube.GetComponent<Collider>());

            var mr = cube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (DragonData != null && DragonData.flameMaterial != null)
                {
                    mr.sharedMaterial = DragonData.flameMaterial;
                }
            }

            return cube.AddComponent<DragonFlameProjectile>();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Count:
                    currentDragonCount = Mathf.Max(1, currentDragonCount + Mathf.RoundToInt(value));
                    SyncFollowers();
                    break;
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Cooldown:
                    currentCooldown = Mathf.Max(0.3f, currentCooldown - value);
                    break;
                case ModifierType.Radius:
                    currentSearchRadius += value;
                    break;
            }
        }

        private void SyncFollowers()
        {
            if (dragonContainer == null) return;

            while (followers.Count > currentDragonCount)
            {
                DragonFollower last = followers[followers.Count - 1];
                if (last != null) SafeDestroy(last.gameObject);
                followers.RemoveAt(followers.Count - 1);
            }

            float followDist = DragonData != null ? DragonData.followDistance : 2f;
            float smooth = DragonData != null ? DragonData.followSmoothTime : 0.22f;
            Material mat = DragonData != null ? DragonData.dragonMaterial : null;

            for (int i = followers.Count; i < currentDragonCount; i++)
            {
                GameObject obj;
                if (DragonData != null && DragonData.followerPrefab != null)
                {
                    obj = Instantiate(DragonData.followerPrefab, dragonContainer);
                }
                else
                {
                    obj = new GameObject($"DragonWhelp_{i}");
                    obj.transform.SetParent(dragonContainer, false);
                }

                var follower = obj.GetComponent<DragonFollower>();
                if (follower == null) follower = obj.AddComponent<DragonFollower>();

                Vector3 offset = ComputeFollowOffset(i, currentDragonCount, followDist);
                follower.Initialize(transform, offset, smooth, i * 1.4f, mat);
                followers.Add(follower);
            }

            for (int i = 0; i < followers.Count; i++)
            {
                if (followers[i] != null)
                {
                    followers[i].SetOffset(ComputeFollowOffset(i, followers.Count, followDist));
                }
            }
        }

        private static Vector3 ComputeFollowOffset(int index, int totalCount, float distance)
        {
            if (totalCount <= 1)
            {
                return new Vector3(-distance * 0.7f, 0f, -distance * 0.7f);
            }

            float arcSpan = 140f;
            float startAngle = 180f - arcSpan * 0.5f;
            float angle = startAngle + (arcSpan / (totalCount - 1)) * index;
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * distance;
        }

        private void Update()
        {
            if (Data == null || flamePool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                FireAllDragons();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.3f);
            }
        }

        public int FireAllDragons()
        {
            if (DragonData == null || flamePool == null || followers.Count == 0) return 0;

            int totalFired = 0;
            for (int i = 0; i < followers.Count; i++)
            {
                DragonFollower dragon = followers[i];
                if (dragon == null) continue;

                Transform target = FindClosestTarget(dragon.transform.position, currentSearchRadius, DragonData.targetLayer);
                if (target == null)
                {
                    target = FindClosestTarget(transform.position, currentSearchRadius, DragonData.targetLayer);
                }
                if (target == null) continue;

                totalFired += dragon.FireCone(
                    target,
                    DragonData.coneProjectileCount,
                    DragonData.coneSpreadAngle,
                    DragonData.projectileSpeed,
                    GetScaledDamage(currentDamage),
                    DragonData.targetLayer,
                    runtimeStats,
                    flamePool);
            }

            return totalFired;
        }

        private Transform FindClosestTarget(Vector3 origin, float radius, LayerMask layer)
        {
            int found = Physics.OverlapSphereNonAlloc(origin, radius, hitBuffer, layer);
            if (found == 0) return null;

            Transform best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;

                float sqr = (col.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = col.transform;
                }
            }

            return best;
        }

        private void OnDestroy()
        {
            followers.Clear();
            flamePool?.Clear();
            if (dragonContainer != null)
            {
                SafeDestroy(dragonContainer.gameObject);
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
