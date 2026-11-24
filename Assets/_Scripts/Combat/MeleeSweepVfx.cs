using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.Combat
{
    /// <summary>
    /// Pooled "cutting wind" crescent for the Knight melee sweep. The crescent mesh is procedural and shared
    /// between instances; per-swing animation (spin, scale, fade) uses a MaterialPropertyBlock — Zero-GC.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MeleeSweepVfx : MonoBehaviour
    {
        private const int ARC_SEGMENTS = 40;
        private const float INNER_RATIO = 0.4f;
        private const float CREST_RATIO = 0.88f;
        private const float ARC_DEGREES = 250f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh sharedMesh;

        [SerializeField, Min(0.05f)] private float duration = 0.24f;
        [Tooltip("How far the crescent spins around the sweep centre during one swing")]
        [SerializeField] private float spinDegrees = 200f;
        [SerializeField, Range(0.3f, 1f)] private float startScale = 0.7f;
        [SerializeField] private float heightOffset = 0.35f;
        [Tooltip("HDR tint: values above 1 pick up the Bloom post-effect")]
        [SerializeField, ColorUsage(true, true)] private Color color = new Color(1.25f, 1.4f, 1.6f, 1f);

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private IObjectPool<MeleeSweepVfx> pool;
        private float elapsed;
        private float radius = 1f;
        private float baseYaw;
        private bool playing;
        private Transform followTarget;
        private Vector3 followOffset;

        public bool IsPlaying => playing;
        public float Duration => duration;

        private void Awake()
        {
            EnsureSetup();
        }

        private void EnsureSetup()
        {
            if (propertyBlock != null) return;
            propertyBlock = new MaterialPropertyBlock();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            GetComponent<MeshFilter>().sharedMesh = GetSharedMesh();
        }

        /// <summary>Starts one swing visual. <paramref name="facing"/> must be a flat (XZ) direction.</summary>
        /// <param name="follow">Optional owner: the crescent keeps its offset to it, so it stays attached while the hero runs.</param>
        public void Play(Vector3 center, Vector3 facing, float sweepRadius, IObjectPool<MeleeSweepVfx> ownerPool, Transform follow = null)
        {
            EnsureSetup();
            pool = ownerPool;
            followTarget = follow;
            followOffset = follow != null ? center - follow.position : Vector3.zero;
            radius = Mathf.Max(0.1f, sweepRadius);
            elapsed = 0f;
            playing = true;
            baseYaw = facing.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg
                : 0f;
            transform.position = new Vector3(center.x, center.y + heightOffset, center.z);
            Apply(0f);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Finish();
                return;
            }
            if (followTarget != null)
            {
                Vector3 c = followTarget.position + followOffset;
                transform.position = new Vector3(c.x, c.y + heightOffset, c.z);
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            float easeOut = 1f - (1f - t) * (1f - t);
            // Leading edge of the crescent passes through the facing direction half-way through the swing.
            float yaw = baseYaw - ARC_DEGREES + spinDegrees * (easeOut - 0.5f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            float scale = radius * Mathf.Lerp(startScale, 1f, easeOut);
            transform.localScale = new Vector3(scale, 1f, scale);

            Color c = color;
            c.a *= 1f - t * t;
            propertyBlock.SetColor(BaseColorId, c);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Finish()
        {
            if (!playing) return;
            playing = false;
            followTarget = null;
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        /// <summary>Crescent in the XZ plane, unit outer radius. Alpha ramps from the trailing to the leading edge
        /// and peaks at a bright crest close to the outer rim.</summary>
        private static Mesh GetSharedMesh()
        {
            if (sharedMesh != null) return sharedMesh;

            const int rings = 3;
            int columns = ARC_SEGMENTS + 1;
            var vertices = new Vector3[columns * rings];
            var colors = new Color[columns * rings];
            var uvs = new Vector2[columns * rings];
            var triangles = new int[ARC_SEGMENTS * (rings - 1) * 6];
            float[] ringRadius = { INNER_RATIO, CREST_RATIO, 1f };
            float[] ringAlpha = { 0f, 1f, 0.45f };

            for (int i = 0; i < columns; i++)
            {
                float u = i / (float)ARC_SEGMENTS;
                float angle = u * ARC_DEGREES * Mathf.Deg2Rad;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                float lead = Mathf.Pow(u, 1.6f);
                for (int r = 0; r < rings; r++)
                {
                    int v = i * rings + r;
                    vertices[v] = new Vector3(sin * ringRadius[r], 0f, cos * ringRadius[r]);
                    colors[v] = new Color(1f, 1f, 1f, ringAlpha[r] * lead);
                    uvs[v] = new Vector2(u, r / (float)(rings - 1));
                }
            }

            int tri = 0;
            for (int i = 0; i < ARC_SEGMENTS; i++)
            {
                for (int r = 0; r < rings - 1; r++)
                {
                    int a = i * rings + r;
                    int b = a + 1;
                    int c = a + rings;
                    int d = c + 1;
                    triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                    triangles[tri++] = b; triangles[tri++] = d; triangles[tri++] = c;
                }
            }

            sharedMesh = new Mesh { name = "MeleeSweepCrescent" };
            sharedMesh.vertices = vertices;
            sharedMesh.colors = colors;
            sharedMesh.uv = uvs;
            sharedMesh.triangles = triangles;
            sharedMesh.RecalculateNormals();
            sharedMesh.RecalculateBounds();
            return sharedMesh;
        }
    }
}
