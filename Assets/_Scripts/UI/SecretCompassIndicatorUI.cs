using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Player;

namespace ProjectB.UI
{
    public enum CompassPoiType
    {
        Secret = 0,
        ChallengeShrine = 1,
        VacuumMagnet = 2,
        GoldGoblin = 3
    }

    /// <summary>
    /// Multi-POI off-screen edge compass displaying up to 3 simultaneous color-coded pointers
    /// (Secret Item - purple, Challenge Shrine - crimson, Vacuum Magnet - cyan, Gold Goblin - gold).
    /// Zero GC allocations in LateUpdate.
    /// </summary>
    public class SecretCompassIndicatorUI : MonoBehaviour
    {
        public const int MAX_COMPASS_SLOTS = 3;
        private const int MAX_CANDIDATES = 16;
        private const int MAX_DYNAMIC_POIS = 8;

        [System.Serializable]
        public struct CompassSlotRefs
        {
            public RectTransform indicatorRect;
            public RectTransform arrowRect;
            public Image arrowImage;
            public TextMeshProUGUI distanceText;
        }

        private struct SlotRuntimeState
        {
            public CompassSlotRefs refs;
            public bool isVisible;
            public int lastDisplayedMeters;
            public CompassPoiType currentPoiType;
            public Color currentArrowColor;
        }

        private struct CompassPoiCandidate
        {
            public CompassPoiType type;
            public int priority;
            public float distanceSqr;
            public Vector2 anchoredPos;
            public float arrowAngleDeg;
            public int distanceMeters;
        }

        private struct DynamicPoiEntry
        {
            public Transform target;
            public CompassPoiType type;
            public float maxDistance;
        }

        private static readonly string[] CachedDistanceLabels = BuildDistanceLabels(201);
        private static readonly Color SecretColor = new Color(0.82f, 0.36f, 1.0f, 1f);
        private static readonly Color ShrineColor = new Color(1.0f, 0.22f, 0.30f, 1f);
        private static readonly Color MagnetColor = new Color(0.22f, 0.92f, 1.0f, 1f);
        private static readonly Color GoldGoblinColor = new Color(1.0f, 0.84f, 0.18f, 1f);

        private static readonly DynamicPoiEntry[] DynamicPois = new DynamicPoiEntry[MAX_DYNAMIC_POIS];
        private static int dynamicPoiCount;

        [Header("Primary Slot (Slot 0)")]
        [SerializeField] private RectTransform indicatorRect;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private Image arrowImage;
        [SerializeField] private TextMeshProUGUI distanceText;

        [Header("Additional Slots (Slots 1-2)")]
        [SerializeField] private CompassSlotRefs[] extraSlots;

        [Header("Compass Settings")]
        [SerializeField] private float revealDistance = 45f;
        [SerializeField] private float shrineRevealDistance = 55f;
        [SerializeField] private float magnetRevealDistance = 48f;
        [SerializeField] private float goldGoblinRevealDistance = 70f;
        [SerializeField] private float sidePadding = 85f;
        [SerializeField] private float topPadding = 240f;
        [SerializeField] private float bottomPadding = 260f;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseAmplitude = 0.08f;

        private SecretItemSpawner secretItemSpawner;
        private ArenaGenerator arenaGenerator;
        private VacuumMagnetSpawner vacuumMagnetSpawner;
        private HeroHealth heroHealth;
        private CameraController cameraController;
        private Camera mainCamera;
        private RectTransform parentRect;

        private readonly SlotRuntimeState[] slotStates = new SlotRuntimeState[MAX_COMPASS_SLOTS];
        private readonly CompassPoiCandidate[] candidateBuffer = new CompassPoiCandidate[MAX_CANDIDATES];
        private int activeVisibleSlotCount;
        private bool slotsInitialized;

        public int ActiveVisibleSlotCount => activeVisibleSlotCount;

        private static string[] BuildDistanceLabels(int count)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++)
            {
                labels[i] = $"{i}m";
            }
            return labels;
        }

        public static string GetCachedDistanceLabel(int meters)
        {
            if (meters >= 0 && meters < CachedDistanceLabels.Length)
            {
                return CachedDistanceLabels[meters];
            }
            return CachedDistanceLabels[CachedDistanceLabels.Length - 1];
        }

        public static Color GetPoiColor(CompassPoiType type)
        {
            return type switch
            {
                CompassPoiType.ChallengeShrine => ShrineColor,
                CompassPoiType.VacuumMagnet => MagnetColor,
                CompassPoiType.GoldGoblin => GoldGoblinColor,
                _ => SecretColor
            };
        }

        public static int GetPoiPriority(CompassPoiType type)
        {
            return type switch
            {
                CompassPoiType.GoldGoblin => 4,
                CompassPoiType.ChallengeShrine => 3,
                CompassPoiType.Secret => 2,
                CompassPoiType.VacuumMagnet => 1,
                _ => 0
            };
        }

        public static void RegisterDynamicPoi(Transform target, CompassPoiType type, float maxDistance = 70f)
        {
            if (target == null) return;

            for (int i = 0; i < dynamicPoiCount; i++)
            {
                if (DynamicPois[i].target == target)
                {
                    DynamicPois[i].type = type;
                    DynamicPois[i].maxDistance = maxDistance;
                    return;
                }
            }

            if (dynamicPoiCount < MAX_DYNAMIC_POIS)
            {
                DynamicPois[dynamicPoiCount++] = new DynamicPoiEntry
                {
                    target = target,
                    type = type,
                    maxDistance = maxDistance
                };
            }
        }

        public static void UnregisterDynamicPoi(Transform target)
        {
            if (target == null) return;
            for (int i = 0; i < dynamicPoiCount; i++)
            {
                if (DynamicPois[i].target == target)
                {
                    dynamicPoiCount--;
                    DynamicPois[i] = DynamicPois[dynamicPoiCount];
                    DynamicPois[dynamicPoiCount] = default;
                    return;
                }
            }
        }

        public static void ClearDynamicPois()
        {
            for (int i = 0; i < dynamicPoiCount; i++)
            {
                DynamicPois[i] = default;
            }
            dynamicPoiCount = 0;
        }

        [Inject]
        public void Construct(
            SecretItemSpawner secretItemSpawner,
            HeroHealth heroHealth,
            CameraController cameraController = null,
            ArenaGenerator arenaGenerator = null,
            VacuumMagnetSpawner vacuumMagnetSpawner = null)
        {
            this.secretItemSpawner = secretItemSpawner;
            this.heroHealth = heroHealth;
            this.cameraController = cameraController;
            this.arenaGenerator = arenaGenerator;
            this.vacuumMagnetSpawner = vacuumMagnetSpawner;
        }

        private void Awake()
        {
            EnsureSlotStates();

            var graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }

            HideAllSlots(force: true);
        }

        private void Start()
        {
            ResolveCamera();
        }

        private void EnsureSlotStates()
        {
            if (slotsInitialized) return;
            slotsInitialized = true;

            if (indicatorRect == null)
            {
                indicatorRect = (RectTransform)transform;
            }
            parentRect = indicatorRect.parent as RectTransform;

            slotStates[0].refs = new CompassSlotRefs
            {
                indicatorRect = indicatorRect,
                arrowRect = arrowRect,
                arrowImage = arrowImage,
                distanceText = distanceText
            };
            slotStates[0].lastDisplayedMeters = -1;

            bool canClonePrimary = indicatorRect != null && indicatorRect != (RectTransform)transform;
            for (int i = 1; i < MAX_COMPASS_SLOTS; i++)
            {
                int extraIdx = i - 1;
                CompassSlotRefs slotRefs = default;
                if (extraSlots != null && extraIdx < extraSlots.Length && extraSlots[extraIdx].indicatorRect != null)
                {
                    slotRefs = extraSlots[extraIdx];
                }
                else if (canClonePrimary)
                {
                    GameObject clone = Instantiate(indicatorRect.gameObject, indicatorRect.parent, false);
                    clone.name = $"{indicatorRect.gameObject.name}_Slot{i}";
                    var cloneRect = (RectTransform)clone.transform;
                    Transform arrowTf = clone.transform.Find("Arrow");
                    Transform textTf = clone.transform.Find("DistanceText");
                    slotRefs = new CompassSlotRefs
                    {
                        indicatorRect = cloneRect,
                        arrowRect = arrowTf as RectTransform,
                        arrowImage = arrowTf != null ? arrowTf.GetComponent<Image>() : clone.GetComponentInChildren<Image>(true),
                        distanceText = textTf != null ? textTf.GetComponent<TextMeshProUGUI>() : clone.GetComponentInChildren<TextMeshProUGUI>(true)
                    };
                }

                slotStates[i].refs = slotRefs;
                slotStates[i].lastDisplayedMeters = -1;
            }
        }

        private Camera ResolveCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = cameraController != null
                    ? cameraController.GetComponent<Camera>()
                    : Camera.main;
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                }
            }
            return mainCamera;
        }

        private void LateUpdate()
        {
            RefreshIndicators();
        }

        public void RefreshIndicators()
        {
            EnsureSlotStates();

            if (heroHealth == null || heroHealth.IsDead)
            {
                HideAllSlots();
                return;
            }

            Camera cam = ResolveCamera();
            if (cam == null)
            {
                HideAllSlots();
                return;
            }

            if (parentRect == null && indicatorRect != null)
            {
                parentRect = indicatorRect.parent as RectTransform;
            }

            Vector2 canvasSize = parentRect != null
                ? parentRect.rect.size
                : new Vector2(1080f, 1920f);
            if (canvasSize.x <= 1f || canvasSize.y <= 1f)
            {
                canvasSize = new Vector2(1080f, 1920f);
            }

            Vector3 heroPos = heroHealth.transform.position;
            int candidateCount = 0;

            // 1. Secret Item
            if (secretItemSpawner != null)
            {
                InteractableItem activeItem = secretItemSpawner.ActiveItem;
                if (activeItem != null && !activeItem.IsCollected && activeItem.gameObject.activeInHierarchy)
                {
                    TryAddCandidate(CompassPoiType.Secret, heroPos, activeItem.transform.position, revealDistance, cam, canvasSize, ref candidateCount);
                }
            }

            // 2. Challenge Shrine (unactivated)
            if (arenaGenerator != null)
            {
                ChallengeShrine shrine = arenaGenerator.ActiveShrine;
                if (shrine != null && shrine.State == ShrineState.Idle && shrine.gameObject.activeInHierarchy)
                {
                    TryAddCandidate(CompassPoiType.ChallengeShrine, heroPos, shrine.transform.position, shrineRevealDistance, cam, canvasSize, ref candidateCount);
                }
            }

            // 3. Vacuum Magnets (active uncollected drops)
            if (vacuumMagnetSpawner != null)
            {
                var pickups = vacuumMagnetSpawner.ActivePickups;
                if (pickups != null)
                {
                    for (int i = 0; i < pickups.Count && candidateCount < MAX_CANDIDATES; i++)
                    {
                        VacuumMagnetPickup pickup = pickups[i];
                        if (pickup != null && !pickup.IsCollected && pickup.gameObject.activeInHierarchy)
                        {
                            TryAddCandidate(CompassPoiType.VacuumMagnet, heroPos, pickup.transform.position, magnetRevealDistance, cam, canvasSize, ref candidateCount);
                        }
                    }
                }
            }

            // 4. Dynamic POIs (e.g. Gold Goblin)
            for (int i = 0; i < dynamicPoiCount && candidateCount < MAX_CANDIDATES; i++)
            {
                DynamicPoiEntry entry = DynamicPois[i];
                if (entry.target != null && entry.target.gameObject.activeInHierarchy)
                {
                    float maxDist = entry.type == CompassPoiType.GoldGoblin
                        ? Mathf.Max(entry.maxDistance, goldGoblinRevealDistance)
                        : entry.maxDistance;
                    TryAddCandidate(entry.type, heroPos, entry.target.position, maxDist, cam, canvasSize, ref candidateCount);
                }
            }

            if (candidateCount == 0)
            {
                HideAllSlots();
                return;
            }

            SortCandidatesInPlace(candidateCount);

            int visibleCount = Mathf.Min(MAX_COMPASS_SLOTS, candidateCount);
            activeVisibleSlotCount = visibleCount;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;

            for (int i = 0; i < MAX_COMPASS_SLOTS; i++)
            {
                if (i < visibleCount)
                {
                    ApplyCandidateToSlot(i, candidateBuffer[i], pulse);
                }
                else
                {
                    SetSlotVisible(i, false);
                }
            }
        }

        private void TryAddCandidate(
            CompassPoiType type,
            Vector3 heroPos,
            Vector3 targetPos,
            float maxDistance,
            Camera cam,
            Vector2 canvasSize,
            ref int candidateCount)
        {
            if (candidateCount >= MAX_CANDIDATES) return;

            if (!EvaluateIndicatorState(
                heroPos,
                targetPos,
                cam,
                canvasSize,
                maxDistance,
                out Vector2 anchoredPos,
                out float arrowAngleDeg,
                out int distanceMeters))
            {
                return;
            }

            float dx = targetPos.x - heroPos.x;
            float dz = targetPos.z - heroPos.z;
            candidateBuffer[candidateCount++] = new CompassPoiCandidate
            {
                type = type,
                priority = GetPoiPriority(type),
                distanceSqr = dx * dx + dz * dz,
                anchoredPos = anchoredPos,
                arrowAngleDeg = arrowAngleDeg,
                distanceMeters = distanceMeters
            };
        }

        private void SortCandidatesInPlace(int count)
        {
            for (int i = 1; i < count; i++)
            {
                CompassPoiCandidate key = candidateBuffer[i];
                int j = i - 1;
                while (j >= 0 && IsHigherOrder(key, candidateBuffer[j]))
                {
                    candidateBuffer[j + 1] = candidateBuffer[j];
                    j--;
                }
                candidateBuffer[j + 1] = key;
            }
        }

        private static bool IsHigherOrder(CompassPoiCandidate a, CompassPoiCandidate b)
        {
            if (a.priority != b.priority)
            {
                return a.priority > b.priority;
            }
            return a.distanceSqr < b.distanceSqr;
        }

        private void ApplyCandidateToSlot(int slotIndex, CompassPoiCandidate candidate, float pulse)
        {
            SetSlotVisible(slotIndex, true);

            ref SlotRuntimeState state = ref slotStates[slotIndex];
            state.currentPoiType = candidate.type;
            Color poiColor = GetPoiColor(candidate.type);
            state.currentArrowColor = poiColor;

            if (state.refs.indicatorRect != null)
            {
                state.refs.indicatorRect.anchoredPosition = candidate.anchoredPos;
                state.refs.indicatorRect.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (state.refs.arrowRect != null)
            {
                state.refs.arrowRect.localEulerAngles = new Vector3(0f, 0f, candidate.arrowAngleDeg);
            }

            if (state.refs.arrowImage != null && state.refs.arrowImage.color != poiColor)
            {
                state.refs.arrowImage.color = poiColor;
            }

            if (state.refs.distanceText != null)
            {
                if (state.lastDisplayedMeters != candidate.distanceMeters)
                {
                    state.lastDisplayedMeters = candidate.distanceMeters;
                    state.refs.distanceText.text = GetCachedDistanceLabel(candidate.distanceMeters);
                }
                if (state.refs.distanceText.color != poiColor)
                {
                    state.refs.distanceText.color = poiColor;
                }
            }
        }

        public CompassPoiType GetSlotPoiType(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_COMPASS_SLOTS) return CompassPoiType.Secret;
            return slotStates[slotIndex].currentPoiType;
        }

        public Color GetSlotArrowColor(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_COMPASS_SLOTS) return Color.clear;
            return slotStates[slotIndex].currentArrowColor;
        }

        public bool EvaluateIndicatorState(
            Vector3 heroPos,
            Vector3 itemPos,
            Camera cam,
            Vector2 canvasSize,
            out Vector2 anchoredPos,
            out float arrowAngleDeg,
            out int distanceMeters)
        {
            return EvaluateIndicatorState(
                heroPos,
                itemPos,
                cam,
                canvasSize,
                revealDistance,
                out anchoredPos,
                out arrowAngleDeg,
                out distanceMeters);
        }

        public bool EvaluateIndicatorState(
            Vector3 heroPos,
            Vector3 itemPos,
            Camera cam,
            Vector2 canvasSize,
            float maxDistance,
            out Vector2 anchoredPos,
            out float arrowAngleDeg,
            out int distanceMeters)
        {
            anchoredPos = Vector2.zero;
            arrowAngleDeg = 0f;

            float dx = itemPos.x - heroPos.x;
            float dz = itemPos.z - heroPos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            distanceMeters = Mathf.RoundToInt(dist);

            if (dist > maxDistance || cam == null)
            {
                return false;
            }

            Vector3 vp = cam.WorldToViewportPoint(itemPos);
            bool onScreen = vp.z > 0f
                && vp.x >= 0.05f && vp.x <= 0.95f
                && vp.y >= 0.05f && vp.y <= 0.95f;

            if (onScreen)
            {
                return false;
            }

            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f)
            {
                dir = -dir;
            }

            dir.x *= canvasSize.x;
            dir.y *= canvasSize.y;

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.up;
            }
            dir.Normalize();

            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;
            float minX = Mathf.Min(0f, -halfW + sidePadding);
            float maxX = Mathf.Max(0f, halfW - sidePadding);
            float minY = Mathf.Min(0f, -halfH + bottomPadding);
            float maxY = Mathf.Max(0f, halfH - topPadding);

            float t = float.MaxValue;
            if (dir.x > 0.0001f) t = Mathf.Min(t, maxX / dir.x);
            else if (dir.x < -0.0001f) t = Mathf.Min(t, minX / dir.x);

            if (dir.y > 0.0001f) t = Mathf.Min(t, maxY / dir.y);
            else if (dir.y < -0.0001f) t = Mathf.Min(t, minY / dir.y);

            if (t == float.MaxValue || t < 0f)
            {
                t = 0f;
            }

            anchoredPos = dir * t;
            arrowAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            return true;
        }

        private void HideAllSlots(bool force = false)
        {
            activeVisibleSlotCount = 0;
            for (int i = 0; i < MAX_COMPASS_SLOTS; i++)
            {
                SetSlotVisible(i, false, force);
            }
        }

        private void SetSlotVisible(int slotIndex, bool visible, bool force = false)
        {
            if (slotIndex < 0 || slotIndex >= MAX_COMPASS_SLOTS) return;
            ref SlotRuntimeState state = ref slotStates[slotIndex];
            if (!force && state.isVisible == visible)
            {
                return;
            }
            state.isVisible = visible;

            RectTransform rect = state.refs.indicatorRect;
            if (rect != null && rect != (RectTransform)transform)
            {
                if (rect.gameObject.activeSelf != visible)
                {
                    rect.gameObject.SetActive(visible);
                }
            }
            else
            {
                if (state.refs.arrowImage != null && state.refs.arrowImage.enabled != visible)
                {
                    state.refs.arrowImage.enabled = visible;
                }
                if (state.refs.distanceText != null && state.refs.distanceText.enabled != visible)
                {
                    state.refs.distanceText.enabled = visible;
                }
            }
        }
    }
}
