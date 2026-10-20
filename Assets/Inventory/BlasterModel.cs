using UnityEngine;

/// <summary>Builds the blaster out of simple shapes (used for both the pickup and the one in your hand).</summary>
public static class BlasterModel
{
    static Material _body, _dark, _glow;

    /// <summary>Creates the model under parent, pointing along +Z. Size is in the parent's units (~0.3 long).</summary>
    public static Transform Create(Transform parent, out Transform muzzle)
    {
        EnsureMaterials();
        var root = new GameObject("Blaster Model").transform;
        root.SetParent(parent, false);

        Part(root, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.07f, 0.08f, 0.24f), _body);
        Part(root, PrimitiveType.Cube, "Top Rail", new Vector3(0f, 0.05f, -0.01f), Vector3.zero, new Vector3(0.035f, 0.02f, 0.18f), _dark);
        Part(root, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.01f, 0.17f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.06f, 0.04f), _dark);
        Part(root, PrimitiveType.Cylinder, "Muzzle Ring", new Vector3(0f, 0.01f, 0.235f), new Vector3(90f, 0f, 0f), new Vector3(0.055f, 0.008f, 0.055f), _glow);
        Part(root, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.08f, -0.07f), new Vector3(-15f, 0f, 0f), new Vector3(0.05f, 0.12f, 0.06f), _dark);
        Part(root, PrimitiveType.Cube, "Energy Cell L", new Vector3(-0.037f, 0.005f, 0.02f), Vector3.zero, new Vector3(0.006f, 0.03f, 0.12f), _glow);
        Part(root, PrimitiveType.Cube, "Energy Cell R", new Vector3(0.037f, 0.005f, 0.02f), Vector3.zero, new Vector3(0.006f, 0.03f, 0.12f), _glow);

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = new Vector3(0f, 0.01f, 0.25f);
        return root;
    }

    static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
    {
        var p = GameObject.CreatePrimitive(type);
        p.name = name;
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localRotation = Quaternion.Euler(euler);
        p.transform.localScale = scale;
        var r = p.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void EnsureMaterials()
    {
        if (_body != null) return;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        _body = new Material(lit) { name = "Blaster Body" };
        _body.SetColor("_BaseColor", new Color(0.78f, 0.8f, 0.84f)); _body.SetFloat("_Metallic", 0.6f); _body.SetFloat("_Smoothness", 0.6f);
        _dark = new Material(lit) { name = "Blaster Dark" };
        _dark.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.15f)); _dark.SetFloat("_Smoothness", 0.4f);
        _glow = new Material(lit) { name = "Blaster Glow" };
        _glow.SetColor("_BaseColor", new Color(0.2f, 0.9f, 1f));
        _glow.EnableKeyword("_EMISSION");
        _glow.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 1f) * 2.5f);
    }
}
