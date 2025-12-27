using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Inventory;

namespace Pangaea.Building
{
    /// <summary>
    /// Storage container building - persists items in bases.
    /// </summary>
    public class StorageContainer : MonoBehaviour, IInteractable
    {
        [Header("Storage")]
        [SerializeField] private int maxSlots = 20;
        [SerializeField] private float maxWeight = 200f;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string lockCode = "";

        [Header("Access")]
        [SerializeField] private uint ownerId;
        [SerializeField] private uint clanId;
        [SerializeField] private AccessLevel accessLevel = AccessLevel.Owner;

        // Stored items
        private List<ItemStack> storedItems = new List<ItemStack>();
        private float currentWeight = 0f;

        // UI state
        private bool isOpen = false;
        private PlayerController currentUser;

        public string InteractionPrompt => isLocked ? "Unlock Storage" : "Open Storage";
        public int SlotCount => storedItems.Count;
        public int MaxSlots => maxSlots;

        public void Initialize(uint owner, uint clan = 0)
        {
            ownerId = owner;
            clanId = clan;
        }

        public void Interact(PlayerController player)
        {
            if (!CanAccess(player))
            {
                Debug.Log("[Storage] Access denied");
                return;
            }

            if (isLocked)
            {
                // Would show lock code input UI
                Debug.Log("[Storage] Enter lock code");
                return;
            }

            Open(player);
        }

        public bool CanAccess(PlayerController player)
        {
            switch (accessLevel)
            {
                case AccessLevel.Owner:
                    return player.PlayerId == ownerId;

                case AccessLevel.Clan:
                    // return player.ClanId == clanId || player.PlayerId == ownerId;
                    return player.PlayerId == ownerId;

                case AccessLevel.Anyone:
                    return true;

                default:
                    return false;
            }
        }

        public void Open(PlayerController player)
        {
            currentUser = player;
            isOpen = true;

            // Show storage UI
            Core.GameManager.Instance?.UIManager?.PushScreen(Core.UIScreen.Trading);
            Debug.Log($"[Storage] Opened by player {player.PlayerId}");
        }

        public void Close()
        {
            currentUser = null;
            isOpen = false;

            Core.GameManager.Instance?.UIManager?.PopScreen();
        }

        public bool AddItem(Item item, int quantity = 1)
        {
            float additionalWeight = item.weight * quantity;
            if (currentWeight + additionalWeight > maxWeight)
            {
                Debug.Log("[Storage] Storage full (weight)");
                return false;
            }

            // Try stacking
            if (item.isStackable)
            {
                foreach (var stack in storedItems)
                {
                    if (stack.Item.itemId == item.itemId && stack.Quantity < item.maxStackSize)
                    {
                        int canAdd = Mathf.Min(quantity, item.maxStackSize - stack.Quantity);
                        stack.Quantity += canAdd;
                        quantity -= canAdd;
                        currentWeight += item.weight * canAdd;

                        if (quantity == 0) return true;
                    }
                }
            }

            // Create new stacks
            while (quantity > 0 && storedItems.Count < maxSlots)
            {
                int stackSize = Mathf.Min(quantity, item.maxStackSize);
                storedItems.Add(new ItemStack(item, stackSize));
                quantity -= stackSize;
                currentWeight += item.weight * stackSize;
            }

            return quantity == 0;
        }

        public bool RemoveItem(Item item, int quantity = 1)
        {
            int remaining = quantity;

            for (int i = storedItems.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (storedItems[i].Item.itemId == item.itemId)
                {
                    int toRemove = Mathf.Min(remaining, storedItems[i].Quantity);
                    storedItems[i].Quantity -= toRemove;
                    remaining -= toRemove;
                    currentWeight -= item.weight * toRemove;

                    if (storedItems[i].Quantity <= 0)
                    {
                        storedItems.RemoveAt(i);
                    }
                }
            }

            return remaining == 0;
        }

        public bool HasItem(Item item, int quantity = 1)
        {
            int count = 0;
            foreach (var stack in storedItems)
            {
                if (stack.Item.itemId == item.itemId)
                {
                    count += stack.Quantity;
                }
            }
            return count >= quantity;
        }

        public IReadOnlyList<ItemStack> GetItems()
        {
            return storedItems;
        }

        public void SetLock(string code)
        {
            lockCode = code;
            isLocked = !string.IsNullOrEmpty(code);
        }

        public bool TryUnlock(string code)
        {
            if (code == lockCode)
            {
                isLocked = false;
                return true;
            }
            return false;
        }

        public void SetAccessLevel(AccessLevel level)
        {
            accessLevel = level;
        }
    }

    public enum AccessLevel
    {
        Owner,      // Only owner
        Clan,       // Owner + clan members
        Anyone      // Public
    }
}
