using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Pangaea.UI
{
    /// <summary>
    /// Mobile virtual joystick and action buttons.
    /// Add this to a Canvas for touch controls.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("Joystick")]
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        [SerializeField] private float joystickRange = 50f;

        [Header("Action Buttons")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button interactButton;
        [SerializeField] private Button inventoryButton;

        [Header("Settings")]
        [SerializeField] private bool showOnDesktop = false;

        // Joystick state
        private Vector2 joystickInput;
        private bool joystickActive = false;
        private int joystickTouchId = -1;
        private Vector2 joystickStartPos;

        // Button states (consumed by PlayerInput)
        public bool AttackPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool InventoryPressed { get; private set; }

        public static MobileControls Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            // Hide on desktop unless forced
            #if !UNITY_IOS && !UNITY_ANDROID
            if (!showOnDesktop)
            {
                gameObject.SetActive(false);
                return;
            }
            #endif

            SetupButtons();
        }

        private void SetupButtons()
        {
            if (attackButton != null)
            {
                attackButton.onClick.AddListener(() => AttackPressed = true);
            }

            if (interactButton != null)
            {
                interactButton.onClick.AddListener(() => InteractPressed = true);
            }

            if (inventoryButton != null)
            {
                inventoryButton.onClick.AddListener(() => InventoryPressed = true);
            }
        }

        private void Update()
        {
            HandleJoystickInput();
        }

        private void LateUpdate()
        {
            // Reset button states after frame (so they're consumed once)
            AttackPressed = false;
            InteractPressed = false;
            InventoryPressed = false;
        }

        private void HandleJoystickInput()
        {
            if (joystickBackground == null || joystickHandle == null) return;

            // Check for touches
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Is this touch on the left side of screen (joystick area)?
                if (touch.position.x < Screen.width * 0.4f)
                {
                    switch (touch.phase)
                    {
                        case TouchPhase.Began:
                            if (!joystickActive)
                            {
                                joystickActive = true;
                                joystickTouchId = touch.fingerId;
                                joystickStartPos = touch.position;

                                // Move joystick background to touch position
                                joystickBackground.position = touch.position;
                            }
                            break;

                        case TouchPhase.Moved:
                        case TouchPhase.Stationary:
                            if (joystickActive && touch.fingerId == joystickTouchId)
                            {
                                Vector2 delta = touch.position - joystickStartPos;
                                delta = Vector2.ClampMagnitude(delta, joystickRange);

                                // Move handle
                                joystickHandle.anchoredPosition = delta;

                                // Calculate input (-1 to 1)
                                joystickInput = delta / joystickRange;
                            }
                            break;

                        case TouchPhase.Ended:
                        case TouchPhase.Canceled:
                            if (touch.fingerId == joystickTouchId)
                            {
                                ResetJoystick();
                            }
                            break;
                    }
                }
            }

            // Mouse fallback for testing
            #if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width * 0.4f)
            {
                joystickActive = true;
                joystickStartPos = Input.mousePosition;
                joystickBackground.position = Input.mousePosition;
            }

            if (Input.GetMouseButton(0) && joystickActive)
            {
                Vector2 delta = (Vector2)Input.mousePosition - joystickStartPos;
                delta = Vector2.ClampMagnitude(delta, joystickRange);
                joystickHandle.anchoredPosition = delta;
                joystickInput = delta / joystickRange;
            }

            if (Input.GetMouseButtonUp(0))
            {
                ResetJoystick();
            }
            #endif
        }

        private void ResetJoystick()
        {
            joystickActive = false;
            joystickTouchId = -1;
            joystickInput = Vector2.zero;

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }
        }

        public Vector2 GetJoystickInput()
        {
            return joystickInput;
        }

        public bool IsJoystickActive()
        {
            return joystickActive;
        }

        public bool IsRunning()
        {
            // Running = joystick pushed to edge
            return joystickInput.magnitude > 0.9f;
        }
    }
}
