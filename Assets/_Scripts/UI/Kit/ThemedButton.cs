using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using ProjectB.Core;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    public enum ButtonSound
    {
        Click = 0,
        Confirm = 1,
        None = 2
    }

    /// <summary>
    /// Toy-like themed button (Sprint 13.1): face tinted by <see cref="ButtonStyle"/> from <see cref="UiTheme"/>,
    /// press-scale 0.94, hover/select scale (Sprint 14.3), click / confirm SFX, disabled state (dimmed face + label)
    /// and an error buzz + shake when a disabled button is tapped (e.g. not enough coins). Works on top of a regular <see cref="Button"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class ThemedButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private ButtonStyle style = ButtonStyle.Primary;
        [SerializeField] private Image faceImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image icon;
        [SerializeField] private ButtonSound sound = ButtonSound.Click;
        [SerializeField] private bool errorFeedbackWhenDisabled = true;
        [SerializeField] private bool tintIcon = true;
        [SerializeField] private bool keepCustomSprite;
        [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.04f;

        private Button button;
        private RectTransform rectTransform;
        private InputModeService inputModeService;
        private InputConfig inputConfig;
        private bool lastInteractable = true;
        private bool pressed;
        private bool hovered;
        private bool selected;
        private Vector3 restScale = Vector3.one;

        public ButtonStyle Style => style;
        public Button Button => button != null ? button : (button = GetComponent<Button>());
        public TMP_Text Label => label;
        public Image Face => faceImage;
        public Image Icon => icon;
        public bool IsHovered => hovered;
        public bool IsSelected => selected;

        private float EffectiveHoverScale => inputConfig != null ? inputConfig.buttonHoverScale : hoverScale;

        private void Awake()
        {
            button = GetComponent<Button>();
            rectTransform = transform as RectTransform;
            if (faceImage == null) faceImage = button.targetGraphic as Image;
            restScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            ApplyTheme(UiTheme.Current);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (inputModeService == null) RootServices.TryResolve(out inputModeService);
                if (inputConfig == null) RootServices.TryResolve(out inputConfig);
            }
            lastInteractable = Button.interactable;
            ApplyInteractableVisuals(lastInteractable);
        }

        private void OnDisable()
        {
            pressed = false;
            hovered = false;
            selected = false;
            transform.localScale = restScale;
        }

        private void LateUpdate()
        {
            bool interactable = button.interactable;
            if (interactable != lastInteractable)
            {
                lastInteractable = interactable;
                ApplyInteractableVisuals(interactable);
            }
        }

        public void SetStyle(ButtonStyle newStyle)
        {
            style = newStyle;
            ApplyTheme(UiTheme.Current);
        }

        /// <summary>Re-tints face / label / icon from the theme (editor applier and runtime).</summary>
        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            if (button == null) button = GetComponent<Button>();
            if (faceImage == null && button != null) faceImage = button.targetGraphic as Image;

            ButtonStyleColors colors = theme.GetButtonStyle(style);
            if (faceImage != null)
            {
                faceImage.color = theme.GetColor(colors.face);
                if (!keepCustomSprite)
                {
                    Sprite faceSprite = style == ButtonStyle.Icon ? theme.circleSprite : theme.buttonSprite;
                    if (faceSprite != null)
                    {
                        faceImage.sprite = faceSprite;
                        faceImage.type = style == ButtonStyle.Icon ? Image.Type.Simple : Image.Type.Sliced;
                    }
                }
            }

            Color labelColor = theme.GetColor(colors.label);
            if (label != null) label.color = labelColor;
            if (icon != null && tintIcon) icon.color = labelColor;

            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock block = button.colors;
                block.normalColor = Color.white;
                block.highlightedColor = theme.hoverTint;
                block.selectedColor = Color.Lerp(Color.white, theme.gold, 0.25f);
                block.pressedColor = theme.pressedTint;
                block.disabledColor = theme.disabledTint;
                block.colorMultiplier = 1f;
                block.fadeDuration = theme.durationFast * 0.5f;
                button.colors = block;
                if (faceImage != null) button.targetGraphic = faceImage;
            }

            ApplyInteractableVisuals(button == null || button.interactable);
        }

        private void ApplyInteractableVisuals(bool interactable)
        {
            if (label != null)
            {
                Color c = label.color;
                c.a = interactable ? 1f : 0.55f;
                label.color = c;
            }
            if (icon != null)
            {
                Color c = icon.color;
                c.a = interactable ? 1f : 0.55f;
                icon.color = c;
            }
            if (!interactable)
            {
                hovered = false;
                selected = false;
                pressed = false;
                transform.localScale = restScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Button.interactable) return;
            if (inputModeService != null && inputModeService.Mode == InputMode.Touch) return;
            hovered = true;
            if (!pressed) ApplyHoverOrRestScale();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Button.interactable) return;
            pressed = true;
            float s = UiTheme.Current.pressScale;
            UiTween.Kill(transform, complete: false);
            UiTween.Scale(transform, transform.localScale, restScale * s, UiTheme.Current.durationFast * 0.6f, 0f, UiEase.OutCubic);
            if (!Application.isPlaying) transform.localScale = restScale * s;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (pressed) Release();
            else ApplyHoverOrRestScale();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!Button.interactable) return;
            if (inputModeService != null && inputModeService.Mode == InputMode.Touch) return;
            selected = true;
            if (!pressed) ApplyHoverOrRestScale();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            if (!pressed) ApplyHoverOrRestScale();
        }

        private void ApplyHoverOrRestScale()
        {
            Vector3 target = (hovered || selected) ? restScale * EffectiveHoverScale : restScale;
            if (Application.isPlaying)
            {
                UiTween.Kill(transform, complete: false);
                UiTween.Scale(transform, transform.localScale, target, UiTheme.Current.durationFast, 0f, UiEase.OutCubic);
            }
            else
            {
                transform.localScale = target;
            }
        }

        private void Release()
        {
            if (!pressed) return;
            pressed = false;
            Vector3 target = (hovered || selected) ? restScale * EffectiveHoverScale : restScale;
            if (Application.isPlaying)
            {
                UiTween.Scale(transform, transform.localScale, target, UiTheme.Current.durationNormal, 0f, UiEase.OutBack);
            }
            else
            {
                transform.localScale = target;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayClickFeedback();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayClickFeedback();
        }

        private void PlayClickFeedback()
        {
            if (!Button.interactable)
            {
                if (errorFeedbackWhenDisabled)
                {
                    UiAudio.Error();
                    UiTween.Shake(rectTransform);
                }
                return;
            }

            switch (sound)
            {
                case ButtonSound.Click: UiAudio.Click(); break;
                case ButtonSound.Confirm: UiAudio.Confirm(); break;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(ButtonStyle newStyle, Image face, TMP_Text text, Image iconImage, ButtonSound buttonSound = ButtonSound.Click, bool customSprite = false)
        {
            keepCustomSprite = customSprite;
            style = newStyle;
            faceImage = face;
            label = text;
            icon = iconImage;
            sound = buttonSound;
            ApplyTheme(UiTheme.Current);
        }
#endif
    }
}
