using UnityEngine;

namespace ProjectB.Visuals
{
    /// <summary>
    /// Lightweight flipbook animator for voxel models that swaps meshes on a MeshFilter.
    /// Zero GC allocations in Update, optimized for high-density enemy swarms on mobile.
    /// </summary>
    [DisallowMultipleComponent]
    public class VoxelMeshAnimator : MonoBehaviour
    {
        [Header("Mesh Settings")]
        [SerializeField] private MeshFilter targetMeshFilter;
        [Tooltip("Default mesh displayed when idle / stopped")]
        [SerializeField] private Mesh idleMesh;
        [SerializeField] private Mesh[] frames;

        [Header("Playback Settings")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("Animation speed in frames per second (8-12 is typical for voxel stop-motion)")]
        [SerializeField] private float framesPerSecond = 10f;
        [SerializeField] private bool loop = true;
        [Tooltip("Adds a random time offset on enable to desynchronize swarms of enemies")]
        [SerializeField] private bool randomizeStartOffset = true;

        [Header("Bobbing / Floating Effect")]
        [SerializeField] private bool enableBobbing = false;
        [Tooltip("Transform to apply vertical floating to. Defaults to this transform if unassigned.")]
        [SerializeField] private Transform bobbingTransform;
        [SerializeField] private float bobbingSpeed = 4f;
        [SerializeField] private float bobbingHeight = 0.1f;

        private float timer;
        private int currentFrameIndex = -1;
        private Vector3 baseLocalPosition;
        private bool isPlaying = true;

        public MeshFilter TargetMeshFilter
        {
            get => targetMeshFilter;
            set => targetMeshFilter = value;
        }

        public Mesh IdleMesh
        {
            get => idleMesh;
            set
            {
                idleMesh = value;
                if (!isPlaying && targetMeshFilter != null && idleMesh != null)
                {
                    targetMeshFilter.sharedMesh = idleMesh;
                }
            }
        }

        public Mesh[] Frames
        {
            get => frames;
            set
            {
                frames = value;
                currentFrameIndex = -1;
                UpdateFrame();
            }
        }

        public bool PlayOnEnable
        {
            get => playOnEnable;
            set => playOnEnable = value;
        }

        public bool IsPlaying => isPlaying;

        public float FramesPerSecond
        {
            get => framesPerSecond;
            set => framesPerSecond = value;
        }

        public bool EnableBobbing
        {
            get => enableBobbing;
            set => enableBobbing = value;
        }

        public Transform BobbingTransform
        {
            get => bobbingTransform;
            set
            {
                bobbingTransform = value;
                if (bobbingTransform != null)
                {
                    baseLocalPosition = bobbingTransform.localPosition;
                }
            }
        }

        private void Awake()
        {
            if (targetMeshFilter == null)
            {
                targetMeshFilter = GetComponent<MeshFilter>();
                if (targetMeshFilter == null)
                {
                    targetMeshFilter = GetComponentInChildren<MeshFilter>();
                }
            }

            if (bobbingTransform == null)
            {
                bobbingTransform = transform;
            }

            baseLocalPosition = bobbingTransform.localPosition;
        }

        private void OnEnable()
        {
            if (bobbingTransform != null)
            {
                baseLocalPosition = bobbingTransform.localPosition;
            }

            if (!playOnEnable)
            {
                Stop();
                return;
            }

            isPlaying = true;

            if (randomizeStartOffset)
            {
                timer = Random.Range(0f, 10f);
            }
            else
            {
                timer = 0f;
            }

            currentFrameIndex = -1;
            UpdateFrame();
        }

        private void Update()
        {
            if (!isPlaying) return;

            if (frames != null && frames.Length > 1)
            {
                timer += Time.deltaTime;
                UpdateFrame();
            }

            if (enableBobbing && bobbingTransform != null)
            {
                float yOffset = Mathf.Sin(timer * bobbingSpeed) * bobbingHeight;
                bobbingTransform.localPosition = new Vector3(
                    baseLocalPosition.x,
                    baseLocalPosition.y + yOffset,
                    baseLocalPosition.z
                );
            }
        }

        private void UpdateFrame()
        {
            if (frames == null || frames.Length == 0 || targetMeshFilter == null) return;

            int totalFrames = frames.Length;
            int frameIndex = Mathf.FloorToInt(timer * framesPerSecond);

            if (loop)
            {
                frameIndex = (frameIndex % totalFrames + totalFrames) % totalFrames;
            }
            else
            {
                if (frameIndex >= totalFrames)
                {
                    frameIndex = totalFrames - 1;
                    isPlaying = false;
                }
            }

            if (frameIndex != currentFrameIndex)
            {
                currentFrameIndex = frameIndex;
                var mesh = frames[currentFrameIndex];
                if (mesh != null)
                {
                    targetMeshFilter.sharedMesh = mesh;
                }
            }
        }

        public void Play()
        {
            if (!isPlaying)
            {
                isPlaying = true;
                timer = 0f;
                currentFrameIndex = -1;
                UpdateFrame();
            }
        }

        public void Pause() => isPlaying = false;

        public void Stop()
        {
            isPlaying = false;
            currentFrameIndex = -1;
            timer = 0f;
            if (idleMesh != null && targetMeshFilter != null)
            {
                targetMeshFilter.sharedMesh = idleMesh;
            }
        }
    }
}

