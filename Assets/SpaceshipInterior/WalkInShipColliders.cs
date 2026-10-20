using UnityEngine;

/// <summary>
/// Makes a solid (non-hollow) ship model walkable inside by replacing its MeshCollider with a box shell:
/// floor, ceiling and walls around an interior cavity, a doorway on one side, and an optional ramp.
/// All values are in the ship's local space. Select the ship to see the layout as gizmos
/// (green = cavity, yellow = doorway, cyan = ramp), then right-click the component > Rebuild Walk-In Colliders.
/// </summary>
public class WalkInShipColliders : MonoBehaviour
{
    public enum Side { PosZ, NegZ, PosX, NegX }

    [Header("Hull (outer box the colliders fill up to)")]
    public Vector3 outerMin = new Vector3(-0.44f, -0.30f, -0.88f);
    public Vector3 outerMax = new Vector3(0.44f, 0.25f, -0.03f);

    [Header("Interior cavity (the walkable space)")]
    public Vector3 innerMin = new Vector3(-0.28f, -0.20f, -0.78f);
    public Vector3 innerMax = new Vector3(0.28f, 0.07f, -0.06f);

    [Header("Doorway")]
    public Side doorSide = Side.PosZ;
    [Tooltip("Door centre offset along the wall, from the cavity centre.")]
    public float doorOffset = 0f;
    public float doorWidth = 0.18f;
    public float doorHeight = 0.17f;

    [Header("Ramp (leave Ramp Width at 0 for none)")]
    public Vector3 rampBottom = new Vector3(0f, -0.42f, 0.95f);
    public Vector3 rampTop = new Vector3(0f, -0.20f, 0.29f);
    public float rampWidth = 0.3f;
    [Tooltip("Flat landing from the ramp top to the doorway.")]
    public bool addLanding = true;

    [Header("Entry corridor (open bay in front of the door, running along Z)")]
    [Tooltip("Adds side walls and a ceiling around the bay so the player can't walk out through the model.")]
    public bool addEntryCorridor = true;
    public Vector3 corridorMin = new Vector3(-0.145f, -0.204f, -0.03f);
    public Vector3 corridorMax = new Vector3(0.145f, 0.05f, 0.25f);
    public float corridorWallThickness = 0.03f;

    [Header("Sliding door")]
    public bool addSlidingDoor = true;
    public Material doorMaterial;
    [Tooltip("Dark backing shown in the doorway when the door is open (hides the solid hull texture).")]
    public Material doorwayMaterial;
    [Tooltip("How far in front of the outer wall the panels sit, so they're visible on the hull.")]
    public float doorPanelOffset = 0.02f;
    public float doorPanelThickness = 0.008f;
    public float doorSpeed = 0.4f;
    [Tooltip("How far from the doorway (each side) the player triggers the door.")]
    public float doorTriggerDepth = 0.25f;

    [Header("Visuals (optional)")]
    [Tooltip("If set, the collider shell (walls, floor, ramp, corridor) is drawn with this material, so it doubles as the ship's outside.")]
    public Material hullMaterial;
    [Tooltip("If set, the cavity gets visible inner walls with this material, so from inside you see walls instead of the outside world.")]
    public Material interiorMaterial;
    [Tooltip("How far the inner walls sit in front of the shell. Keeps the two surfaces apart so they don't flicker.")]
    public float liningInset = 0.002f;

    [Header("Setup")]
    [Tooltip("Turn off the model's own MeshCollider(s) so they stop blocking the interior.")]
    public bool disableMeshColliders = true;

    const string RootName = "WalkInColliders";
    const float SlabMin = 0.005f;

    void Awake()
    {
        if (transform.Find(RootName) == null) Rebuild();
    }

    [ContextMenu("Rebuild Walk-In Colliders")]
    public void Rebuild()
    {
        var old = transform.Find(RootName);
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        if (disableMeshColliders)
            foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
                mc.enabled = false;

        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildShell(root, outerMin, outerMax, 0f, hullMaterial, true);

        if (interiorMaterial != null)
        {
            // Thin visible lining just inside the shell, with the same doorway hole. It is pulled slightly
            // into the cavity so its faces never sit on the same plane as the shell's.
            var lining = new GameObject("Interior Lining").transform;
            lining.SetParent(root, false);
            Vector3 t = Vector3.one * 0.01f;
            BuildShell(lining, innerMin - t, innerMax + t, liningInset, interiorMaterial, false);
        }

        if (rampWidth > 0f) BuildRamp(root);
        if (addEntryCorridor) BuildCorridor(root);
        if (addSlidingDoor) BuildDoor(root);
    }

    void BuildCorridor(Transform parent)
    {
        Vector3 a = corridorMin, b = corridorMax;
        float t = corridorWallThickness;
        Box(parent, "Corridor Left", new Vector3(a.x - t, a.y, a.z), new Vector3(a.x, b.y, b.z), hullMaterial, true);
        Box(parent, "Corridor Right", new Vector3(b.x, a.y, a.z), new Vector3(b.x + t, b.y, b.z), hullMaterial, true);
        Box(parent, "Corridor Ceiling", new Vector3(a.x - t, b.y, a.z), new Vector3(b.x + t, b.y + t, b.z), hullMaterial, true);
    }

    // Two panels on the outside of the doorway that slide apart along the hull, plus a trigger zone.
    void BuildDoor(Transform parent)
    {
        bool alongX = doorSide == Side.PosX || doorSide == Side.NegX;
        Vector3 tangent = alongX ? Vector3.forward : Vector3.right;
        Vector3 normal = doorSide switch
        {
            Side.PosZ => Vector3.forward,
            Side.NegZ => Vector3.back,
            Side.PosX => Vector3.right,
            _ => Vector3.left,
        };
        Vector3 basePos = DoorCentreOnOuterWall() + Vector3.up * doorHeight * 0.5f;

        var doorRoot = new GameObject("Door").transform;
        doorRoot.SetParent(parent, false);

        if (doorwayMaterial != null)
        {
            Vector3 backSize = alongX
                ? new Vector3(0.002f, doorHeight, doorWidth)
                : new Vector3(doorWidth, doorHeight, 0.002f);
            var back = Visual(doorRoot, "Doorway", basePos + normal * (doorPanelOffset * 0.5f), backSize, doorwayMaterial);
            SafeDestroy(back.GetComponent<Collider>());
        }

        float half = doorWidth * 0.5f;
        Vector3 panelSize = alongX
            ? new Vector3(doorPanelThickness, doorHeight, half)
            : new Vector3(half, doorHeight, doorPanelThickness);
        var panels = new SlidingDoor.Panel[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var p = new GameObject(i == 0 ? "Panel A" : "Panel B").transform;
            p.SetParent(doorRoot, false);
            p.localPosition = basePos + normal * doorPanelOffset + tangent * (side * half * 0.5f);
            Visual(p, "Slab", Vector3.zero, panelSize, doorMaterial);
            panels[i] = new SlidingDoor.Panel { transform = p, openOffset = tangent * (side * (half + 0.005f)) };
        }

        var trig = new GameObject("Door Trigger");
        trig.transform.SetParent(doorRoot, false);
        trig.transform.localPosition = basePos;
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = alongX
            ? new Vector3(doorTriggerDepth * 2f, doorHeight, doorWidth + 0.1f)
            : new Vector3(doorWidth + 0.1f, doorHeight, doorTriggerDepth * 2f);
        var door = trig.AddComponent<SlidingDoor>();
        door.panels = panels;
        door.speed = doorSpeed;
    }

    static GameObject Visual(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void SafeDestroy(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else DestroyImmediate(o);
    }

    // Floor, ceiling and four walls between the cavity (shrunk by inset) and the given outer box.
    void BuildShell(Transform parent, Vector3 oMin, Vector3 oMax, float inset, Material visual, bool solid)
    {
        Vector3 iMin = innerMin + Vector3.one * inset, iMax = innerMax - Vector3.one * inset;

        Box(parent, "Floor", new Vector3(oMin.x, oMin.y, oMin.z), new Vector3(oMax.x, iMin.y, oMax.z), visual, solid);
        Box(parent, "Ceiling", new Vector3(oMin.x, iMax.y, oMin.z), new Vector3(oMax.x, oMax.y, oMax.z), visual, solid);

        foreach (Side s in System.Enum.GetValues(typeof(Side)))
        {
            bool alongX = s == Side.PosX || s == Side.NegX;   // wall normal is the X axis
            bool positive = s == Side.PosZ || s == Side.PosX;
            int n = alongX ? 0 : 2, tan = alongX ? 2 : 0;

            Vector3 min = Vector3.zero, max = Vector3.zero;
            min[n] = positive ? iMax[n] : oMin[n];
            max[n] = positive ? oMax[n] : iMin[n];
            // X walls run the full outer length so the corners are closed; Z walls fit between them.
            min[tan] = alongX ? oMin[tan] : iMin[tan];
            max[tan] = alongX ? oMax[tan] : iMax[tan];
            min.y = iMin.y; max.y = iMax.y;

            if (s != doorSide)
            {
                Box(parent, "Wall " + s, min, max, visual, solid);
                continue;
            }

            // The inset also narrows the doorway, so the lining's door frame sits in front of the shell's.
            float c = (iMin[tan] + iMax[tan]) * 0.5f + doorOffset;
            float d0 = c - doorWidth * 0.5f + inset, d1 = c + doorWidth * 0.5f - inset;

            Vector3 aMax = max; aMax[tan] = d0;
            Box(parent, "Wall " + s + " A", min, aMax, visual, solid);

            Vector3 bMin = min; bMin[tan] = d1;
            Box(parent, "Wall " + s + " B", bMin, max, visual, solid);

            Vector3 topMin = min, topMax = max;
            topMin[tan] = d0; topMax[tan] = d1; topMin.y = innerMin.y + doorHeight - inset;
            Box(parent, "Wall " + s + " Lintel", topMin, topMax, visual, solid);
        }
    }

    void BuildRamp(Transform parent)
    {
        const float thick = 0.03f;
        Vector3 dir = rampTop - rampBottom;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        var go = new GameObject("Ramp");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = rot;
        go.transform.localPosition = (rampBottom + rampTop) * 0.5f - rot * Vector3.up * (thick * 0.5f);
        var size = new Vector3(rampWidth, thick, dir.magnitude);
        go.AddComponent<BoxCollider>().size = size;
        if (hullMaterial != null) AddVisual(go, size, hullMaterial);

        if (!addLanding) return;
        // Landing from ramp top to the doorway, flush with the cavity floor.
        Vector3 doorPoint = DoorCentreOnOuterWall();
        Vector3 a = new Vector3(Mathf.Min(rampTop.x, doorPoint.x) - rampWidth * 0.5f, innerMin.y - thick, Mathf.Min(rampTop.z, doorPoint.z));
        Vector3 b = new Vector3(Mathf.Max(rampTop.x, doorPoint.x) + rampWidth * 0.5f, innerMin.y, Mathf.Max(rampTop.z, doorPoint.z));
        if (doorSide == Side.PosX || doorSide == Side.NegX)
        {
            a = new Vector3(Mathf.Min(rampTop.x, doorPoint.x), innerMin.y - thick, Mathf.Min(rampTop.z, doorPoint.z) - rampWidth * 0.5f);
            b = new Vector3(Mathf.Max(rampTop.x, doorPoint.x), innerMin.y, Mathf.Max(rampTop.z, doorPoint.z) + rampWidth * 0.5f);
        }
        Box(parent, "Landing", a, b, hullMaterial, true);
    }

    Vector3 DoorCentreOnOuterWall()
    {
        Vector3 c = (innerMin + innerMax) * 0.5f;
        c.y = innerMin.y;
        switch (doorSide)
        {
            case Side.PosZ: c.z = outerMax.z; c.x += doorOffset; break;
            case Side.NegZ: c.z = outerMin.z; c.x += doorOffset; break;
            case Side.PosX: c.x = outerMax.x; c.z += doorOffset; break;
            case Side.NegX: c.x = outerMin.x; c.z += doorOffset; break;
        }
        return c;
    }

    // A box from min to max: a collider if solid, drawn with the material if one is given.
    static void Box(Transform parent, string name, Vector3 min, Vector3 max, Material visual, bool solid)
    {
        Vector3 size = max - min;
        if (size.x < SlabMin || size.y < SlabMin || size.z < SlabMin) return;
        if (!solid && visual == null) return;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (min + max) * 0.5f;
        if (solid) go.AddComponent<BoxCollider>().size = size;
        if (visual != null) AddVisual(go, size, visual);
    }

    static void AddVisual(GameObject go, Vector3 size, Material visual)
    {
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        go.AddComponent<MeshRenderer>().sharedMaterial = visual;
        // The cube mesh is 1 unit, so scale it here and shrink the collider to match.
        go.transform.localScale = size;
        var bc = go.GetComponent<BoxCollider>();
        if (bc != null) bc.size = Vector3.one;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube((innerMin + innerMax) * 0.5f, innerMax - innerMin);
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube((outerMin + outerMax) * 0.5f, outerMax - outerMin);

        Gizmos.color = Color.yellow;
        Vector3 d = DoorCentreOnOuterWall();
        bool alongX = doorSide == Side.PosX || doorSide == Side.NegX;
        Vector3 doorSize = alongX ? new Vector3(0.01f, doorHeight, doorWidth) : new Vector3(doorWidth, doorHeight, 0.01f);
        Gizmos.DrawWireCube(d + Vector3.up * doorHeight * 0.5f, doorSize);

        if (addEntryCorridor)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireCube((corridorMin + corridorMax) * 0.5f, corridorMax - corridorMin);
        }

        if (rampWidth > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(rampBottom, rampTop);
        }
    }
}
