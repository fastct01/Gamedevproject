using UnityEngine;

/// <summary>
/// The socket on the ship's wall. Right-click it while carrying the Power Gear: the gear is taken from your
/// collectables, appears mounted on the wall (slowly turning) and the ship's interior lights come on.
/// </summary>
public class ShipGearFixture : MonoBehaviour
{
    public string requiredItemId = "ship_gear";
    public string partName = "Power Gear";
    public ShipPower power;
    [Tooltip("The gear on the wall (keep inactive until installed).")]
    public GameObject mountedGear;
    public float spinSpeed = 30f;
    [Tooltip("Small lamp that is red until installed, then green.")]
    public Renderer statusLamp;

    public bool Installed { get; private set; }

    Material _lamp;

    void Awake()
    {
        if (statusLamp != null) { _lamp = statusLamp.material; _lamp.EnableKeyword("_EMISSION"); }
        if (mountedGear != null) mountedGear.SetActive(false);
        SetLamp();
    }

    public string HintFor(Inventory inv)
    {
        if (Installed) return "";
        return inv != null && inv.Has(requiredItemId)
            ? "Right-click: insert " + partName
            : "Missing ship part: " + partName;
    }

    public bool Interact(GameObject player)
    {
        if (Installed) return false;
        var inv = player != null ? player.GetComponentInParent<Inventory>() : null;
        if (inv == null || !inv.Has(requiredItemId))
        {
            if (inv != null) inv.Notify("This socket needs the " + partName);
            return false;
        }
        inv.RemoveCollectable(requiredItemId);
        Installed = true;
        if (mountedGear != null) mountedGear.SetActive(true);
        if (power != null) power.PowerOn();
        SetLamp();
        inv.Notify(partName + " installed - interior lights restored");
        return true;
    }

    void SetLamp()
    {
        if (_lamp == null) return;
        Color c = Installed ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.15f, 0.1f);
        _lamp.SetColor("_BaseColor", c);
        _lamp.SetColor("_EmissionColor", c * 2f);
    }

    void Update()
    {
        if (Installed && mountedGear != null)
            mountedGear.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
