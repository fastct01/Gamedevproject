using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Inventory > Set Up Inventory, Blaster, Gear and Ship Lights:
/// creates the item assets, adds the inventory/blaster to the player, adds the ship's (unpowered) interior lights,
/// and rebuilds both puzzles so their rewards (blaster in the Weighing Gate cache, gear in the Lightwell) are placed.
/// </summary>
public static class InventoryBuilder
{
    const string Dir = "Assets/Inventory/Items";

    [MenuItem("Tools/Inventory/Set Up Inventory, Blaster, Gear and Ship Lights")]
    public static void SetUp()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        EnsureItems();

        var player = Object.FindFirstObjectByType<CharacterController>();
        if (player != null)
        {
            var inv = player.GetComponent<Inventory>();
            if (inv == null) inv = Undo.AddComponent<Inventory>(player.gameObject);
            if (player.GetComponent<InventoryUI>() == null) Undo.AddComponent<InventoryUI>(player.gameObject);
            var blaster = player.GetComponent<Blaster>();
            if (blaster == null) blaster = Undo.AddComponent<Blaster>(player.gameObject);
            Undo.RecordObject(inv, "Inventory start");
            Undo.RecordObject(blaster, "Blaster");
            // The torch starts in slot 1, in your hand. Slots 2-3 unlock with the blaster.
            if (inv.hotbar == null || inv.hotbar.Length != Inventory.HotbarSize) inv.hotbar = new InventoryItem[Inventory.HotbarSize];
            for (int i = 0; i < inv.hotbar.Length; i++) inv.hotbar[i] = null;
            inv.hotbar[0] = TorchItem;
            inv.unlockedSlots = 1;
            inv.startEquippedSlot = 0;
            inv.collectables.Clear();
            blaster.viewOffset = new Vector3(0.2f, -0.13f, 0.7f); // right hand (the torch is put away while it's out)
            EditorUtility.SetDirty(inv);
            EditorUtility.SetDirty(blaster);
        }

        BuildShipLights();
        AddParkourReward();
        PuzzleBuilder.BuildWeighingGate(); // places the blaster reward
        PuzzleBuilder.BuildLightwell();    // places the gear reward
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Inventory set up: blaster in the Weighing Gate cache, gear in the Lightwell, ship lights waiting for the gear.");
    }

    // ---------------------------------------------------------------- items
    public static InventoryItem BlasterItem => EnsureItems().blaster;
    public static InventoryItem GearItem => EnsureItems().gear;
    public static InventoryItem TorchItem => EnsureItems().torch;
    public static InventoryItem OxygenBoostItem => EnsureItems().boost;

    static (InventoryItem blaster, InventoryItem gear, InventoryItem torch, InventoryItem boost) EnsureItems()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Inventory", "Items");
        var blaster = Item("Blaster", i =>
        {
            i.id = "blaster"; i.displayName = "Blaster";
            i.description = "Stuns creatures for a few seconds. Left-click to fire.";
            i.unlocksHotbarSlots = 3; i.showCrosshair = true;
            i.iconStyle = ItemIconStyle.Blaster; i.iconColor = new Color(0.4f, 0.9f, 1f);
        });
        var gear = Item("ShipGear", i =>
        {
            i.id = "ship_gear"; i.displayName = "Ship Part: Power Gear"; i.collectable = true;
            i.description = "Restores power to the ship's interior lights. Bring it back to the ship.";
            i.iconStyle = ItemIconStyle.Gear; i.iconColor = new Color(1f, 0.78f, 0.35f);
        });
        var torch = Item("Torch", i =>
        {
            i.id = "torch"; i.displayName = "Torch";
            i.description = "Lights the way. F switches it on/off. The monster can see you while it's on.";
            i.iconStyle = ItemIconStyle.Torch; i.iconColor = new Color(1f, 0.92f, 0.6f);
        });
        var boost = Item("OxygenBoost", i =>
        {
            i.id = "oxygen_boost"; i.displayName = "Oxygen Boost";
            i.description = "Press its slot number to use: refills your oxygen and stops it draining for 2 minutes.";
            i.consumable = true; i.oxygenBoostSeconds = 120f;
            i.unlocksHotbarSlots = 2; // makes room for itself next to the torch
            i.iconStyle = ItemIconStyle.OxygenTank; i.iconColor = new Color(0.35f, 0.75f, 1f);
        });
        return (blaster, gear, torch, boost);
    }

    static InventoryItem Item(string file, System.Action<InventoryItem> setup)
    {
        string path = $"{Dir}/{file}.asset";
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
        if (item != null) return item;
        item = ScriptableObject.CreateInstance<InventoryItem>();
        setup(item);
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    // ---------------------------------------------------------------- rewards (called by PuzzleBuilder)
    /// <summary>Blaster on a small pedestal (Weighing Gate cache).</summary>
    public static void AddBlasterPickup(Transform parent, Vector3 groundPoint)
    {
        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Puzzles/Materials/Stone.mat");
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ped.name = "Blaster Pedestal";
        ped.transform.localScale = new Vector3(0.12f, 0.07f, 0.12f);
        ped.transform.position = groundPoint + Vector3.up * 0.035f;
        ped.transform.SetParent(parent, true);
        if (stone != null) ped.GetComponent<Renderer>().sharedMaterial = stone;

        var root = new GameObject("Blaster (pickup)");
        root.transform.position = groundPoint + Vector3.up * 0.1f;
        root.transform.SetParent(parent, true);
        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.06f, 0.05f, 0.07f);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        visual.localScale = Vector3.one * 0.2f;
        BlasterModel.Create(visual, out _);
        var pickup = root.AddComponent<ItemPickup>();
        pickup.item = BlasterItem;
        pickup.visual = visual;
        AddGlow(root.transform, new Color(0.3f, 0.9f, 1f));
    }

    /// <summary>The ship-part gear, standing up and spinning (Lightwell, revealed when solved).</summary>
    public static void AddGearPickup(Transform parent, Vector3 groundPoint)
    {
        var root = new GameObject("Ship Gear (pickup)");
        root.transform.position = groundPoint + Vector3.up * 0.07f;
        root.transform.SetParent(parent, true);
        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.05f;
        var spin = new GameObject("Visual").transform;
        spin.SetParent(root.transform, false);
        var gear = new GameObject("Gear", typeof(MeshFilter), typeof(MeshRenderer));
        gear.transform.SetParent(spin, false);
        gear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // stand it up
        gear.GetComponent<MeshFilter>().sharedMesh = GearMesh();
        gear.GetComponent<MeshRenderer>().sharedMaterial = GearMaterial();
        var pickup = root.AddComponent<ItemPickup>();
        pickup.item = GearItem;
        pickup.visual = spin;
        AddGlow(root.transform, new Color(1f, 0.75f, 0.35f));
    }

    [MenuItem("Tools/Inventory/Place Oxygen Boost on Parkour Platform")]
    public static void PlaceParkourReward()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        AddParkourReward();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Oxygen Boost placed on the parkour platform. Save the scene (Cmd+S).");
    }

    // The parkour on Alien Ground 1: push-cube steps up to the floating "Cube" platform. The boost waits on top.
    static void AddParkourReward()
    {
        var platform = GameObject.Find("Cube");
        if (platform == null) { Debug.LogWarning("Parkour platform 'Cube' not found - oxygen boost not placed."); return; }
        if (platform.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(platform); // so you can stand on it
        var old = GameObject.Find("Oxygen Boost (pickup)");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var b = platform.GetComponent<Renderer>().bounds;
        var root = new GameObject("Oxygen Boost (pickup)");
        Undo.RegisterCreatedObjectUndo(root, "Oxygen boost");
        root.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.05f, 0.08f, 0.05f);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        var tankMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inventory/Items/OxygenTank.mat");
        if (tankMat == null)
        {
            tankMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tankMat.SetColor("_BaseColor", new Color(0.25f, 0.6f, 1f));
            tankMat.SetFloat("_Metallic", 0.5f);
            tankMat.SetFloat("_Smoothness", 0.7f);
            tankMat.EnableKeyword("_EMISSION");
            tankMat.SetColor("_EmissionColor", new Color(0.05f, 0.25f, 0.6f));
            AssetDatabase.CreateAsset(tankMat, "Assets/Inventory/Items/OxygenTank.mat");
        }
        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Puzzles/Materials/Emitter.mat");
        void Part(PrimitiveType type, string name, Vector3 lp, Vector3 scale, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(visual, false);
            p.transform.localPosition = lp;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = m;
        }
        Part(PrimitiveType.Cylinder, "Canister", new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.025f, 0.035f), tankMat);
        Part(PrimitiveType.Sphere, "Dome", new Vector3(0f, 0.025f, 0f), new Vector3(0.035f, 0.02f, 0.035f), tankMat);
        Part(PrimitiveType.Cylinder, "Valve", new Vector3(0f, 0.04f, 0f), new Vector3(0.012f, 0.006f, 0.012f), metal);
        Part(PrimitiveType.Cylinder, "Band", new Vector3(0f, -0.005f, 0f), new Vector3(0.037f, 0.003f, 0.037f), metal);

        var pickup = root.AddComponent<ItemPickup>();
        pickup.item = OxygenBoostItem;
        pickup.visual = visual;
        AddGlow(root.transform, new Color(0.35f, 0.75f, 1f));
    }

    static void AddGlow(Transform parent, Color c)
    {
        var g = new GameObject("Glow", typeof(Light));
        g.transform.SetParent(parent, false);
        var l = g.GetComponent<Light>();
        l.type = LightType.Point; l.range = 0.3f; l.intensity = 0.08f; l.color = c; l.shadows = LightShadows.None;
    }

    static Material GearMaterial()
    {
        const string path = "Assets/Inventory/Items/GearBrass.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.85f, 0.65f, 0.28f));
        m.SetFloat("_Metallic", 0.9f);
        m.SetFloat("_Smoothness", 0.65f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.25f, 0.15f, 0.03f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// <summary>A flat gear: 10 teeth, hole in the middle, axis along Y.</summary>
    static Mesh GearMesh()
    {
        const string path = "Assets/Inventory/Items/GearMesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const int teeth = 10;
        const float rOut = 0.05f, rRoot = 0.04f, rHole = 0.014f, half = 0.007f;
        var outline = new List<float>();   // angles
        var radii = new List<float>();
        float seg = Mathf.PI * 2f / teeth;
        for (int t = 0; t < teeth; t++)
        {
            float a0 = t * seg;
            outline.Add(a0);               radii.Add(rRoot);
            outline.Add(a0 + seg * 0.15f); radii.Add(rOut);
            outline.Add(a0 + seg * 0.45f); radii.Add(rOut);
            outline.Add(a0 + seg * 0.6f);  radii.Add(rRoot);
            outline.Add(a0 + seg * 0.8f);  radii.Add(rRoot);
        }
        int n = outline.Count;
        Vector3 O(int i, float y) => new Vector3(Mathf.Cos(outline[i]) * radii[i], y, Mathf.Sin(outline[i]) * radii[i]);
        Vector3 H(int i, float y) => new Vector3(Mathf.Cos(outline[i]) * rHole, y, Mathf.Sin(outline[i]) * rHole);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var tmp = b; b = c; c = tmp; }
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) { Tri(a, b, c, outward); Tri(a, c, d, outward); }

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Quad(O(i, half), O(j, half), H(j, half), H(i, half), Vector3.up);          // top
            Quad(O(i, -half), O(j, -half), H(j, -half), H(i, -half), Vector3.down);    // bottom
            Vector3 e = O(j, 0) - O(i, 0);
            Quad(O(i, half), O(j, half), O(j, -half), O(i, -half), new Vector3(e.z, 0, -e.x));   // outer rim
            Vector3 h = H(j, 0) - H(i, 0);
            Quad(H(i, half), H(j, half), H(j, -half), H(i, -half), new Vector3(-h.z, 0, h.x));   // hole wall
        }

        var mesh = new Mesh { name = "Gear" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------------------------------------------------------------- ship lights
    static void BuildShipLights()
    {
        var ship = GameObject.Find("SpaceShip_2");
        if (ship == null) { Debug.LogWarning("SpaceShip_2 not found - skipping ship lights."); return; }
        var old = ship.transform.Find("Interior Lights (powered)");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var group = new GameObject("Interior Lights (powered)");
        Undo.RegisterCreatedObjectUndo(group, "Ship lights");
        group.transform.SetParent(ship.transform, false);

        var strip = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inventory/Items/ShipLightStrip.mat");
        if (strip == null)
        {
            strip = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            strip.SetColor("_BaseColor", new Color(1.6f, 1.5f, 1.3f));
            AssetDatabase.CreateAsset(strip, "Assets/Inventory/Items/ShipLightStrip.mat");
        }

        // Ship-local positions (see WalkInShipColliders: room behind the door, corridor in front of it).
        void Light(string name, Vector3 local, float range, float intensity)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = local;
            var l = go.GetComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity;
            l.color = new Color(1f, 0.93f, 0.82f);
            l.shadows = LightShadows.None; // no shadow maps -> no hitching inside the ship
        }
        void Strip(string name, Vector3 local, Vector3 size)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = name;
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(group.transform, false);
            s.transform.localPosition = local;
            s.transform.localScale = size;
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = strip;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Light("Room Light", new Vector3(0f, 0.03f, -0.42f), 0.55f, 0.12f);
        Light("Corridor Light", new Vector3(0f, 0.0f, 0.1f), 0.35f, 0.06f);
        Strip("Ceiling Strip L", new Vector3(-0.15f, 0.064f, -0.42f), new Vector3(0.02f, 0.006f, 0.6f));
        Strip("Ceiling Strip R", new Vector3(0.15f, 0.064f, -0.42f), new Vector3(0.02f, 0.006f, 0.6f));
        Strip("Corridor Strip", new Vector3(0f, 0.044f, 0.11f), new Vector3(0.02f, 0.006f, 0.25f));
        group.SetActive(false);

        var power = ship.GetComponent<ShipPower>();
        if (power == null) power = Undo.AddComponent<ShipPower>(ship);
        power.shipZone = null;
        power.autoInstallInZone = false; // installed through the wall fixture instead
        power.poweredObjects = new[] { group };

        BuildGearFixture(ship.transform, power);
    }

    // Socket on the back wall of the room inside the ship. Right-click it with the gear to install it.
    static void BuildGearFixture(Transform ship, ShipPower power)
    {
        var old = ship.Find("Gear Fixture");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var walkIn = ship.GetComponent<WalkInShipColliders>();
        float backZ = walkIn != null ? walkIn.innerMin.z : -0.78f;
        float midY = walkIn != null ? (walkIn.innerMin.y + walkIn.innerMax.y) * 0.5f : -0.065f;

        var root = new GameObject("Gear Fixture");
        Undo.RegisterCreatedObjectUndo(root, "Gear fixture");
        root.transform.SetParent(ship, false);
        root.transform.localPosition = new Vector3(0f, midY, backZ + 0.012f); // on the wall, facing into the room
        root.transform.localRotation = Quaternion.identity;                  // +Z = out of the wall

        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Puzzles/Materials/Emitter.mat");
        var lampMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Inventory/Items/FixtureLamp.mat");
        if (lampMat == null)
        {
            lampMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lampMat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f));
            lampMat.EnableKeyword("_EMISSION");
            lampMat.SetColor("_EmissionColor", new Color(2f, 0.3f, 0.2f));
            AssetDatabase.CreateAsset(lampMat, "Assets/Inventory/Items/FixtureLamp.mat");
        }

        GameObject Part(PrimitiveType type, string name, Vector3 lp, Vector3 euler, Vector3 scale, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(root.transform, false);
            p.transform.localPosition = lp;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            if (m != null) p.GetComponent<Renderer>().sharedMaterial = m;
            return p;
        }

        // Wall plate, a round socket with an axle, and a status lamp above it.
        Part(PrimitiveType.Cube, "Wall Plate", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.15f, 0.15f, 0.012f), dark);
        Part(PrimitiveType.Cylinder, "Socket Ring", new Vector3(0f, 0f, 0.008f), new Vector3(90f, 0f, 0f), new Vector3(0.115f, 0.004f, 0.115f), GearMaterial());
        Part(PrimitiveType.Cylinder, "Socket Hole", new Vector3(0f, 0f, 0.011f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.002f, 0.1f), dark);
        Part(PrimitiveType.Cylinder, "Axle", new Vector3(0f, 0f, 0.018f), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 0.01f, 0.022f), GearMaterial());
        var lamp = Part(PrimitiveType.Sphere, "Status Lamp", new Vector3(0f, 0.062f, 0.01f), Vector3.zero, Vector3.one * 0.014f, lampMat);

        // The gear that appears on the axle once installed (axis out of the wall).
        var mounted = new GameObject("Mounted Gear", typeof(MeshFilter), typeof(MeshRenderer));
        mounted.transform.SetParent(root.transform, false);
        mounted.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        mounted.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // gear mesh axis (Y) -> out of the wall
        mounted.GetComponent<MeshFilter>().sharedMesh = GearMesh();
        mounted.GetComponent<MeshRenderer>().sharedMaterial = GearMaterial();
        mounted.SetActive(false);

        // Click target.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.015f);
        col.size = new Vector3(0.15f, 0.15f, 0.04f);

        var fixture = root.AddComponent<ShipGearFixture>();
        fixture.power = power;
        fixture.mountedGear = mounted;
        fixture.statusLamp = lamp.GetComponent<Renderer>();
    }
}
