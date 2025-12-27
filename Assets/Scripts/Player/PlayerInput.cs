using UnityEngine;

namespace Pangaea.Player
{
    /// <summary>
    /// Handles player input for both mobile (touch/joystick) and desktop (keyboard/mouse).
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] private float joystickDeadzone = 0.1f;
        [SerializeField] private bool useMobileControls = false;

        // Virtual joystick state (for mobile)
        private Vector2 joystickInput;
        private bool isTouchActive;
        private int movementTouchId = -1;
        private Vector2 joystickStartPos;

        // Button states
        private bool attackPressed;
        private bool interactPressed;
        private bool runHeld;
        private bool inventoryPressed;
        private bool mapPressed;

        private void Start()
        {
            // Detect platform
            #if UNITY_IOS || UNITY_ANDROID
            useMobileControls = true;
            #else
            useMobileControls = false;
            #endif
        }

        private void Update()
        {
            if (useMobileControls)
            {
                UpdateMobileInput();
            }
            else
            {
                UpdateDesktopInput();
            }
        }

        private void UpdateDesktopInput()
        {
            // Movement - WASD or Arrow keys
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            joystickInput = new Vector2(horizontal, vertical);

            // Actions
            runHeld = Input.GetKey(KeyCode.LeftShift);
            attackPressed = Input.GetMouseButtonDown(0);
            interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
            inventoryPressed = Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab);
            mapPressed = Input.GetKeyDown(KeyCode.M);
        }

        private void UpdateMobileInput()
        {
            // Reset button states
            attackPressed = false;
            interactPressed = false;
            inventoryPressed = false;

            // Handle touches
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Left side of screen = movement joystick
                if (touch.position.x < Screen.width * 0.4f)
                {
                    HandleMovementTouch(touch);
                }
                // Right side = actions
                else
                {
                    HandleActionTouch(touch);
                }
            }

            // Clear joystick if no movement touch
            if (movementTouchId == -1)
            {
                joystickInput = Vector2.zero;
            }
        }

        private void HandleMovementTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (movementTouchId == -1)
                    {
                        movementTouchId = touch.fingerId;
                        joystickStartPos = touch.position;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (touch.fingerId == movementTouchId)
                    {
                        Vector2 delta = touch.position - joystickStartPos;
                        float maxRadius = Screen.width * 0.15f;

                        joystickInput = delta / maxRadius;
                        joystickInput = Vector2.ClampMagnitude(joystickInput, 1f);

                        // Running if pushed to edge
                        runHeld = joystickInput.magnitude > 0.9f;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == movementTouchId)
                    {
                        movementTouchId = -1;
                        joystickInput = Vector2.zero;
                        runHeld = false;
                    }
                    break;
            }
        }

        private void HandleActionTouch(Touch touch)
        {
            if (touch.phase != TouchPhase.Began) return;

            // Determine action based on touch position
            float normalizedY = touch.position.y / Screen.height;
            float normalizedX = (touch.position.x - Screen.width * 0.4f) / (Screen.width * 0.6f);

            // Bottom right = attack
            if (normalizedY < 0.3f && normalizedX > 0.7f)
            {
                attackPressed = true;
            }
            // Middle right = interact
            else if (normalizedY < 0.5f && normalizedX > 0.7f)
            {
                interactPressed = true;
            }
            // Top right = inventory
            else if (normalizedY > 0.8f)
            {
                inventoryPressed = true;
            }
        }

        // Public input getters
        public Vector2 GetMovementInput()
        {
            if (joystickInput.magnitude < joystickDeadzone)
            {
                return Vector2.zero;
            }
            return joystickInput;
        }

        public bool IsRunning()
        {
            return runHeld;
        }

        public bool IsAttacking()
        {
            bool result = attackPressed;
            attackPressed = false; // Consume input
            return result;
        }

        public bool IsInteracting()
        {
            bool result = interactPressed;
            interactPressed = false;
            return result;
        }

        public bool OpenedInventory()
        {
            bool result = inventoryPressed;
            inventoryPressed = false;
            return result;
        }

        public bool OpenedMap()
        {
            bool result = mapPressed;
            mapPressed = false;
            return result;
        }

        public Vector2 GetLookDirection()
        {
            if (useMobileControls)
            {
                // On mobile, look in movement direction
                return joystickInput.normalized;
            }
            else
            {
                // On desktop, look toward mouse
                Vector3 mousePos = Input.mousePosition;
                Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
                Vector3 direction = (mousePos - screenCenter).normalized;
                return new Vector2(direction.x, direction.y);
            }
        }
    }
}
