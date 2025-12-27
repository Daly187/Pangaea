using UnityEngine;

namespace Pangaea.Player
{
    /// <summary>
    /// Isometric camera controller that follows the player.
    /// Handles zoom, rotation, and smooth following.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Isometric Settings")]
        [SerializeField] private float cameraAngle = 45f; // X rotation for isometric view
        [SerializeField] private float cameraRotation = 45f; // Y rotation
        [SerializeField] private float baseHeight = 15f;
        [SerializeField] private float baseDistance = 20f;

        [Header("Zoom")]
        [SerializeField] private float currentZoom = 1f;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float zoomSmoothTime = 0.2f;

        [Header("Following")]
        [SerializeField] private float followSmoothTime = 0.1f;
        [SerializeField] private Vector3 offset = Vector3.zero;

        // Internal state
        private Vector3 currentVelocity;
        private float zoomVelocity;
        private float targetZoom;
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            targetZoom = currentZoom;
        }

        private void Start()
        {
            if (target != null)
            {
                // Set initial position
                UpdateCameraPosition(true);
            }

            // Set isometric rotation
            UpdateCameraRotation();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleZoomInput();
            UpdateZoom();
            UpdateCameraPosition(false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                UpdateCameraPosition(true);
            }
        }

        private void HandleZoomInput()
        {
            // Mouse scroll wheel (desktop)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom -= scroll * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            // Pinch zoom (mobile)
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
                float currentMagnitude = (touch0.position - touch1.position).magnitude;

                float difference = prevMagnitude - currentMagnitude;
                targetZoom += difference * 0.01f * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }

        private void UpdateZoom()
        {
            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }

        private void UpdateCameraPosition(bool instant)
        {
            if (target == null) return;

            // Calculate offset based on angle and distance
            float height = baseHeight * currentZoom;
            float distance = baseDistance * currentZoom;

            Vector3 direction = Quaternion.Euler(cameraAngle, cameraRotation, 0) * Vector3.back;
            Vector3 targetPosition = target.position + offset + direction * distance;
            targetPosition.y = target.position.y + height;

            if (instant)
            {
                transform.position = targetPosition;
                currentVelocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, followSmoothTime);
            }
        }

        private void UpdateCameraRotation()
        {
            transform.rotation = Quaternion.Euler(cameraAngle, cameraRotation, 0);
        }

        public void SetZoom(float zoom)
        {
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        // Screen shake for combat feedback
        public void Shake(float intensity, float duration)
        {
            StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeRoutine(float intensity, float duration)
        {
            Vector3 originalOffset = offset;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                offset = originalOffset + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                intensity *= 0.95f; // Decay

                yield return null;
            }

            offset = originalOffset;
        }
    }
}
