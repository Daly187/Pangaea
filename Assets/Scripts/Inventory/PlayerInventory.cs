using UnityEngine;
using System;
using System.Collections.Generic;
using Pangaea.Player;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Player inventory system - PUBG-style limited capacity.
    /// Items drop on death (except soulbound cosmetics).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField] private float maxWeight = 50f;
        [SerializeField] private int maxSlots = 30;

        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlots equipment;

        [Header("Quick Slots")]
        [SerializeField] private int quickSlotCount = 5;

        // Inventory data
        private List<ItemStack> items = new List<ItemStack>();
        private ItemStack[] quickSlots;
        private float currentWeight = 0f;

        // Events
        public event Action OnInventoryChanged;
        public event Action<EquipmentSlotType, Item> OnEquipmentChanged;

        // Properties
        public float CurrentWeight => currentWeight;
        public float MaxWeight => maxWeight;
        public int ItemCount => items.Count;
        public IReadOnlyList<ItemStack> Items => items;
        public EquipmentSlots Equipment => equipment;

        private PlayerStats stats;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            quickSlots = new ItemStack[quickSlotCount];
            equipment = new EquipmentSlots();
        }

        public bool CanAddItem(Item item, int quantity = 1)
        {
            float additionalWeight = item.weight * quantity;

            // Check weight
            if (currentWeight + additionalWeight > GetMaxWeight())
                return false;

            // Check if can stack with existing
            if (item.isStackable)
            {
                ItemStack existing = FindStack(item);
                if (existing != null && existing.Quantity + quantity <= item.maxStackSize)
                    return true;
            }

            // Check slot availability
            return items.Count < maxSlots;
        }

        public bool AddItem(Item item, int quantity = 1)
        {
            if (!CanAddItem(item, quantity))
            {
                Debug.Log($"[Inventory] Cannot add {item.itemName} - inventory full or overweight");
                return false;
            }

            // Try stacking first
            if (item.isStackable)
            {
                ItemStack existing = FindStack(item);
                if (existing != null)
                {
                    int spaceInStack = item.maxStackSize - existing.Quantity;
                    int toAdd = Mathf.Min(quantity, spaceInStack);
                    existing.Quantity += toAdd;
                    quantity -= toAdd;

                    currentWeight += item.weight * toAdd;
                }
            }

            // Create new stacks for remainder
            while (quantity > 0 && items.Count < maxSlots)
            {
                int stackSize = Mathf.Min(quantity, item.maxStackSize);
                items.Add(new ItemStack(item, stackSize));
                quantity -= stackSize;
                currentWeight += item.weight * stackSize;
            }

            OnInventoryChanged?.Invoke();
            return quantity == 0;
        }

        public bool RemoveItem(Item item, int quantity = 1)
        {
            int remaining = quantity;

            for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (items[i].Item.itemId == item.itemId)
                {
                    int toRemove = Mathf.Min(remaining, items[i].Quantity);
                    items[i].Quantity -= toRemove;
                    remaining -= toRemove;
                    currentWeight -= item.weight * toRemove;

                    if (items[i].Quantity <= 0)
                    {
                        items.RemoveAt(i);
                    }
                }
            }

            OnInventoryChanged?.Invoke();
            return remaining == 0;
        }

        public int GetItemCount(Item item)
        {
            int count = 0;
            foreach (var stack in items)
            {
                if (stack.Item.itemId == item.itemId)
                    count += stack.Quantity;
            }
            return count;
        }

        public bool HasItem(Item item, int quantity = 1)
        {
            return GetItemCount(item) >= quantity;
        }

        private ItemStack FindStack(Item item)
        {
            foreach (var stack in items)
            {
                if (stack.Item.itemId == item.itemId && stack.Quantity < item.maxStackSize)
                    return stack;
            }
            return null;
        }

        private float GetMaxWeight()
        {
            // Base + strength bonus
            float bonus = stats != null ? stats.Attributes.Strength * 5f : 0f;
            return maxWeight + bonus;
        }

        // Equipment
        public bool Equip(Item item)
        {
            if (item is WeaponItem weapon)
            {
                if (!weapon.CanEquip(stats))
                {
                    Debug.Log($"[Inventory] Cannot equip {weapon.itemName} - requirements not met");
                    return false;
                }

                // Unequip current weapon
                if (equipment.Weapon != null)
                {
                    AddItem(equipment.Weapon);
                }

                equipment.Weapon = weapon;
                RemoveItem(weapon);
                OnEquipmentChanged?.Invoke(EquipmentSlotType.Weapon, weapon);
                return true;
            }

            if (item is ArmorItem armor)
            {
                if (!armor.CanEquip(stats))
                    return false;

                // Get current armor in slot
                ArmorItem current = GetEquippedArmor(armor.slot);
                if (current != null)
                {
                    AddItem(current);
                }

                SetEquippedArmor(armor);
                RemoveItem(armor);
                OnEquipmentChanged?.Invoke(GetSlotType(armor.slot), armor);
                return true;
            }

            return false;
        }

        public void Unequip(EquipmentSlotType slot)
        {
            Item item = GetEquipped(slot);
            if (item == null) return;

            if (!CanAddItem(item))
            {
                Debug.Log("[Inventory] Cannot unequip - inventory full");
                return;
            }

            AddItem(item);
            ClearSlot(slot);
            OnEquipmentChanged?.Invoke(slot, null);
        }

        private ArmorItem GetEquippedArmor(ArmorSlot slot)
        {
            return slot switch
            {
                ArmorSlot.Head => equipment.Head,
                ArmorSlot.Chest => equipment.Chest,
                ArmorSlot.Legs => equipment.Legs,
                ArmorSlot.Feet => equipment.Feet,
                ArmorSlot.Hands => equipment.Hands,
                ArmorSlot.Back => equipment.Back,
                _ => null
            };
        }

        private void SetEquippedArmor(ArmorItem armor)
        {
            switch (armor.slot)
            {
                case ArmorSlot.Head: equipment.Head = armor; break;
                case ArmorSlot.Chest: equipment.Chest = armor; break;
                case ArmorSlot.Legs: equipment.Legs = armor; break;
                case ArmorSlot.Feet: equipment.Feet = armor; break;
                case ArmorSlot.Hands: equipment.Hands = armor; break;
                case ArmorSlot.Back: equipment.Back = armor; break;
            }
        }

        private Item GetEquipped(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.Weapon => equipment.Weapon,
                EquipmentSlotType.Head => equipment.Head,
                EquipmentSlotType.Chest => equipment.Chest,
                EquipmentSlotType.Legs => equipment.Legs,
                EquipmentSlotType.Feet => equipment.Feet,
                EquipmentSlotType.Hands => equipment.Hands,
                EquipmentSlotType.Back => equipment.Back,
                _ => null
            };
        }

        private void ClearSlot(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Weapon: equipment.Weapon = null; break;
                case EquipmentSlotType.Head: equipment.Head = null; break;
                case EquipmentSlotType.Chest: equipment.Chest = null; break;
                case EquipmentSlotType.Legs: equipment.Legs = null; break;
                case EquipmentSlotType.Feet: equipment.Feet = null; break;
                case EquipmentSlotType.Hands: equipment.Hands = null; break;
                case EquipmentSlotType.Back: equipment.Back = null; break;
            }
        }

        private EquipmentSlotType GetSlotType(ArmorSlot armorSlot)
        {
            return armorSlot switch
            {
                ArmorSlot.Head => EquipmentSlotType.Head,
                ArmorSlot.Chest => EquipmentSlotType.Chest,
                ArmorSlot.Legs => EquipmentSlotType.Legs,
                ArmorSlot.Feet => EquipmentSlotType.Feet,
                ArmorSlot.Hands => EquipmentSlotType.Hands,
                ArmorSlot.Back => EquipmentSlotType.Back,
                _ => EquipmentSlotType.Chest
            };
        }

        // Death handling
        public void DropAllItems(Vector3 position)
        {
            List<ItemStack> toDrop = new List<ItemStack>();

            foreach (var stack in items)
            {
                // Soulbound items (cosmetics) don't drop
                if (!stack.Item.isSoulbound && stack.Item.isDroppable)
                {
                    toDrop.Add(stack);
                }
            }

            // Drop equipment
            DropEquipment(position);

            // Clear inventory of dropped items
            foreach (var stack in toDrop)
            {
                SpawnDroppedItem(stack, position);
            }

            items.RemoveAll(s => !s.Item.isSoulbound && s.Item.isDroppable);
            currentWeight = CalculateWeight();

            OnInventoryChanged?.Invoke();
        }

        private void DropEquipment(Vector3 position)
        {
            if (equipment.Weapon != null && equipment.Weapon.isDroppable)
            {
                SpawnDroppedItem(new ItemStack(equipment.Weapon, 1), position);
                equipment.Weapon = null;
            }

            // Drop all armor pieces
            DropArmorPiece(equipment.Head, position); equipment.Head = null;
            DropArmorPiece(equipment.Chest, position); equipment.Chest = null;
            DropArmorPiece(equipment.Legs, position); equipment.Legs = null;
            DropArmorPiece(equipment.Feet, position); equipment.Feet = null;
            DropArmorPiece(equipment.Hands, position); equipment.Hands = null;
            DropArmorPiece(equipment.Back, position); equipment.Back = null;
        }

        private void DropArmorPiece(ArmorItem armor, Vector3 position)
        {
            if (armor != null && armor.isDroppable)
            {
                SpawnDroppedItem(new ItemStack(armor, 1), position);
            }
        }

        private void SpawnDroppedItem(ItemStack stack, Vector3 position)
        {
            // Create world item at position
            // Randomize position slightly
            Vector3 dropPos = position + new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                0.5f,
                UnityEngine.Random.Range(-2f, 2f)
            );

            // Would instantiate DroppedItem prefab here
            Debug.Log($"[Inventory] Dropped {stack.Quantity}x {stack.Item.itemName} at {dropPos}");
        }

        private float CalculateWeight()
        {
            float weight = 0f;
            foreach (var stack in items)
            {
                weight += stack.Item.weight * stack.Quantity;
            }
            return weight;
        }

        // Quick slots
        public void SetQuickSlot(int slot, ItemStack item)
        {
            if (slot >= 0 && slot < quickSlotCount)
            {
                quickSlots[slot] = item;
            }
        }

        public ItemStack GetQuickSlot(int slot)
        {
            if (slot >= 0 && slot < quickSlotCount)
            {
                return quickSlots[slot];
            }
            return null;
        }

        public void UseQuickSlot(int slot)
        {
            ItemStack stack = GetQuickSlot(slot);
            if (stack != null && stack.Item != null)
            {
                stack.Item.Use(GetComponent<PlayerController>());

                if (stack.Item.itemType == ItemType.Consumable)
                {
                    RemoveItem(stack.Item, 1);
                    if (GetItemCount(stack.Item) <= 0)
                    {
                        quickSlots[slot] = null;
                    }
                }
            }
        }
    }

    [Serializable]
    public class ItemStack
    {
        public Item Item;
        public int Quantity;
        public int CurrentDurability;

        public ItemStack(Item item, int quantity)
        {
            Item = item;
            Quantity = quantity;

            // Set durability for equipment
            if (item is WeaponItem weapon)
                CurrentDurability = weapon.maxDurability;
            else if (item is ArmorItem armor)
                CurrentDurability = armor.maxDurability;
        }
    }

    [Serializable]
    public class EquipmentSlots
    {
        public WeaponItem Weapon;
        public ArmorItem Head;
        public ArmorItem Chest;
        public ArmorItem Legs;
        public ArmorItem Feet;
        public ArmorItem Hands;
        public ArmorItem Back;

        public float GetTotalDefense()
        {
            float defense = 0f;
            if (Head != null) defense += Head.defense;
            if (Chest != null) defense += Chest.defense;
            if (Legs != null) defense += Legs.defense;
            if (Feet != null) defense += Feet.defense;
            if (Hands != null) defense += Hands.defense;
            if (Back != null) defense += Back.defense;
            return defense;
        }

        public float GetTotalMovementPenalty()
        {
            float penalty = 0f;
            if (Head != null) penalty += Head.movementPenalty;
            if (Chest != null) penalty += Chest.movementPenalty;
            if (Legs != null) penalty += Legs.movementPenalty;
            if (Feet != null) penalty += Feet.movementPenalty;
            if (Hands != null) penalty += Hands.movementPenalty;
            if (Back != null) penalty += Back.movementPenalty;
            return Mathf.Min(penalty, 0.5f); // Cap at 50% slowdown
        }
    }

    public enum EquipmentSlotType
    {
        Weapon,
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        Back
    }
}
