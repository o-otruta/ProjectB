using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>Per-mode settings of <see cref="AdaptiveGridLayout"/>.</summary>
    [Serializable]
    public struct GridModeSettings
    {
        [Min(1)] public int columns;
        public Vector2 cellSize;
        public Vector2 spacing;
    }

    /// <summary>
    /// Drives the sibling <see cref="GridLayoutGroup"/> by layout mode (Sprint 14.1): fixed column count, cell size and
    /// spacing per mode, plus an optional extra column on ultra-wide Landscape screens (21:9, threshold in LayoutConfig).
    /// Lists (Upgrades / Achievements / Abilities: 1 → 2 → 3 columns) and Daily Bonus (4+3 → 7 in a row) use it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class AdaptiveGridLayout : MonoBehaviour, ILayoutModeConsumer
    {
        [SerializeField] private GridModeSettings portrait = new GridModeSettings { columns = 1, cellSize = new Vector2(960f, 180f), spacing = new Vector2(24f, 24f) };
        [SerializeField] private GridModeSettings landscape = new GridModeSettings { columns = 2, cellSize = new Vector2(820f, 180f), spacing = new Vector2(24f, 24f) };
        [Tooltip("Columns on ultra-wide Landscape screens. 0 = same as Landscape.")]
        [SerializeField, Min(0)] private int ultraWideColumns;
        [SerializeField, HideInInspector] private LayoutMode appliedMode = LayoutMode.Portrait;

        private GridLayoutGroup grid;
        private LayoutModeBinding binding;

        public LayoutMode AppliedMode => appliedMode;
        public int UltraWideColumns { get => ultraWideColumns; set => ultraWideColumns = Mathf.Max(0, value); }
        public GridLayoutGroup Grid => EnsureGrid();

        private void Reset()
        {
            GridLayoutGroup g = EnsureGrid();
            if (g == null) return;
            int columns = g.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? g.constraintCount : 1;
            portrait = new GridModeSettings { columns = columns, cellSize = g.cellSize, spacing = g.spacing };
            landscape = new GridModeSettings { columns = columns * 2, cellSize = g.cellSize, spacing = g.spacing };
        }

        private void OnEnable()
        {
            if (binding == null) binding = new LayoutModeBinding(null, HandleScreenChanged);
            if (binding.Enable())
            {
                HandleScreenChanged();
            }
        }

        private void OnDisable()
        {
            binding?.Disable();
        }

        public GridModeSettings GetSettings(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscape : portrait;
        }

        public void SetSettings(LayoutMode mode, GridModeSettings settings)
        {
            if (mode == LayoutMode.Landscape) landscape = settings;
            else portrait = settings;
        }

        /// <summary>Editor preview / no-service path: assumes the mode's reference aspect (16:9 never counts as ultra-wide).</summary>
        public void ApplyLayoutMode(LayoutMode mode)
        {
            Apply(mode, mode == LayoutMode.Landscape ? 16f / 9f : 9f / 16f, float.PositiveInfinity);
        }

        public void Apply(LayoutMode mode, float aspect, float ultraWideAspect)
        {
            GridLayoutGroup g = EnsureGrid();
            if (g == null) return;

            GridModeSettings settings = GetSettings(mode);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = AdaptiveLayoutMath.GetGridColumns(mode, aspect, portrait.columns, landscape.columns, ultraWideColumns, ultraWideAspect);
            if (settings.cellSize.x > 0f && settings.cellSize.y > 0f) g.cellSize = settings.cellSize;
            g.spacing = settings.spacing;
            appliedMode = mode;
        }

        private void HandleScreenChanged()
        {
            LayoutModeService service = binding.Service;
            if (service == null) return;
            Apply(service.Mode, service.Aspect, service.Config.ultraWideAspect);
        }

        private GridLayoutGroup EnsureGrid()
        {
            if (grid == null) grid = GetComponent<GridLayoutGroup>();
            return grid;
        }
    }
}
