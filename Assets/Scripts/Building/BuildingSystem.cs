using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Inventory;

namespace Pangaea.Building
{
    /// <summary>
    /// Base building system - walls, structures, and territory control.
    /// Includes offline raid protection.
    /// </summary>
    public class BuildingSystem : MonoBehaviour
    {
        public static BuildingSystem Instance { get; private set; }

        [Header("Building Settings")]
        [SerializeField] private float maxBuildDistance = 10f;
        [SerializeField] private float snapDistance = 1f;
        [SerializeField] private LayerMask buildableLayerMask;
        [SerializeField] private LayerMask obstacleLayerMask;

        [Header("Preview")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        // Building state
        private BuildingPiece currentBlueprintType;
        private GameObject previewObject;
        private bool isPlacementValid = false;
        private PlayerController localPlayer;

        // All placed buildings (server would track these)
        private Dictionary<uint, PlacedBuilding> allBuildings = new Dictionary<uint, PlacedBuilding>();
        private uint nextBuildingId = 1;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (currentBlueprintType != null)
            {
                UpdatePreview();
                HandlePlacementInput();
            }
        }

        public void StartBuilding(BuildingPiece blueprint, PlayerController player)
        {
            localPlayer = player;
            currentBlueprintType = blueprint;

            // Create preview object
            if (previewObject != null)
            {
                Destroy(previewObject);
            }

            previewObject = Instantiate(blueprint.previewPrefab);
            previewObject.layer = LayerMask.NameToLayer("Preview");

            // Disable colliders on preview
            foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            Debug.Log($"[Building] Started placing: {blueprint.pieceName}");
        }

        public void CancelBuilding()
        {
            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
            }

            currentBlueprintType = null;
        }

        private void UpdatePreview()
        {
            if (previewObject == null || localPlayer == null) return;

            // Raycast from camera to find placement position
            Camera cam = Camera.main;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            // For mobile, use center of screen or touch position
            #if UNITY_IOS || UNITY_ANDROID
            if (Input.touchCount > 0)
            {
                ray = cam.ScreenPointToRay(Input.GetTouch(0).position);
            }
            else
            {
                ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            }
            #endif

            if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance * 2, buildableLayerMask))
            {
                Vector3 placementPos = hit.point;

                // Snap to grid
                placementPos = SnapToGrid(placementPos, currentBlueprintType.gridSize);

                // Check for snapping to existing structures
                PlacedBuilding nearbyStructure = FindNearbySnapPoint(placementPos);
                if (nearbyStructure != null)
                {
                    placementPos = GetSnapPosition(nearbyStructure, currentBlueprintType);
                }

                previewObject.transform.position = placementPos;

                // Rotate with player input
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Q))
                {
                    float rotationAmount = Input.GetKeyDown(KeyCode.Q) ? -90f : 90f;
                    previewObject.transform.Rotate(Vector3.up, rotationAmount);
                }

                // Check validity
                isPlacementValid = CheckPlacementValidity(placementPos);
                UpdatePreviewMaterial();
            }
        }

        private Vector3 SnapToGrid(Vector3 position, float gridSize)
        {
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                position.y,
                Mathf.Round(position.z / gridSize) * gridSize
            );
        }

        private PlacedBuilding FindNearbySnapPoint(Vector3 position)
        {
            float closestDist = snapDistance;
            PlacedBuilding closest = null;

            foreach (var building in allBuildings.Values)
            {
                float dist = Vector3.Distance(position, building.Position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = building;
                }
            }

            return closest;
        }

        private Vector3 GetSnapPosition(PlacedBuilding nearbyBuilding, BuildingPiece piece)
        {
            // Snap to attachment points
            foreach (var point in nearbyBuilding.Piece.attachmentPoints)
            {
                Vector3 worldPoint = nearbyBuilding.Transform.TransformPoint(point.localPosition);
                float dist = Vector3.Distance(previewObject.transform.position, worldPoint);

                if (dist < snapDistance && point.acceptedTypes.Contains(piece.pieceType))
                {
                    return worldPoint;
                }
            }

            return previewObject.transform.position;
        }

        private bool CheckPlacementValidity(Vector3 position)
        {
            // Check distance from player
            float distToPlayer = Vector3.Distance(position, localPlayer.transform.position);
            if (distToPlayer > maxBuildDistance)
            {
                return false;
            }

            // Check for overlapping obstacles
            Bounds bounds = GetPieceBounds();
            Collider[] overlaps = Physics.OverlapBox(position + bounds.center, bounds.extents * 0.9f, previewObject.transform.rotation, obstacleLayerMask);
            if (overlaps.Length > 0)
            {
                return false;
            }

            // Check terrain slope (optional)
            // Check building permissions (territory, clan, etc.)

            return true;
        }

        private Bounds GetPieceBounds()
        {
            Renderer renderer = previewObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }
            return new Bounds(Vector3.zero, Vector3.one);
        }

        private void UpdatePreviewMaterial()
        {
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
            Material mat = isPlacementValid ? validPreviewMaterial : invalidPreviewMaterial;

            foreach (var renderer in renderers)
            {
                renderer.material = mat;
            }
        }

        private void HandlePlacementInput()
        {
            // Place on click/tap
            bool placeInput = Input.GetMouseButtonDown(0);

            #if UNITY_IOS || UNITY_ANDROID
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                // Check if touch is not on UI
                placeInput = true;
            }
            #endif

            if (placeInput && isPlacementValid)
            {
                PlaceBuilding();
            }

            // Cancel on right click or back button
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelBuilding();
            }
        }

        private void PlaceBuilding()
        {
            // Check if player has resources
            if (!HasRequiredResources(currentBlueprintType))
            {
                Debug.Log("[Building] Not enough resources");
                return;
            }

            // Consume resources
            ConsumeResources(currentBlueprintType);

            // Create placed building
            Vector3 position = previewObject.transform.position;
            Quaternion rotation = previewObject.transform.rotation;

            GameObject buildingObj = Instantiate(currentBlueprintType.placedPrefab, position, rotation);

            uint buildingId = nextBuildingId++;
            PlacedBuilding placed = new PlacedBuilding
            {
                BuildingId = buildingId,
                Piece = currentBlueprintType,
                Position = position,
                Rotation = rotation,
                Transform = buildingObj.transform,
                OwnerId = localPlayer.PlayerId,
                ClanId = 0, // Would get from player's clan
                Health = currentBlueprintType.maxHealth,
                PlacedTime = Time.time
            };

            allBuildings[buildingId] = placed;

            // Add building component
            BuildingHealth health = buildingObj.AddComponent<BuildingHealth>();
            health.Initialize(placed);

            Debug.Log($"[Building] Placed {currentBlueprintType.pieceName} at {position}");

            // Continue building same type or stop
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                CancelBuilding();
            }
        }

        private bool HasRequiredResources(BuildingPiece piece)
        {
            foreach (var cost in piece.resourceCosts)
            {
                if (!localPlayer.Inventory.HasItem(cost.item, cost.quantity))
                {
                    return false;
                }
            }
            return true;
        }

        private void ConsumeResources(BuildingPiece piece)
        {
            foreach (var cost in piece.resourceCosts)
            {
                localPlayer.Inventory.RemoveItem(cost.item, cost.quantity);
            }
        }

        public PlacedBuilding GetBuilding(uint buildingId)
        {
            allBuildings.TryGetValue(buildingId, out PlacedBuilding building);
            return building;
        }

        public List<PlacedBuilding> GetPlayerBuildings(uint playerId)
        {
            List<PlacedBuilding> result = new List<PlacedBuilding>();
            foreach (var building in allBuildings.Values)
            {
                if (building.OwnerId == playerId)
                {
                    result.Add(building);
                }
            }
            return result;
        }

        public void DestroyBuilding(uint buildingId)
        {
            if (allBuildings.TryGetValue(buildingId, out PlacedBuilding building))
            {
                if (building.Transform != null)
                {
                    Destroy(building.Transform.gameObject);
                }
                allBuildings.Remove(buildingId);
            }
        }
    }

    public class PlacedBuilding
    {
        public uint BuildingId;
        public BuildingPiece Piece;
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Transform;
        public uint OwnerId;
        public uint ClanId;
        public float Health;
        public float PlacedTime;
        public bool IsProtected; // Offline raid protection
    }
}
