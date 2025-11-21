// Inventory.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the player's inventory data, including items, crafting, and item usage logic.
/// This class holds the inventory state but does not interact with the UI directly.
/// </summary>
public class Inventory : MonoBehaviour
{
    // --- Events ---

    /// <summary>
    /// Invoked whenever the inventory changes (item added, removed, swapped, etc.).
    /// UI scripts should subscribe to this to refresh their display.
    /// </summary>
    public event Action OnInventoryUpdated;

    /// <summary>
    /// Invoked specifically when a new item is successfully added.
    /// Used for triggering notifications.
    /// </summary>
    public event Action<ItemSO> OnItemAdded;

    /// <summary>
    /// Invoked when an item is used but has no consumable effect.
    /// </summary>
    public event Action OnItemUseFailed;

    /// <summary>
    /// Invoked when an item is dropped but has no world prefab to instantiate.
    /// </summary>
    public event Action OnItemDropFailed;

    // --- Inspector References ---
    [Header("Inventory Settings")]
    [Tooltip("The maximum number of slots in the inventory.")]
    [SerializeField] private int _inventorySize = 15;
 

    [Tooltip("The position where dropped items should be instantiated.")]
    [SerializeField] private Transform _dropPoint;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    [SerializeField] private PlayerStats _playerStats;

    // --- Properties ---

    /// <summary>
    /// The array of inventory slots that holds all item data.
    /// </summary>
    public InventorySlot[] Slots { get; private set; }

    // --- Unity Lifecycle Methods ---

    private void Awake()
    {
        // Initialize the inventory slots array.
        Slots = new InventorySlot[_inventorySize];
        for (int i = 0; i < Slots.Length; i++)
        {
            Slots[i] = new InventorySlot(null, 0);
        }
        if (_debugMode) Debug.Log($"[Inventory] Initialized with {_inventorySize} slots.");
        _playerStats = GetComponent<PlayerStats>();
    }

    // --- Public Data Accessors ---

    /// <summary>
    /// Gets the total quantity of a specific item across all slots.
    /// </summary>
    /// <param name="itemToCount">The Item ScriptableObject to count.</param>
    /// <returns>The total number of the specified item.</returns>
    public int GetItemCount(ItemSO itemToCount)
    {
        if (itemToCount == null) return 0;

        int count = Slots
            .Where(slot => slot.item == itemToCount)
            .Sum(slot => slot.quantity);

        if (_debugMode) Debug.Log($"[Inventory] Found {count} of {itemToCount.itemName}.");
        return count;
    }

    /// <summary>
    /// Retrieves the inventory slot at a given index.
    /// </summary>
    /// <param name="index">The index of the slot to retrieve.</param>
    /// <returns>The InventorySlot object, or null if the index is invalid.</returns>
    public InventorySlot GetSlot(int index)
    {
        return IsIndexValid(index) ? Slots[index] : null;
    }

    // --- Public Inventory Modification Methods ---

    /// <summary>
    /// Tries to add an item to the inventory. First, it tries to stack with existing items,
    /// then it fills empty slots.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="quantity">The amount to add.</param>
    /// <returns>True if the entire quantity was added, false otherwise.</returns>
    public bool AddItem(ItemSO item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        if (_debugMode) Debug.Log($"[Inventory] Attempting to add {quantity} of {item.itemName}.");

        int originalQuantity = quantity;

        // First pass: Try to stack with existing items.
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].item == item && Slots[i].quantity < item.maxStackSize)
            {
                int spaceAvailable = item.maxStackSize - Slots[i].quantity;
                int amountToAdd = Mathf.Min(quantity, spaceAvailable);

                Slots[i].AddToStack(amountToAdd);
                quantity -= amountToAdd;
                if (quantity <= 0) break;
            }
        }

        // Second pass: Fill empty slots.
        if (quantity > 0)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].item == null)
                {
                    int amountToAdd = Mathf.Min(quantity, item.maxStackSize);
                    Slots[i].item = item;
                    Slots[i].AddToStack(amountToAdd);
                    quantity -= amountToAdd;
                    if (quantity <= 0) break;
                }
            }
        }

        bool success = quantity < originalQuantity;
        if (success)
        {
            OnInventoryUpdated?.Invoke();
            OnItemAdded?.Invoke(item);
        }

        if (quantity > 0)
        {
            if (_debugMode) Debug.LogWarning($"[Inventory] Inventory full. Could not add {quantity} of {item.itemName}.");
            return false;
        }

        return true;
    }
    /// <summary>
    /// Returns true if there's room to add at least one of the given item:
    /// either topping off an existing stack or using an empty slot.
    /// </summary>
    public bool CanPickup(ItemSO item)
    {
        if (item == null) return false;

        foreach (var slot in Slots)
        {
            if (slot.item == null)
                return true; // empty slot available

            if (slot.item == item && slot.quantity < item.maxStackSize)
                return true; // can top off existing stack
        }

        return false;
    }

    /// <summary>
    /// Returns true if there's room to add 'quantity' of the given item.
    /// It simulates stacking-first then empty-slot usage to see if all fit.
    /// </summary>
    public bool HasSpaceFor(ItemSO item, int quantity)
    {
        if (item == null || quantity <= 0) return false;

        int remaining = quantity;

        // 1) Fill existing stacks
        foreach (var slot in Slots)
        {
            if (slot.item == item)
            {
                int space = item.maxStackSize - slot.quantity;
                if (space > 0)
                {
                    int used = Mathf.Min(space, remaining);
                    remaining -= used;
                    if (remaining <= 0) return true;
                }
            }
        }

        // 2) Use empty slots
        foreach (var slot in Slots)
        {
            if (slot.item == null)
            {
                int used = Mathf.Min(item.maxStackSize, remaining);
                remaining -= used;
                if (remaining <= 0) return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Swaps the contents of two inventory slots.
    /// </summary>
    public void SwapItems(int indexA, int indexB)
    {
        if (!IsIndexValid(indexA) || !IsIndexValid(indexB)) return;

        InventorySlot tempSlot = Slots[indexA];
        Slots[indexA] = Slots[indexB];
        Slots[indexB] = tempSlot;

        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// Removes a specified quantity from a slot. If quantity drops to zero, clears the slot.
    /// </summary>
    public void RemoveFromSlot(int index, int quantity = 1)
    {
        if (!IsIndexValid(index) || Slots[index].item == null) return;

        Slots[index].RemoveFromStack(quantity);

        if (Slots[index].quantity <= 0)
        {
            ClearSlot(index);
        }

        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// Uses the item at the specified slot index.
    /// </summary>
    public void UseItem(int index)
    {
        
        if (!IsIndexValid(index) || Slots[index].item == null) return;

        ItemSO item = Slots[index].item;

        foreach (ItemType type in item.itemTypes)
        {
            if (type == ItemType.Stamina) // Replace with the enum value you're checking for
            {
                if (_playerStats.CurrentHunger == _playerStats.MaxHunger)
                {
                    if (_debugMode) Console.WriteLine("Max HUNGER ALREADY!!");
                    return;
                }
            }
            if (type == ItemType.Health) // Replace with the enum value you're checking for
            {
                if(_playerStats.CurrentHealth == _playerStats.MaxHealth)
                {
                    if (_debugMode) Console.WriteLine("Max HP ALREADY!!");
                    return;
                }
                
            }
            
        }

        if (item.ItemEffect != null)
        {
            Instantiate(item.ItemEffect);
            RemoveFromSlot(index, 1); // This already invokes the update event.
            if (_debugMode) Debug.Log($"[Inventory] Used {item.itemName}.");
        }
        else
        {
            if (_debugMode) Debug.Log($"[Inventory] {item.itemName} has no effect.");
            OnItemUseFailed?.Invoke();
        }
    }
    /// <summary>
    /// Directly assigns an InventorySlot's data to a specific index. 
    /// Used for drag-and-drop operations.
    /// </summary>
    public void AssignSlot(int index, InventorySlot slot)
    {
        if (!IsIndexValid(index)) return;
        Slots[index] = new InventorySlot(slot.item, slot.quantity);
        OnInventoryUpdated?.Invoke();
    }


    /// <summary>
    /// Updates only the quantity of an item in a given slot.
    /// Used for splitting and merging stacks.
    /// </summary>
    public void UpdateSlotQuantity(int index, int quantity)
    {
        if (!IsIndexValid(index)) return;
        Slots[index].quantity = quantity;
        OnInventoryUpdated?.Invoke();
    }
    /// <summary>
    /// Drops the item at the specified slot index into the world.
    /// </summary>
    public void DropItem(int index)
    {
        if (!IsIndexValid(index) || Slots[index].item == null) return;

        ItemSO item = Slots[index].item;
        if (item.worldItem != null)
        {
            Vector3 position = _dropPoint != null ? _dropPoint.position : transform.position;
            GameObject droppedObj = Instantiate(item.worldItem, position, Quaternion.identity);

            // Special case: if this item is a Corpse
            if (item.itemTypes.Contains(ItemType.Corpse)) // assuming itemTypes is a List<ItemType> or similar
            {
                EnemyHealth enemyHealth = droppedObj.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.ToDeathState();
                    if (_debugMode) Debug.Log($"[Inventory] {item.itemName} corpse dropped and marked dead.");
                }
                else if (_debugMode)
                {
                    Debug.LogWarning($"[Inventory] Dropped {item.itemName} corpse but no EnemyHealth component found.");
                }
            }

            RemoveFromSlot(index, 1); // This already invokes the update event.
            if (_debugMode) Debug.Log($"[Inventory] Dropped {item.itemName}.");
        }
        else
        {
            if (_debugMode) Debug.Log($"[Inventory] {item.itemName} has no world prefab.");
            OnItemDropFailed?.Invoke();
        }
    }


    // --- Crafting ---

    /// <summary>
    /// Attempts to craft a new item using the items from two specified slots.
    /// </summary>
    /// <returns>True if crafting was successful, false otherwise.</returns>

    /// <summary>
    /// Finds all items in the inventory that can be validly crafted with the given ingredient.
    /// </summary>
    /// <returns>A list of valid partner items.</returns>



    // --- Private Helper Methods ---

    private bool IsIndexValid(int index) => index >= 0 && index < Slots.Length;

    public void ClearSlot(int index)
    {
        if (!IsIndexValid(index)) return;
        Slots[index].item = null;
        Slots[index].quantity = 0;
    }


}

// This remains a simple data container, so it   stay in the same file for convenience.
[System.Serializable]
public class InventorySlot
{
    public ItemSO item;
    public int quantity;

    public InventorySlot(ItemSO item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public void AddToStack(int amount) => quantity += amount;
    public void RemoveFromStack(int amount) => quantity -= amount;
}