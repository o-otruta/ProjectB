using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class DragonFollower : MonoBehaviour
    {
        private Transform heroTransform;
        private Vector3 followOffset;
        private float smoothTime;
        private float bobPhase;
        private Vector3 smoothVelocity;

        private Transform leftWing;
        private Transform rightWing;
        private Transform muzzlePoint;

        public void Initialize(
            Transform hero,
            Vector3 offset,
            float smoothTime,
            float phaseOffset,
            Material dragonMaterial)
        {
            heroTransform = hero;
            followOffset = offset;
            this.smoothTime = Mathf.Max(0.05f, smoothTime);
            bobPhase = phaseOffset;
            smoothVelocity = Vector3.zero;

            EnsureVisual(dragonMaterial);

            if (heroTransform != null)
            {
                transform.position = heroTransform.position + followOffset + Vector3.up * 1.35f;
            }
        }

        public void SetOffset(Vector3 offset)
        {
            followOffset = offset;
        }

        private void EnsureVisual(Material dragonMat)
        {
            if (muzzlePoint != null) return;

            if (transform.childCount > 0)
            {
                muzzlePoint = transform.Find("Muzzle");
                if (muzzlePoint == null) muzzlePoint = transform;
                return;
            }

            CreatePart("Body", Vector3.zero, new Vector3(0.45f, 0.34f, 0.65f), dragonMat);
            CreatePart("Head", new Vector3(0f, 0.14f, 0.42f), new Vector3(0.32f, 0.28f, 0.32f), dragonMat);
            CreatePart("Tail", new Vector3(0f, 0.04f, -0.45f), new Vector3(0.18f, 0.16f, 0.36f), dragonMat);

            leftWing = CreatePart("LeftWing", new Vector3(-0.34f, 0.12f, -0.04f), new Vector3(0.45f, 0.06f, 0.32f), dragonMat).transform;
            rightWing = CreatePart("RightWing", new Vector3(0.34f, 0.12f, -0.04f), new Vector3(0.45f, 0.06f, 0.32f), dragonMat).transform;

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0.12f, 0.62f);
            muzzlePoint = muzzleGo.transform;
        }

        private GameObject CreatePart(string partName, Vector3 localPos, Vector3 scale, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = partName;
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            var col = cube.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var mr = cube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null) mr.sharedMaterial = mat;
            }
            return cube;
        }

        private void Update()
        {
            if (heroTransform == null || Time.deltaTime <= 0f) return;

            float t = Time.time * 4.5f + bobPhase;
            float hoverY = 1.35f + Mathf.Sin(t) * 0.2f;
            Vector3 desiredPos = heroTransform.position + followOffset;
            desiredPos.y = heroTransform.position.y + hoverY;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref smoothVelocity, smoothTime);

            Vector3 moveLook = smoothVelocity;
            moveLook.y = 0f;
            if (moveLook.sqrMagnitude > 0.08f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveLook), Time.deltaTime * 10f);
            }

            float flapAngle = Mathf.Sin(t * 2.2f) * 28f;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
        }

        public int FireCone(
            Transform target,
            int projectileCount,
            float spreadAngle,
            float speed,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            IObjectPool<DragonFlameProjectile> flamePool,
            int pierceCount = 1,
            System.Action<Vector3, float> onFuryHit = null,
            ProjectB.Core.RunStatistics runStatistics = null,
            string sourceId = "MiniDragon",
            float visualScale = 1f)
        {
            if (target == null || flamePool == null) return 0;

            Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector3 baseDir = target.position - origin;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = transform.forward;
            baseDir.Normalize();

            transform.rotation = Quaternion.LookRotation(baseDir);

            int count = Mathf.Max(1, projectileCount);
            int fired = 0;

            for (int i = 0; i < count; i++)
            {
                DragonFlameProjectile flame = flamePool.Get();
                if (flame == null) continue;

                Vector3 dir = baseDir;
                if (count > 1)
                {
                    float step = spreadAngle / (count - 1);
                    float angle = -spreadAngle * 0.5f + step * i;
                    dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                }

                Vector3 spawnPos = new Vector3(origin.x, 0.75f, origin.z);
                flame.Initialize(
                    spawnPos,
                    dir,
                    speed,
                    damage,
                    enemyLayer,
                    runtimeStats,
                    flamePool,
                    pierceCount,
                    onFuryHit,
                    runStatistics,
                    sourceId,
                    visualScale);
                fired++;
            }

            return fired;
        }
    }
}
