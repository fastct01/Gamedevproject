using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Menu: Tools > Spaceship > Build Spaceship. Creates a walk-in ship with an auto door in the open scene.</summary>
public static class SpaceshipBuilder
{
    const string MatFolder = "Assets/SpaceshipInterior/Materials";

    // Interior size (metres). Ship front (door) faces +Z.
    const float W = 8f, H = 3.6f, L = 12f, T = 0.3f;
    const float DoorW = 2.4f, DoorH = 2.8f;

    [MenuItem("Tools/Spaceship/Build Spaceship")]
    public static void Build()
    {
        EnsureFolders();
        var hull = Mat("Hull", new Color(0.55f, 0.58f, 0.62f), 0.8f, 0.5f);
        var floor = Mat("Floor", new Color(0.18f, 0.19f, 0.22f), 0.3f, 0.6f);
        var accent = Mat("Accent", new Color(0.1f, 0.6f, 1f), 0f, 0.5f, new Color(0.1f, 0.6f, 1f) * 2f);
        var panelMat = Mat("Door", new Color(0.35f, 0.38f, 0.42f), 0.9f, 0.6f);
        var dark = Mat("Console", new Color(0.08f, 0.09f, 0.11f), 0.5f, 0.4f);

        var root = new GameObject("Spaceship");
        Undo.RegisterCreatedObjectUndo(root, "Build Spaceship");

        // Floor and ceiling
        Box(root, "Floor", new Vector3(0, -T / 2, 0), new Vector3(W + 2 * T, T, L + 2 * T), floor);
        Box(root, "Ceiling", new Vector3(0, H + T / 2, 0), new Vector3(W + 2 * T, T, L + 2 * T), hull);

        // Side and back walls
        float wy = H / 2;
        Box(root, "Wall Left", new Vector3(-(W + T) / 2, wy, 0), new Vector3(T, H, L + 2 * T), hull);
        Box(root, "Wall Right", new Vector3((W + T) / 2, wy, 0), new Vector3(T, H, L + 2 * T), hull);
        Box(root, "Wall Back", new Vector3(0, wy, -(L + T) / 2), new Vector3(W, H, T), hull);

        // Front wall with doorway
        float fz = (L + T) / 2;
        float sideW = (W - DoorW) / 2;
        Box(root, "Front Left", new Vector3(-(DoorW + sideW) / 2, wy, fz), new Vector3(sideW, H, T), hull);
        Box(root, "Front Right", new Vector3((DoorW + sideW) / 2, wy, fz), new Vector3(sideW, H, T), hull);
        Box(root, "Front Lintel", new Vector3(0, DoorH + (H - DoorH) / 2, fz), new Vector3(DoorW, H - DoorH, T), hull);

        // Door panels (slide sideways into the wall thickness / behind the front wall)
        var doorRoot = new GameObject("Door");
        doorRoot.transform.SetParent(root.transform, false);
        doorRoot.transform.localPosition = new Vector3(0, 0, fz);
        var left = Panel(doorRoot, "Panel Left", new Vector3(-DoorW / 4, DoorH / 2, 0.05f), new Vector3(DoorW / 2, DoorH, T * 0.6f), panelMat, accent, 1);
        var right = Panel(doorRoot, "Panel Right", new Vector3(DoorW / 4, DoorH / 2, 0.05f), new Vector3(DoorW / 2, DoorH, T * 0.6f), panelMat, accent, -1);

        // Trigger zone around doorway (both sides)
        var trig = new GameObject("Door Trigger");
        trig.transform.SetParent(doorRoot.transform, false);
        trig.transform.localPosition = new Vector3(0, DoorH / 2, 0);
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(DoorW + 1f, DoorH, 6f);
        var door = trig.AddComponent<SlidingDoor>();
        door.panels = new[]
        {
            new SlidingDoor.Panel { transform = left.transform,  openOffset = new Vector3(-DoorW / 2 - 0.1f, 0, 0) },
            new SlidingDoor.Panel { transform = right.transform, openOffset = new Vector3( DoorW / 2 + 0.1f, 0, 0) },
        };
        // Interior: lights and furniture
        for (int i = 0; i < 3; i++)
        {
            float z = -L / 3 + i * L / 3;
            Box(root, "Ceiling Light " + i, new Vector3(0, H - 0.05f, z), new Vector3(1.5f, 0.1f, 0.4f), accent);
            var lg = new GameObject("Light " + i);
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0, H - 0.5f, z);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 8f; l.intensity = 6f; l.color = new Color(0.8f, 0.9f, 1f);
        }
        Box(root, "Console", new Vector3(0, 0.5f, -L / 2 + 1.2f), new Vector3(4f, 1f, 1.2f), dark);
        Box(root, "Console Screen", new Vector3(0, 1.25f, -L / 2 + 0.9f), new Vector3(3.2f, 0.9f, 0.1f), accent);
        Box(root, "Locker Left", new Vector3(-W / 2 + 0.5f, 1.1f, -1f), new Vector3(0.8f, 2.2f, 1.6f), dark);
        Box(root, "Locker Right", new Vector3(W / 2 - 0.5f, 1.1f, 1f), new Vector3(0.8f, 2.2f, 1.6f), dark);
        Box(root, "Floor Strip L", new Vector3(-1.2f, 0.01f, 0), new Vector3(0.1f, 0.02f, L - 1f), accent);
        Box(root, "Floor Strip R", new Vector3(1.2f, 0.01f, 0), new Vector3(0.1f, 0.02f, L - 1f), accent);

        // Exterior shell details: nose, engines, fins
        Box(root, "Nose", new Vector3(0, H / 2, L / 2 + 1.2f), new Vector3(W * 0.7f, H * 0.8f, 2.4f), hull);
        Box(root, "Fin Left", new Vector3(-W / 2 - 1.2f, 0.8f, -L / 2 + 1.5f), new Vector3(2.4f, 0.2f, 3f), hull);
        Box(root, "Fin Right", new Vector3(W / 2 + 1.2f, 0.8f, -L / 2 + 1.5f), new Vector3(2.4f, 0.2f, 3f), hull);
        for (int i = -1; i <= 1; i += 2)
        {
            var eng = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eng.name = "Engine " + i;
            eng.transform.SetParent(root.transform, false);
            eng.transform.localPosition = new Vector3(i * 2f, H / 2, -L / 2 - 0.9f);
            eng.transform.localRotation = Quaternion.Euler(90, 0, 0);
            eng.transform.localScale = new Vector3(1.4f, 0.8f, 1.4f);
            eng.GetComponent<Renderer>().sharedMaterial = dark;
        }

        // Place in front of the scene camera / at origin on top of ground
        root.transform.position = Vector3.zero;
        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Spaceship built. Move it into position, set the floor on your ground level, and save the scene.");
    }

    static GameObject Box(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat)
        => Box(parent.transform, name, pos, size, mat);

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Unscaled parent that moves with the door, holding the slab and a glowing edge strip.
    static GameObject Panel(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat, Material strip, int edgeSide)
    {
        var p = new GameObject(name);
        p.transform.SetParent(parent.transform, false);
        p.transform.localPosition = pos;
        Box(p, "Slab", Vector3.zero, size, mat);
        Box(p, "Strip", new Vector3(edgeSide * (size.x / 2 - 0.05f), 0, size.z / 2 + 0.03f), new Vector3(0.06f, 1f, 0.05f), strip);
        return p;
    }

    static Material Mat(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/SpaceshipInterior", "Materials");
    }
}
