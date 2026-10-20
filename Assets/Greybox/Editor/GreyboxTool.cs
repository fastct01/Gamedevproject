using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Greybox > Enable: hides every textured model in the open scene and puts a plain grey Unity shape
/// (cube / capsule / cylinder) in its place. Heavy mesh colliders are swapped for box colliders.
/// Lights, scripts and gameplay objects are left alone.
/// Tools > Greybox > Disable puts everything back.
/// </summary>
public static class GreyboxTool
{
    const string MatDir = "Assets/Greybox/Materials";

    [MenuItem("Tools/Greybox/Enable Greybox Mode")]
    public static void Enable()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Greybox: stop Play mode first - changes made while playing are thrown away."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        int models = 0, swaps = 0, shells = 0;
        Undo.SetCurrentGroupName("Enable Greybox Mode");
        int group = Undo.GetCurrentGroup();

        var renderers = new List<MeshRenderer>();
        foreach (var root in scene.GetRootGameObjects())
            renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));

        foreach (var mr in renderers)
        {
            if (mr == null || mr.GetComponent<GreyboxRecord>() || mr.GetComponentInParent<GreyboxProxy>()) continue;
            if (mr.GetComponentInParent<GreyboxKeep>(true) != null) continue;
            if (mr.gameObject.hideFlags != HideFlags.None) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            bool builtIn = string.IsNullOrEmpty(meshPath) || !meshPath.StartsWith("Assets/");

            if (builtIn)
            {
                // Unity primitive: only swap the material if it uses a texture.
                if (!HasTexture(mr.sharedMaterials)) continue;
                var rec = Undo.AddComponent<GreyboxRecord>(mr.gameObject);
                rec.materialSwapOnly = true;
                rec.originalMaterials = mr.sharedMaterials;
                Undo.RecordObject(mr, "Greybox");
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = Mat("Default", new Color(0.7f, 0.7f, 0.7f));
                mr.sharedMaterials = mats;
                swaps++;
                continue;
            }

            // Imported model: hide it and add a plain stand-in.
            var record = Undo.AddComponent<GreyboxRecord>(mr.gameObject);
            record.rendererWasEnabled = mr.enabled;
            Undo.RecordObject(mr, "Greybox");
            mr.enabled = false;

            var meshColliders = new List<MeshCollider>();
            foreach (var mc in mr.GetComponents<MeshCollider>())
                if (mc.enabled)
                {
                    Undo.RecordObject(mc, "Greybox");
                    mc.enabled = false;
                    meshColliders.Add(mc);
                }
            record.disabledColliders = meshColliders.ToArray();

            // The walk-in ship already has box shells around its interior: show those instead of a solid block.
            var walkIn = mr.GetComponentInParent<WalkInShipColliders>();
            if (walkIn != null)
            {
                shells += ShowWalkInShell(walkIn);
                continue;
            }

            string category = Category(mr.transform);
            // If the object already has a box collider, match the stand-in to it so it lines up with
            // what you actually walk on / bump into. Otherwise fit it to the mesh.
            var box = mr.GetComponent<BoxCollider>();
            if (box != null && box.enabled && !box.isTrigger)
                record.proxy = MakeProxy(mr.transform, new Bounds(box.center, box.size), category, false);
            else
                record.proxy = MakeProxy(mr.transform, mf.sharedMesh.bounds, category, meshColliders.Count > 0);
            models++;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Greybox mode ON: {models} models replaced with basic shapes, {swaps} textured materials swapped, {shells} ship shell pieces shown. Tools > Greybox > Disable to restore.");
    }

    [MenuItem("Tools/Greybox/Disable Greybox Mode (restore textures)")]
    public static void Disable()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Greybox: stop Play mode first - changes made while playing are thrown away."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Disable Greybox Mode");
        int group = Undo.GetCurrentGroup();
        int restored = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var rec in root.GetComponentsInChildren<GreyboxRecord>(true))
            {
                var mr = rec.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Undo.RecordObject(mr, "Greybox");
                    if (rec.materialSwapOnly) mr.sharedMaterials = rec.originalMaterials;
                    else mr.enabled = rec.rendererWasEnabled;
                }
                if (rec.disabledColliders != null)
                    foreach (var mc in rec.disabledColliders)
                        if (mc != null) { Undo.RecordObject(mc, "Greybox"); mc.enabled = true; }
                if (rec.proxy != null) Undo.DestroyObjectImmediate(rec.proxy);
                Undo.DestroyObjectImmediate(rec);
                restored++;
            }
            // Any leftover stand-ins (e.g. ship shell visuals).
            foreach (var p in root.GetComponentsInChildren<GreyboxProxy>(true))
                if (p != null) Undo.DestroyObjectImmediate(p.gameObject);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Greybox mode OFF: {restored} objects restored.");
    }

    // ---------- helpers ----------

    static GameObject MakeProxy(Transform parent, Bounds b, string category, bool addCollider)
    {
        PrimitiveType type = category == "Crystal" ? PrimitiveType.Capsule
                           : category == "Flashlight" ? PrimitiveType.Cylinder
                           : PrimitiveType.Cube;
        var go = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(go, "Greybox");
        go.name = "Greybox " + category;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = b.center;
        go.transform.localRotation = Quaternion.identity;

        Vector3 s = b.size;
        if (type == PrimitiveType.Cube) go.transform.localScale = s;
        else go.transform.localScale = new Vector3(s.x, s.y * 0.5f, s.z); // capsule/cylinder are 2 units tall

        var col = go.GetComponent<Collider>();
        if (!addCollider || category == "Flashlight") Object.DestroyImmediate(col);
        else if (type == PrimitiveType.Capsule) { Object.DestroyImmediate(col); go.AddComponent<BoxCollider>(); }

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Mat(category, CategoryColor(category));
        if (category == "Flashlight") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<GreyboxProxy>();
        return go;
    }

    // Give the ship's invisible collider boxes a visible grey cube each.
    static int ShowWalkInShell(WalkInShipColliders ship)
    {
        var shell = ship.transform.Find("WalkInColliders");
        if (shell == null) return 0;
        int n = 0;
        foreach (var bc in shell.GetComponentsInChildren<BoxCollider>(true))
        {
            if (bc.isTrigger || bc.GetComponent<MeshRenderer>() || bc.GetComponentInParent<GreyboxProxy>()) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Greybox");
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Greybox Ship";
            go.transform.SetParent(bc.transform, false);
            go.transform.localPosition = bc.center;
            go.transform.localScale = bc.size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat("Ship", CategoryColor("Ship"));
            go.AddComponent<GreyboxProxy>();
            n++;
        }
        return n;
    }

    static string Category(Transform t)
    {
        if (t.GetComponentInParent<PlayerFlashlight>() != null) return "Flashlight";
        string root = t.root.name.ToLowerInvariant();
        if (root.Contains("ground")) return "Ground";
        if (root.Contains("crystal")) return "Crystal";
        if (root.Contains("rock")) return "Rock";
        if (root.Contains("ship")) return "Ship";
        return "Default";
    }

    static Color CategoryColor(string c)
    {
        switch (c)
        {
            case "Ground": return new Color(0.32f, 0.32f, 0.34f);
            case "Rock": return new Color(0.5f, 0.5f, 0.52f);
            case "Crystal": return new Color(0.62f, 0.72f, 0.82f);
            case "Ship": return new Color(0.85f, 0.85f, 0.88f);
            case "Flashlight": return new Color(0.2f, 0.2f, 0.22f);
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Greybox", "Materials");
        string path = $"{MatDir}/Greybox_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static bool HasTexture(Material[] mats)
    {
        foreach (var m in mats)
        {
            if (m == null) continue;
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                if (m.HasProperty(prop) && m.GetTexture(prop) != null) return true;
        }
        return false;
    }
}
