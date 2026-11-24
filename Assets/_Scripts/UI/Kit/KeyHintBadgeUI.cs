using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Core;

namespace ProjectB.UI
{
    /// <summary>
    /// Small keyboard-shortcut pill shown only when <see cref="InputModeService.Mode"/> is <see cref="InputMode.KeyboardMouse"/>
    /// (Sprint 14.3). Zero-GC: uses a cached delegate for <see cref="InputModeService.InputModeChanged"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyHintBadgeUI : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private string keyLabel;

        private InputModeService inputModeService;
        private Action<InputMode> cachedInputModeHandler;
        private bool externallyAllowed = true;

        public string KeyLabel => keyLabel;
        public bool IsHintVisible
        {
            get
            {
                if (visualRoot != null && visualRoot != gameObject) return visualRoot.activeSelf;
                if (backgroundImage != null) return backgroundImage.gameObject != gameObject ? backgroundImage.gameObject.activeSelf : backgroundImage.enabled;
                if (keyText != null) return keyText.gameObject != gameObject ? keyText.gameObject.activeSelf : keyText.enabled;
                return false;
            }
        }

        private void Awake()
        {
            if (visualRoot == null || visualRoot == gameObject)
            {
                if (backgroundImage != null && backgroundImage.gameObject != gameObject)
                {
                    visualRoot = backgroundImage.gameObject;
                }
                else if (keyText != null && keyText.gameObject != gameObject)
                {
                    visualRoot = keyText.gameObject;
                }
            }
            ApplyTheme();
            SyncLabel();
        }

        private void OnEnable()
        {
            if (inputModeService == null && Application.isPlaying)
            {
                RootServices.TryResolve(out inputModeService);
            }
            Subscribe();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Bind(InputModeService service)
        {
            if (ReferenceEquals(inputModeService, service))
            {
                RefreshVisibility();
                return;
            }

            Unsubscribe();
            inputModeService = service;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            RefreshVisibility();
        }

        public void SetKeyLabel(string label)
        {
            keyLabel = label ?? string.Empty;
            SyncLabel();
        }

        public void SetAllowed(bool allowed)
        {
            if (externallyAllowed == allowed) return;
            externallyAllowed = allowed;
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            bool show = externallyAllowed && inputModeService != null && inputModeService.Mode == InputMode.KeyboardMouse;
            if (visualRoot != null && visualRoot != gameObject)
            {
                if (visualRoot.activeSelf != show) visualRoot.SetActive(show);
                return;
            }

            if (backgroundImage != null && backgroundImage.gameObject != gameObject)
            {
                backgroundImage.gameObject.SetActive(show);
            }
            else if (backgroundImage != null)
            {
                backgroundImage.enabled = show;
            }

            if (keyText != null && keyText.gameObject != gameObject)
            {
                keyText.gameObject.SetActive(show);
            }
            else if (keyText != null)
            {
                keyText.enabled = show;
            }
        }

        public void ApplyTheme()
        {
            UiTheme theme = UiTheme.Current;
            if (theme == null) return;

            if (backgroundImage != null)
            {
                if (theme.pillSprite != null)
                {
                    backgroundImage.sprite = theme.pillSprite;
                    backgroundImage.type = Image.Type.Sliced;
                }
                backgroundImage.color = UiTheme.WithAlpha(theme.surfaceSunken, 0.92f);
                backgroundImage.raycastTarget = false;
            }

            if (keyText != null)
            {
                if (theme.bodyBoldFont != null) keyText.font = theme.bodyBoldFont;
                else if (theme.bodyFont != null) keyText.font = theme.bodyFont;
                keyText.color = theme.textPrimary;
                keyText.raycastTarget = false;
            }
        }

        private void SyncLabel()
        {
            if (keyText != null && !string.IsNullOrEmpty(keyLabel))
            {
                keyText.text = keyLabel;
            }
        }

        private void Subscribe()
        {
            if (inputModeService == null) return;
            cachedInputModeHandler ??= HandleInputModeChanged;
            inputModeService.InputModeChanged -= cachedInputModeHandler;
            inputModeService.InputModeChanged += cachedInputModeHandler;
        }

        private void Unsubscribe()
        {
            if (inputModeService != null && cachedInputModeHandler != null)
            {
                inputModeService.InputModeChanged -= cachedInputModeHandler;
            }
        }

        private void HandleInputModeChanged(InputMode _)
        {
            RefreshVisibility();
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject root, Image bg, TMP_Text text, string label)
        {
            visualRoot = root;
            backgroundImage = bg;
            keyText = text;
            keyLabel = label;
            ApplyTheme();
            SyncLabel();
            if (visualRoot != null) visualRoot.SetActive(false);
        }
#endif
    }
}
