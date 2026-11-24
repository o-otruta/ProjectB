using System;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using ProjectB.Combat;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Pooled ground frost zone spawned by Frost Wisp on death.
    /// Slows the hero while standing inside without allocating in Update.
    /// </summary>
    public class FrostSlowZone : MonoBehaviour
    {
        private static Material fallbackZoneMaterial;

        private float radius = 2.6f;
        private float remainingDuration;
        private float slowFactor = 0.55f;
        private float slowRefreshDuration = 0.35f;
        private float tickInterval = 0.15f;
        private float tickTimer;
        private Transform heroTarget;
        private ISlowable heroSlowable;
        private IObjectPool<FrostSlowZone> pool;
        private Action<FrostSlowZone> onReleasedCallback;
        private Transform visualRoot;
        private bool isReturned;

        public float Radius => radius;
        public float RemainingDuration => remainingDuration;
        public bool IsActive => !isReturned && remainingDuration > 0f;

        public void Initialize(
            Vector3 position,
            float radius,
            float duration,
            float slowFactor,
            float slowRefreshDuration,
            float tickInterval,
            Transform heroTarget,
            ISlowable heroSlowable,
            Material zoneMaterial,
            IObjectPool<FrostSlowZone> pool,
            GameObject customVisualPrefab = null,
            Action<FrostSlowZone> onReleased = null)
        {
            EnsureVisual(zoneMaterial, customVisualPrefab);

            this.radius = Mathf.Max(0.5f, radius);
            remainingDuration = Mathf.Max(0.25f, duration);
            this.slowFactor = Mathf.Clamp(slowFactor, 0.25f, 0.95f);
            this.slowRefreshDuration = Mathf.Max(0.1f, slowRefreshDuration);
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            tickTimer = 0f;
            this.heroTarget = heroTarget;
            this.heroSlowable = heroSlowable;
            this.pool = pool;
            onReleasedCallback = onReleased;
            isReturned = false;

            transform.position = new Vector3(position.x, 0.03f, position.z);
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(this.radius, 1f, this.radius);
            }

            gameObject.SetActive(true);
            CheckAndApplySlow();
        }

        private void EnsureVisual(Material zoneMaterial, GameObject customVisualPrefab)
        {
            if (visualRoot != null) return;

            if (customVisualPrefab != null)
            {
                var inst = Instantiate(customVisualPrefab, transform, false);
                inst.name = "FrostZoneVisual";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                visualRoot = inst.transform;
                var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                        renderers[i].receiveShadows = false;
                    }
                }
                return;
            }

            var rootGo = new GameObject("FrostZoneVisual");
            rootGo.transform.SetParent(transform, false);
            visualRoot = rootGo.transform;

            Material mat = ResolveMaterial(zoneMaterial);
            CreateFlatTile(visualRoot, Vector3.zero, new Vector3(1.55f, 0.04f, 1.55f), mat);
            CreateFlatTile(visualRoot, new Vector3(0.45f, 0.01f, 0.45f), new Vector3(0.7f, 0.05f, 0.7f), mat);
            CreateFlatTile(visualRoot, new Vector3(-0.48f, 0.01f, -0.42f), new Vector3(0.72f, 0.05f, 0.65f), mat);
            CreateFlatTile(visualRoot, new Vector3(-0.45f, 0.01f, 0.46f), new Vector3(0.62f, 0.05f, 0.62f), mat);
            CreateFlatTile(visualRoot, new Vector3(0.46f, 0.01f, -0.46f), new Vector3(0.64f, 0.05f, 0.58f), mat);
        }

        private static Material ResolveMaterial(Material provided)
        {
            if (provided != null) return provided;
            if (fallbackZoneMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                fallbackZoneMaterial = new Material(shader)
                {
                    name = "FrostZoneFallbackMat",
                    enableInstancing = true
                };
                if (fallbackZoneMaterial.HasProperty("_BaseColor"))
                {
                    fallbackZoneMaterial.SetColor("_BaseColor", new Color(0.42f, 0.88f, 1f, 0.52f));
                }
                else
                {
                    fallbackZoneMaterial.color = new Color(0.42f, 0.88f, 1f, 0.52f);
                }
            }
            return fallbackZoneMaterial;
        }

        private static void CreateFlatTile(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
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
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null) mr.sharedMaterial = mat;
            }
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;
            TickZone(Time.deltaTime);
        }

        public bool TickZone(float deltaTime)
        {
            if (isReturned) return false;
            if (deltaTime < 0f) return true;

            remainingDuration -= deltaTime;
            if (remainingDuration <= 0f)
            {
                ForceExpire();
                return false;
            }

            if (visualRoot != null && remainingDuration < 0.4f)
            {
                float fadeScale = Mathf.Clamp01(remainingDuration / 0.4f);
                float r = radius * Mathf.Lerp(0.35f, 1f, fadeScale);
                visualRoot.localScale = new Vector3(r, 1f, r);
            }

            tickTimer -= deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = tickInterval;
                CheckAndApplySlow();
            }

            return true;
        }

        public void ForceExpire()
        {
            if (isReturned) return;
            isReturned = true;
            remainingDuration = 0f;
            onReleasedCallback?.Invoke(this);

            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void CheckAndApplySlow()
        {
            if (heroTarget == null || heroSlowable == null) return;

            Vector3 diff = heroTarget.position - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude <= radius * radius)
            {
                heroSlowable.ApplySlow(slowFactor, slowRefreshDuration);
            }
        }
    }
}
