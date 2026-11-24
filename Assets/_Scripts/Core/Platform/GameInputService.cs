using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace ProjectB.Core
{
    /// <summary>
    /// Project-wide wrapper around <c>Resources/ProjectB_InputActions</c> (Singleton in <see cref="RootLifetimeScope"/>, Sprint 14.3).
    /// Instantiates an isolated runtime copy so it never conflicts with <c>InputSystemUIInputModule</c>'s enable/disable lifecycle,
    /// caches action references, and exposes Zero-GC per-frame queries.
    /// </summary>
    public sealed class GameInputService : IDisposable
    {
        public const string INPUT_ACTIONS_RESOURCE_PATH = "ProjectB_InputActions";

        private readonly InputActionAsset ownedAsset;
        private readonly InputActionMap gameplayMap;
        private readonly InputActionMap uiMap;

        public InputActionAsset ActionsAsset { get; }
        public InputAction MoveAction { get; }
        public InputAction UltimateAction { get; }
        public InputAction PauseAction { get; }
        public InputAction NavigateAction { get; }
        public InputAction SubmitAction { get; }
        public InputAction CancelAction { get; }
        public InputAction Card1Action { get; }
        public InputAction Card2Action { get; }
        public InputAction Card3Action { get; }
        public InputAction RerollAction { get; }

        [Inject]
        public GameInputService()
            : this(Resources.Load<InputActionAsset>(INPUT_ACTIONS_RESOURCE_PATH), cloneAsset: true)
        {
        }

        public GameInputService(InputActionAsset asset, bool cloneAsset = false)
        {
            if (asset == null)
            {
                ownedAsset = CreateFallbackAsset();
                ActionsAsset = ownedAsset;
            }
            else if (cloneAsset)
            {
                ownedAsset = UnityEngine.Object.Instantiate(asset);
                ownedAsset.hideFlags = HideFlags.HideAndDontSave;
                ActionsAsset = ownedAsset;
            }
            else
            {
                ActionsAsset = asset;
            }

            gameplayMap = ActionsAsset.FindActionMap("Gameplay", throwIfNotFound: false);
            uiMap = ActionsAsset.FindActionMap("UI", throwIfNotFound: false);

            if (gameplayMap != null)
            {
                MoveAction = gameplayMap.FindAction("Move", throwIfNotFound: false);
                UltimateAction = gameplayMap.FindAction("Ultimate", throwIfNotFound: false);
                PauseAction = gameplayMap.FindAction("Pause", throwIfNotFound: false);
                gameplayMap.Enable();
            }

            if (uiMap != null)
            {
                NavigateAction = uiMap.FindAction("Navigate", throwIfNotFound: false);
                SubmitAction = uiMap.FindAction("Submit", throwIfNotFound: false);
                CancelAction = uiMap.FindAction("Cancel", throwIfNotFound: false);
                Card1Action = uiMap.FindAction("Card1", throwIfNotFound: false);
                Card2Action = uiMap.FindAction("Card2", throwIfNotFound: false);
                Card3Action = uiMap.FindAction("Card3", throwIfNotFound: false);
                RerollAction = uiMap.FindAction("Reroll", throwIfNotFound: false);
                uiMap.Enable();
            }
        }

        public Vector2 ReadMove()
        {
            return MoveAction != null ? MoveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        public bool WasUltimatePressedThisFrame()
        {
            return UltimateAction != null && UltimateAction.WasPressedThisFrame();
        }

        public bool WasPausePressedThisFrame()
        {
            return PauseAction != null && PauseAction.WasPressedThisFrame();
        }

        public bool WasCancelPressedThisFrame()
        {
            return CancelAction != null && CancelAction.WasPressedThisFrame();
        }

        public bool WasCardPressedThisFrame(int index)
        {
            return index switch
            {
                0 => Card1Action != null && Card1Action.WasPressedThisFrame(),
                1 => Card2Action != null && Card2Action.WasPressedThisFrame(),
                2 => Card3Action != null && Card3Action.WasPressedThisFrame(),
                _ => false
            };
        }

        public bool WasRerollPressedThisFrame()
        {
            return RerollAction != null && RerollAction.WasPressedThisFrame();
        }

        public void Dispose()
        {
            gameplayMap?.Disable();
            uiMap?.Disable();

            if (ownedAsset != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(ownedAsset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(ownedAsset);
                }
            }
        }

        private static InputActionAsset CreateFallbackAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;

            InputActionMap gameplay = asset.AddActionMap("Gameplay");
            InputAction move = gameplay.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddBinding("<Gamepad>/leftStick");
            move.AddCompositeBinding("2DVector(mode=1)")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            InputAction ult = gameplay.AddAction("Ultimate", InputActionType.Button);
            ult.AddBinding("<Keyboard>/space");
            ult.AddBinding("<Gamepad>/buttonSouth");

            InputAction pause = gameplay.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            InputActionMap ui = asset.AddActionMap("UI");
            InputAction navigate = ui.AddAction("Navigate", InputActionType.PassThrough);
            navigate.expectedControlType = "Vector2";
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter");
            InputAction cancel = ui.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
            ui.AddAction("Card1", InputActionType.Button).AddBinding("<Keyboard>/1");
            ui.AddAction("Card2", InputActionType.Button).AddBinding("<Keyboard>/2");
            ui.AddAction("Card3", InputActionType.Button).AddBinding("<Keyboard>/3");
            ui.AddAction("Reroll", InputActionType.Button).AddBinding("<Keyboard>/r");

            return asset;
        }
    }
}
