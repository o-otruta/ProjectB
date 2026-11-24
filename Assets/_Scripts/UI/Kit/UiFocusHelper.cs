using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectB.Core;

namespace ProjectB.UI
{
    /// <summary>
    /// Manages <see cref="EventSystem.SetSelectedGameObject"/> for gamepad and keyboard UI navigation (Sprint 14.3).
    /// Suppresses sticky selection in <see cref="InputMode.Touch"/> so touch buttons never look artificially focused.
    /// </summary>
    public static class UiFocusHelper
    {
        public static void SetFirstSelected(Selectable selectable, InputModeService inputMode = null)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;

            if (inputMode == null && Application.isPlaying)
            {
                RootServices.TryResolve(out inputMode);
            }

            if (inputMode != null && inputMode.Mode == InputMode.Touch)
            {
                es.SetSelectedGameObject(null);
                return;
            }

            if (selectable != null && selectable.gameObject.activeInHierarchy && selectable.interactable)
            {
                es.SetSelectedGameObject(selectable.gameObject);
            }
            else
            {
                es.SetSelectedGameObject(null);
            }
        }

        public static void ClearSelection()
        {
            EventSystem es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
            }
        }

        public static Selectable FindFirstInteractable(Transform root)
        {
            if (root == null) return null;
            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(includeInactive: false);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable s = selectables[i];
                if (s != null && s.IsActive() && s.IsInteractable())
                {
                    return s;
                }
            }
            return null;
        }
    }
}
