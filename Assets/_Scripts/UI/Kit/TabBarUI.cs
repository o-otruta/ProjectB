using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// Bottom navigation (Sprint 13.2): one dark bar, the active <see cref="BottomTabUI"/> gets the accent plate and a
    /// raised icon; each tab may carry a <see cref="BadgeUI"/>. Selection only changes visuals — screens are switched
    /// by <see cref="MainMenuUI"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabBarUI : MonoBehaviour
    {
        [SerializeField] private BottomTabUI[] tabs;
        [SerializeField] private BadgeUI[] badges;
        [SerializeField] private Image barBackground;

        private int selectedIndex = -1;

        public int SelectedIndex => selectedIndex;
        public int TabCount => tabs != null ? tabs.Length : 0;

        private void Awake()
        {
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            if (barBackground != null) barBackground.color = theme.surfaceSunken;
            if (tabs == null) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].ApplyTheme(theme);
            }
        }

        public void Select(int index, bool instant = false)
        {
            if (tabs == null) return;
            selectedIndex = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].SetActiveState(i == index, instant);
            }
        }

        public void SetBadge(int index, int count)
        {
            BadgeUI badge = GetBadge(index);
            if (badge != null) badge.SetCount(count);
        }

        public void SetBadgeDot(int index, bool visible)
        {
            BadgeUI badge = GetBadge(index);
            if (badge != null) badge.SetDot(visible);
        }

        public BadgeUI GetBadge(int index)
        {
            return badges != null && index >= 0 && index < badges.Length ? badges[index] : null;
        }

#if UNITY_EDITOR
        public void EditorSetup(BottomTabUI[] tabItems, BadgeUI[] tabBadges, Image background)
        {
            tabs = tabItems;
            badges = tabBadges;
            barBackground = background;
        }
#endif
    }
}
