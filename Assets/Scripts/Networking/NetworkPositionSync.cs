using UnityEngine;

namespace Pangaea.Networking
{
    /// <summary>
    /// Smooth position interpolation for networked players.
    /// Handles prediction and lag compensation.
    /// </summary>
    public class NetworkPositionSync : MonoBehaviour
    {
        [Header("Interpolation")]
        [SerializeField] private float positionLerpSpeed = 15f;
        [SerializeField] private float rotationLerpSpeed = 15f;
        [SerializeField] private float snapDistance = 5f; // Teleport if too far

        [Header("Prediction")]
        [SerializeField] private bool usePrediction = true;
        [SerializeField] private float predictionTime = 0.1f;

        // State
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 lastPosition;
        private Vector3 velocity;
        private float lastUpdateTime;

        // Buffer for interpolation
        private readonly int BUFFER_SIZE = 20;
        private PositionSnapshot[] positionBuffer;
        private int bufferIndex = 0;

        private bool isLocalPlayer = false;

        private void Awake()
        {
            positionBuffer = new PositionSnapshot[BUFFER_SIZE];
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            lastPosition = transform.position;
        }

        public void Initialize(bool local)
        {
            isLocalPlayer = local;
        }

        private void Update()
        {
            if (isLocalPlayer) return;

            InterpolatePosition();
            InterpolateRotation();
        }

        public void SetTargetPosition(Vector3 position, Quaternion rotation)
        {
            // Calculate velocity from last update
            float deltaTime = Time.time - lastUpdateTime;
            if (deltaTime > 0)
            {
                velocity = (position - lastPosition) / deltaTime;
            }

            // Store in buffer
            positionBuffer[bufferIndex] = new PositionSnapshot
            {
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                Timestamp = Time.time
            };
            bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;

            lastPosition = targetPosition;
            targetPosition = position;
            targetRotation = rotation;
            lastUpdateTime = Time.time;
        }

        private void InterpolatePosition()
        {
            Vector3 predicted = targetPosition;

            // Apply prediction
            if (usePrediction)
            {
                float timeSinceUpdate = Time.time - lastUpdateTime;
                if (timeSinceUpdate < predictionTime)
                {
                    predicted = targetPosition + velocity * timeSinceUpdate;
                }
            }

            // Check for snap
            float distance = Vector3.Distance(transform.position, predicted);
            if (distance > snapDistance)
            {
                transform.position = predicted;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, predicted, positionLerpSpeed * Time.deltaTime);
            }
        }

        private void InterpolateRotation()
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
        }

        // For local player - sends updates
        private float lastSendTime;
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private const float MIN_SEND_INTERVAL = 0.05f;
        private const float POSITION_THRESHOLD = 0.1f;
        private const float ROTATION_THRESHOLD = 5f;

        public void TrySendUpdate()
        {
            if (!isLocalPlayer) return;
            if (Time.time - lastSendTime < MIN_SEND_INTERVAL) return;

            bool positionChanged = Vector3.Distance(transform.position, lastSentPosition) > POSITION_THRESHOLD;
            bool rotationChanged = Quaternion.Angle(transform.rotation, lastSentRotation) > ROTATION_THRESHOLD;

            if (positionChanged || rotationChanged)
            {
                NetworkManager.Instance?.SendPositionUpdate(transform.position, transform.rotation);

                lastSendTime = Time.time;
                lastSentPosition = transform.position;
                lastSentRotation = transform.rotation;
            }
        }
    }

    public struct PositionSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float Timestamp;
    }
}
