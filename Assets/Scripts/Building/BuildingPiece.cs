using UnityEngine;
using System.Collections.Generic;
using Pangaea.Inventory;

namespace Pangaea.Building
{
    /// <summary>
    /// Building piece definition - walls, floors, doors, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "New Building Piece", menuName = "Pangaea/Building/Building Piece")]
    public class BuildingPiece : ScriptableObject
    {
        [Header("Basic Info")]
        public string pieceId;
        public string pieceName;
        [TextArea]
        public string description;
        public Sprite icon;
        public BuildingPieceType pieceType;

        [Header("Prefabs")]
        public GameObject previewPrefab;
        public GameObject placedPrefab;
        public GameObject damagedPrefab;
        public GameObject destroyedEffect;

        [Header("Stats")]
        public float maxHealth = 500f;
        public float repairCostMultiplier = 0.5f; // 50% of build cost to repair

        [Header("Placement")]
        public float gridSize = 1f;
        public bool requiresFoundation = false;
        public bool canRotate = true;
        public int rotationSteps = 4; // 90 degree increments

        [Header("Attachment Points")]
        public List<AttachmentPoint> attachmentPoints;

        [Header("Crafting Requirements")]
        public Profession requiredProfession = Profession.None;
        public int requiredBuildingLevel = 0;
        public List<CraftingIngredient> resourceCosts;

        [Header("Upgrade")]
        public BuildingPiece upgradeTo;
        public List<CraftingIngredient> upgradeCosts;

        public int GetTotalResourceCost()
        {
            int total = 0;
            foreach (var cost in resourceCosts)
            {
                total += cost.quantity;
            }
            return total;
        }
    }

    public enum BuildingPieceType
    {
        Foundation,
        Floor,
        Wall,
        DoorFrame,
        Door,
        Window,
        Roof,
        Stairs,
        Pillar,
        Fence,
        Trap,
        Storage,
        CraftingStation,
        Decoration
    }

    [System.Serializable]
    public class AttachmentPoint
    {
        public string pointName;
        public Vector3 localPosition;
        public Vector3 localRotation;
        public List<BuildingPieceType> acceptedTypes;
    }
}
