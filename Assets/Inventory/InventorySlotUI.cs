using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One hotbar slot in the UI. Click to equip; drag onto another slot to swap (while the inventory is open).</summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int index;
    public InventoryUI owner;
    public Image frame, background, icon, lockIcon;
    public Text number;

    public void OnPointerClick(PointerEventData e) => owner.OnSlotClick(index);
    public void OnBeginDrag(PointerEventData e) => owner.OnSlotBeginDrag(index, e);
    public void OnDrag(PointerEventData e) => owner.OnSlotDrag(e);
    public void OnEndDrag(PointerEventData e) => owner.OnSlotEndDrag();
    public void OnDrop(PointerEventData e) => owner.OnSlotDrop(index);
}
