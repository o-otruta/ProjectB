using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// Isolates a heavy menu panel in its own nested <see cref="Canvas"/> + <see cref="GraphicRaycaster"/>
    /// and hides/shows it without deactivating the GameObject after first open, avoiding repeated TMP mesh
    /// regeneration, LayoutGroup teardown, and root-Canvas batch rebuilds.
    /// </summary>
    internal sealed class PanelCanvasVisibility
    {
        private GameObject root;
        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private CanvasGroup canvasGroup;
        private ScreenTransition transition;
        private bool initialized;
        private bool isVisible;

        public bool IsVisible => isVisible;

        public void EnsureInitialized(GameObject target)
        {
            if (target == null) return;
            if (initialized && root == target && canvas != null && raycaster != null && canvasGroup != null) return;

            root = target;
            canvas = target.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = target.AddComponent<Canvas>();
            }
            canvas.overrideSorting = false;

            Canvas parentCanvas = target.transform.parent != null
                ? target.transform.parent.GetComponentInParent<Canvas>(true)
                : null;
            Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
            if (rootCanvas != null)
            {
                canvas.additionalShaderChannels = rootCanvas.additionalShaderChannels;
                canvas.vertexColorAlwaysGammaSpace = rootCanvas.vertexColorAlwaysGammaSpace;
            }
            else
            {
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.Normal
                    | AdditionalCanvasShaderChannels.Tangent;
            }

            raycaster = target.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = target.AddComponent<GraphicRaycaster>();
            }

            canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            transition = target.GetComponent<ScreenTransition>();
            initialized = true;
        }

        public bool BeginShow(GameObject target)
        {
            if (target == null) return false;
            EnsureInitialized(target);

            isVisible = true;
            canvas.enabled = true;
            raycaster.enabled = true;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            bool wasAlreadyActive = target.activeSelf;
            if (!wasAlreadyActive)
            {
                target.SetActive(true);
            }
            return wasAlreadyActive;
        }

        public void FinishShow(bool wasAlreadyActive)
        {
            if (wasAlreadyActive && transition != null)
            {
                transition.PlayIn();
            }
        }

        public void Hide(GameObject target)
        {
            isVisible = false;
            if (target == null || !target.activeSelf) return;

            EnsureInitialized(target);
            if (transition != null)
            {
                transition.StopAndReset();
            }

            canvas.enabled = false;
            raycaster.enabled = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void SyncOnEnable(GameObject target)
        {
            isVisible = true;
            if (initialized && canvas != null && raycaster != null && canvasGroup != null)
            {
                canvas.enabled = true;
                raycaster.enabled = true;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void SyncOnDisable()
        {
            isVisible = false;
        }
    }
}
