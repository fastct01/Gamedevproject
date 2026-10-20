using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Builds the inventory HUD at runtime:
///  - a 3-slot hotbar at the bottom (numbers 1-3, locked slots show a padlock, equipped slot is highlighted)
///  - Tab opens the inventory screen: drag hotbar items between slots, click a slot to equip,
///    and see the Collectables section (ship parts etc. only appear here)
///  - a crosshair while an item that wants one is equipped
///  - short messages (picked up ...) and a "Right-click: pick up" hint when looking at an item
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public Key toggleKey = Key.Tab;
    public float slotSize = 64f;
    public float slotGap = 10f;
    [Tooltip("Distance from the bottom of the screen (the stamina line sits below this).")]
    public float bottomMargin = 46f;
    public Color frameIdle = new Color(1f, 1f, 1f, 0.35f);
    public Color frameEquipped = new Color(0.3f, 0.9f, 1f, 1f);

    Canvas _canvas;
    RectTransform _canvasRect;
    InventorySlotUI[] _slots;
    GameObject _panel, _hotbarLabel, _crosshair;
    RectTransform _collectList;
    Text _collectEmpty, _toast, _hint, _description;
    Image _ghost;
    float _toastUntil;
    int _dragFrom = -1;
    Font _font;
    StarterAssetsInputs _inputs;
    Camera _cam;
    float _reach = 1.5f;
    PlayerHealth _health;
    OxygenSystem _oxygen;

    public bool IsOpen => inventory != null && inventory.IsOpen;

    void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _cam = GetComponentInChildren<Camera>();
        _health = GetComponent<PlayerHealth>();
        _oxygen = GetComponent<OxygenSystem>();
        var oi = GetComponent<ObjectInteractor>();
        if (oi != null) _reach = oi.reach;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        inventory.Changed += Refresh;
        inventory.Message += ShowToast;
        Refresh();
    }

    void OnDestroy()
    {
        if (inventory == null) return;
        inventory.Changed -= Refresh;
        inventory.Message -= ShowToast;
    }

    // ------------------------------------------------------------------ build
    void Build()
    {
        var cgo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 115;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        _canvasRect = (RectTransform)cgo.transform;

        // Crosshair (shown while an item that wants one is equipped).
        var ch = Rect("Crosshair", _canvasRect);
        ch.sizeDelta = new Vector2(40, 40);
        _crosshair = ch.gameObject;
        void Arm(Vector2 pos, Vector2 size)
        {
            var a = Rect("Arm", ch);
            a.anchoredPosition = pos; a.sizeDelta = size;
            Img(a, Color.white).gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        }
        Arm(new Vector2(0, 11), new Vector2(3, 10));
        Arm(new Vector2(0, -11), new Vector2(3, 10));
        Arm(new Vector2(11, 0), new Vector2(10, 3));
        Arm(new Vector2(-11, 0), new Vector2(10, 3));
        var dot = Rect("Dot", ch); dot.sizeDelta = new Vector2(3, 3); Img(dot, Color.white);

        // Hotbar.
        var bar = Rect("Hotbar", _canvasRect);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0, bottomMargin);
        bar.sizeDelta = new Vector2(Inventory.HotbarSize * slotSize + (Inventory.HotbarSize - 1) * slotGap, slotSize);
        _slots = new InventorySlotUI[Inventory.HotbarSize];
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var s = Rect("Slot " + (i + 1), bar);
            s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f);
            s.pivot = new Vector2(0f, 0.5f);
            s.anchoredPosition = new Vector2(i * (slotSize + slotGap), 0);
            s.sizeDelta = new Vector2(slotSize, slotSize);
            var slot = s.gameObject.AddComponent<InventorySlotUI>();
            slot.index = i;
            slot.owner = this;
            slot.frame = Img(s, frameIdle);
            slot.frame.raycastTarget = true; // catches clicks / drags / drops
            var bg = Rect("Background", s); Stretch(bg, 3);
            slot.background = Img(bg, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            var ic = Rect("Icon", s); Stretch(ic, 9);
            slot.icon = Img(ic, Color.white);
            slot.icon.preserveAspect = true;
            var lk = Rect("Lock", s); Stretch(lk, 18);
            slot.lockIcon = Img(lk, Color.white);
            slot.lockIcon.sprite = IconFactory.Lock();
            slot.number = Txt("Number", s, 16, TextAnchor.UpperLeft);
            var nr = slot.number.rectTransform; Stretch(nr, 5);
            slot.number.text = (i + 1).ToString();
            _slots[i] = slot;
        }

        _hotbarLabel = Txt("HotbarLabel", bar, 18, TextAnchor.LowerCenter).gameObject;
        var hl = (RectTransform)_hotbarLabel.transform;
        hl.anchorMin = hl.anchorMax = new Vector2(0.5f, 1f); hl.pivot = new Vector2(0.5f, 0f);
        hl.anchoredPosition = new Vector2(0, 8); hl.sizeDelta = new Vector2(600, 24);
        _hotbarLabel.GetComponent<Text>().text = "HOTBAR  -  drag to rearrange, click to equip";

        // Inventory screen (Tab).
        var panel = Rect("InventoryPanel", _canvasRect);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0, 40);
        panel.sizeDelta = new Vector2(620, 380);
        Img(panel, new Color(0.03f, 0.05f, 0.08f, 0.88f));
        _panel = panel.gameObject;
        var title = Txt("Title", panel, 34, TextAnchor.UpperCenter);
        Place(title.rectTransform, new Vector2(0, -16), new Vector2(600, 44));
        title.text = "INVENTORY";
        var help = Txt("Help", panel, 17, TextAnchor.UpperCenter);
        Place(help.rectTransform, new Vector2(0, -60), new Vector2(600, 24));
        help.text = "1 / 2 / 3 or click a slot to equip   -   Tab or Esc to close";
        help.color = new Color(1, 1, 1, 0.7f);
        var ct = Txt("CollectablesTitle", panel, 22, TextAnchor.UpperLeft);
        Place(ct.rectTransform, new Vector2(0, -104), new Vector2(560, 30));
        ct.text = "COLLECTABLES";
        ct.color = new Color(1f, 0.82f, 0.45f);
        var line = Rect("Line", panel); Place(line, new Vector2(0, -136), new Vector2(560, 2)); Img(line, new Color(1f, 0.82f, 0.45f, 0.5f));
        _collectList = Rect("Collectables", panel);
        Place(_collectList, new Vector2(0, -150), new Vector2(560, 120));
        _collectEmpty = Txt("Empty", _collectList, 18, TextAnchor.MiddleCenter);
        Stretch(_collectEmpty.rectTransform, 0);
        _collectEmpty.text = "Nothing collected yet";
        _collectEmpty.color = new Color(1, 1, 1, 0.45f);
        _description = Txt("Description", panel, 16, TextAnchor.UpperCenter);
        Place(_description.rectTransform, new Vector2(0, -290), new Vector2(560, 70));
        _description.color = new Color(1, 1, 1, 0.75f);
        _panel.SetActive(false);
        _hotbarLabel.SetActive(false);

        // Drag ghost.
        var gh = Rect("DragGhost", _canvasRect);
        gh.sizeDelta = new Vector2(slotSize * 0.8f, slotSize * 0.8f);
        _ghost = Img(gh, new Color(1, 1, 1, 0.85f));
        _ghost.raycastTarget = false;
        _ghost.gameObject.SetActive(false);

        // Messages + pickup hint.
        _toast = Txt("Toast", _canvasRect, 24, TextAnchor.MiddleCenter);
        var tr = _toast.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f); tr.anchoredPosition = new Vector2(0, -90); tr.sizeDelta = new Vector2(1200, 40);
        _hint = Txt("Hint", _canvasRect, 20, TextAnchor.MiddleCenter);
        var hr = _hint.rectTransform; hr.anchoredPosition = new Vector2(0, -60); hr.sizeDelta = new Vector2(800, 30);
    }

    // ------------------------------------------------------------------ state
    void Refresh()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var s = _slots[i];
            bool unlocked = inventory.IsUnlocked(i);
            var item = inventory.hotbar[i];
            bool equipped = inventory.EquippedSlot == i;
            s.lockIcon.enabled = !unlocked;
            s.icon.enabled = unlocked && item != null;
            if (item != null) s.icon.sprite = IconFactory.For(item);
            s.background.color = unlocked ? new Color(0.05f, 0.07f, 0.1f, 0.75f) : new Color(0.02f, 0.02f, 0.03f, 0.6f);
            s.frame.color = equipped ? frameEquipped : frameIdle;
            s.number.color = unlocked ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        // Collectables list (only visible on the inventory screen).
        for (int i = _collectList.childCount - 1; i >= 0; i--)
        {
            var c = _collectList.GetChild(i);
            if (c != _collectEmpty.transform) Destroy(c.gameObject);
        }
        _collectEmpty.enabled = inventory.collectables.Count == 0;
        string desc = "";
        for (int i = 0; i < inventory.collectables.Count; i++)
        {
            var item = inventory.collectables[i];
            if (item == null) continue;
            var entry = Rect("Collectable", _collectList);
            entry.anchorMin = entry.anchorMax = entry.pivot = new Vector2(0f, 1f);
            entry.anchoredPosition = new Vector2(i * 140, 0);
            entry.sizeDelta = new Vector2(130, 110);
            var box = Rect("IconBox", entry);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = Vector2.zero; box.sizeDelta = new Vector2(72, 72);
            Img(box, new Color(1f, 0.82f, 0.45f, 0.18f));
            var ic = Rect("Icon", box); Stretch(ic, 6);
            var img = Img(ic, Color.white); img.sprite = IconFactory.For(item); img.preserveAspect = true;
            var name = Txt("Name", entry, 15, TextAnchor.UpperCenter);
            var nr = name.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f); nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0, -76); nr.sizeDelta = new Vector2(140, 36);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.text = item.displayName;
            if (!string.IsNullOrEmpty(item.description)) desc += item.displayName + ": " + item.description + "\n";
        }
        _description.text = desc;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool blocked = (_health != null && _health.IsDead) || (_oxygen != null && _oxygen.IsDepleted);
            if (kb[toggleKey].wasPressedThisFrame && !blocked) SetOpen(!IsOpen);
            else if (IsOpen && (kb.escapeKey.wasPressedThisFrame || blocked)) SetOpen(false);
        }

        // Crosshair while an item that wants one is equipped.
        var eq = inventory.Equipped;
        _crosshair.SetActive(eq != null && eq.showCrosshair && !IsOpen);

        // Toast fade.
        float a = Mathf.Clamp01((_toastUntil - Time.time) / 0.6f);
        _toast.color = new Color(1f, 1f, 1f, a);

        // "Right-click: pick up ..." when looking at an item.
        _hint.text = "";
        if (!IsOpen && _cam != null)
        {
            var t = _cam.transform;
            if (Physics.Raycast(t.position, t.forward, out var hit, _reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var p = hit.collider.GetComponentInParent<ItemPickup>();
                if (p != null && p.item != null) _hint.text = "Right-click: pick up " + p.item.displayName;
            }
        }
    }

    void SetOpen(bool open)
    {
        inventory.IsOpen = open;
        _panel.SetActive(open);
        _hotbarLabel.SetActive(open);
        if (!open) OnSlotEndDrag();
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_inputs != null) { _inputs.cursorInputForLook = false; _inputs.look = Vector2.zero; _inputs.cursorLocked = false; }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_inputs != null) { _inputs.cursorInputForLook = true; _inputs.cursorLocked = true; }
        }
    }

    void ShowToast(string message)
    {
        _toast.text = message;
        _toastUntil = Time.time + 3f;
    }

    // ------------------------------------------------------------------ slot events
    public void OnSlotClick(int i)
    {
        if (IsOpen) inventory.ToggleEquip(i);
    }

    public void OnSlotBeginDrag(int i, PointerEventData e)
    {
        if (!IsOpen || !inventory.IsUnlocked(i) || inventory.hotbar[i] == null) return;
        _dragFrom = i;
        _ghost.sprite = IconFactory.For(inventory.hotbar[i]);
        _ghost.gameObject.SetActive(true);
        _slots[i].icon.color = new Color(1, 1, 1, 0.3f);
        OnSlotDrag(e);
    }

    public void OnSlotDrag(PointerEventData e)
    {
        if (_dragFrom < 0) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, e.position, null, out var local))
            _ghost.rectTransform.anchoredPosition = local;
    }

    public void OnSlotDrop(int i)
    {
        if (_dragFrom >= 0 && _dragFrom != i && inventory.IsUnlocked(i)) inventory.Swap(_dragFrom, i);
    }

    public void OnSlotEndDrag()
    {
        if (_dragFrom >= 0 && _dragFrom < _slots.Length) _slots[_dragFrom].icon.color = Color.white;
        _dragFrom = -1;
        if (_ghost != null) _ghost.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ ui helpers
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Place(RectTransform rt, Vector2 topOffset, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topOffset;
        rt.sizeDelta = size;
    }

    static Image Img(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Text Txt(string name, Transform parent, int size, TextAnchor anchor)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _font; t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        rt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        return t;
    }
}
