using UnityEngine;

/// <summary>
/// Ship interior power. The lights come on when the ship part (gear) is put into the wall fixture
/// (ShipGearFixture). Lights don't cast shadows, so the interior stays cheap to render.
/// </summary>
public class ShipPower : MonoBehaviour
{
    public string requiredItemId = "ship_gear";
    [Tooltip("Old behaviour: install automatically when you walk into this zone with the part. Leave empty to use the wall fixture.")]
    public OxygenZone shipZone;
    public bool autoInstallInZone;
    [Tooltip("Lights / glowing strips switched on when powered (keep them inactive in the scene).")]
    public GameObject[] poweredObjects;

    public bool Powered { get; private set; }

    Inventory _inv;

    public void PowerOn()
    {
        if (Powered) return;
        Powered = true;
        foreach (var g in poweredObjects) if (g != null) g.SetActive(true);
    }

    void Update()
    {
        if (Powered || !autoInstallInZone) return;
        if (_inv == null) { _inv = FindFirstObjectByType<Inventory>(); if (_inv == null) return; }
        if (!_inv.Has(requiredItemId)) return;
        if (shipZone != null && !shipZone.Contains(_inv.transform.position)) return;
        Powered = true;
        foreach (var g in poweredObjects) if (g != null) g.SetActive(true);
        _inv.Notify("Ship part installed - interior lights restored");
    }
}
