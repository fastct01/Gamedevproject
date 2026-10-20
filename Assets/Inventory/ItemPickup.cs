using UnityEngine;

/// <summary>An item lying in the world. Right-click it (or E) to put it in your inventory. Slowly spins and bobs.</summary>
public class ItemPickup : MonoBehaviour
{
    public InventoryItem item;
    [Tooltip("The part that spins/bobs (defaults to this).")]
    public Transform visual;
    public float spinSpeed = 60f;
    public float bobHeight = 0.006f;

    Vector3 _base;

    void Start()
    {
        if (visual == null) visual = transform;
        _base = visual.localPosition;
    }

    void Update()
    {
        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        visual.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * 2f) * bobHeight;
    }

    public bool Collect(GameObject player)
    {
        var inv = player != null ? player.GetComponentInParent<Inventory>() : null;
        if (inv == null || !inv.Add(item)) return false;
        Destroy(gameObject);
        return true;
    }
}
