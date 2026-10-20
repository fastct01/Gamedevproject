using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's inventory: a 3-slot hotbar (equip with 1/2/3) and a separate list of collectables.
/// Slots start locked; picking up an item with "unlocks hotbar slots" opens more.
/// </summary>
[DefaultExecutionOrder(-20)]
public class Inventory : MonoBehaviour
{
    public const int HotbarSize = 3;

    [Range(0, HotbarSize)] public int unlockedSlots = 1;
    public InventoryItem[] hotbar = new InventoryItem[HotbarSize];
    public List<InventoryItem> collectables = new List<InventoryItem>();
    [Tooltip("Slot equipped when the game starts (-1 = nothing). The torch starts in slot 1 and in your hand.")]
    public int startEquippedSlot = 0;

    public int EquippedSlot { get; private set; } = -1;
    public InventoryItem Equipped => EquippedSlot >= 0 ? hotbar[EquippedSlot] : null;
    /// <summary>True while the inventory screen is open (set by InventoryUI).</summary>
    public bool IsOpen { get; set; }

    public event Action Changed;
    public event Action<string> Message;
    /// <summary>Fired when a consumable item is used from the hotbar.</summary>
    public event Action<InventoryItem> Used;

    static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

    void Awake()
    {
        if (hotbar == null || hotbar.Length != HotbarSize) Array.Resize(ref hotbar, HotbarSize);
        if (IsUnlocked(startEquippedSlot) && hotbar[startEquippedSlot] != null) EquippedSlot = startEquippedSlot;
    }

    /// <summary>Equip the hotbar item with this id (does nothing if you don't have it).</summary>
    public void EquipById(string id)
    {
        for (int i = 0; i < unlockedSlots; i++)
            if (hotbar[i] != null && hotbar[i].id == id)
            {
                if (EquippedSlot != i) { EquippedSlot = i; Changed?.Invoke(); }
                return;
            }
    }

    /// <summary>Remove a collectable (e.g. a ship part once it's installed).</summary>
    public bool RemoveCollectable(string id)
    {
        int i = collectables.FindIndex(c => c != null && c.id == id);
        if (i < 0) return false;
        collectables.RemoveAt(i);
        Changed?.Invoke();
        return true;
    }

    public bool HasInHotbar(string id)
    {
        foreach (var h in hotbar) if (h != null && h.id == id) return true;
        return false;
    }

    public bool IsUnlocked(int slot) => slot >= 0 && slot < unlockedSlots;

    public bool Add(InventoryItem item)
    {
        if (item == null) return false;
        bool unlocked = false;
        if (item.unlocksHotbarSlots > unlockedSlots)
        {
            unlockedSlots = Mathf.Min(HotbarSize, item.unlocksHotbarSlots);
            unlocked = true;
        }

        if (item.collectable)
        {
            collectables.Add(item);
            Notify($"Collected: {item.displayName}  (Tab to view)");
            return true;
        }

        for (int i = 0; i < unlockedSlots; i++)
        {
            if (hotbar[i] != null) continue;
            hotbar[i] = item;
            Notify(unlocked
                ? $"Picked up {item.displayName} - {unlockedSlots} hotbar slots unlocked. Press {i + 1} to equip."
                : $"Picked up {item.displayName}. Press {i + 1} to equip.");
            return true;
        }
        Notify("Hotbar is full");
        return false;
    }

    public bool Has(string id)
    {
        foreach (var c in collectables) if (c != null && c.id == id) return true;
        foreach (var h in hotbar) if (h != null && h.id == id) return true;
        return false;
    }

    public void Swap(int a, int b)
    {
        if (a == b || !IsUnlocked(a) || !IsUnlocked(b)) return;
        (hotbar[a], hotbar[b]) = (hotbar[b], hotbar[a]);
        if (EquippedSlot == a) EquippedSlot = b;
        else if (EquippedSlot == b) EquippedSlot = a;
        Changed?.Invoke();
    }

    /// <summary>Equip the item in this slot, or put it away if it's already equipped. Consumables are used up instead.</summary>
    public void ToggleEquip(int slot)
    {
        if (!IsUnlocked(slot) || hotbar[slot] == null) return;
        var item = hotbar[slot];
        if (item.consumable)
        {
            hotbar[slot] = null;
            if (EquippedSlot == slot) EquippedSlot = -1;
            Used?.Invoke(item);
            Changed?.Invoke();
            return;
        }
        EquippedSlot = EquippedSlot == slot ? -1 : slot;
        Changed?.Invoke();
    }

    public void Notify(string message)
    {
        Message?.Invoke(message);
        Changed?.Invoke();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        for (int i = 0; i < SlotKeys.Length; i++)
            if (kb[SlotKeys[i]].wasPressedThisFrame) ToggleEquip(i);
    }
}
