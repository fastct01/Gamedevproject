using UnityEngine;

public enum ItemIconStyle { Blaster, Gear, Torch, OxygenTank }

/// <summary>An item the player can carry. Hotbar items (tools) can be equipped; collectables live in their own section.</summary>
[CreateAssetMenu(menuName = "Game/Inventory Item")]
public class InventoryItem : ScriptableObject
{
    public string id = "item";
    public string displayName = "Item";
    [TextArea] public string description;
    [Tooltip("Collectables go to the Collectables section of the inventory screen, not the hotbar.")]
    public bool collectable;
    [Tooltip("Picking this up unlocks this many hotbar slots (0 = none).")]
    public int unlocksHotbarSlots;
    [Tooltip("Show a crosshair while this is equipped.")]
    public bool showCrosshair;
    [Tooltip("Used up when you press its slot (instead of being held), e.g. an oxygen boost.")]
    public bool consumable;
    [Tooltip("Seconds of unlimited oxygen when used (0 = none).")]
    public float oxygenBoostSeconds;
    public ItemIconStyle iconStyle;
    public Color iconColor = Color.white;
}
