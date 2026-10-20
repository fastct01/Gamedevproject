using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Tools > Monster > Build Red Pill Monster: territory over Alien Ground 1, the monster, and player health + HUD.</summary>
public static class MonsterBuilder
{
    [MenuItem("Tools/Monster/Build Red Pill Monster (Alien Ground 1)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }

        // Territory = the MassiveAliendGround (1) area.
        GameObject ground = null;
        foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "MassiveAliendGround (1)") ground = r;
        if (ground == null) { Debug.LogWarning("MassiveAliendGround (1) not found."); return; }
        Bounds gb = default; bool any = false;
        foreach (var c in ground.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!any) { gb = c.bounds; any = true; } else gb.Encapsulate(c.bounds);
        }

        foreach (var name in new[] { "Monster Territory (Alien Ground 1)", "Red Pill Monster" })
        {
            var old = GameObject.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        float groundTop = 0.937f;
        var terr = new GameObject("Monster Territory (Alien Ground 1)");
        Undo.RegisterCreatedObjectUndo(terr, "Monster");
        terr.transform.position = new Vector3(gb.center.x, groundTop + 0.4f, gb.center.z);
        var box = terr.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(gb.size.x, 1.4f, gb.size.z);
        var territory = terr.AddComponent<MonsterTerritory>();

        // The monster: a red pill a bit bigger than the player, with eyes and a faint red glow.
        var monster = new GameObject("Red Pill Monster");
        Undo.RegisterCreatedObjectUndo(monster, "Monster");
        monster.transform.position = new Vector3(gb.center.x + 2.4f, groundTop, gb.center.z - 2.2f);
        var col = monster.AddComponent<CapsuleCollider>();
        col.radius = territory.agentRadius;
        col.height = territory.agentHeight;
        col.center = new Vector3(0f, territory.agentHeight * 0.5f, 0f);
        var rb = monster.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var agent = monster.AddComponent<NavMeshAgent>();
        agent.enabled = false;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(monster.transform, false);
        body.transform.localPosition = new Vector3(0f, territory.agentHeight * 0.5f, 0f);
        body.transform.localScale = new Vector3(territory.agentRadius * 2f, territory.agentHeight * 0.5f, territory.agentRadius * 2f);
        body.GetComponent<Renderer>().sharedMaterial = Mat("MonsterRed", new Color(0.75f, 0.05f, 0.05f), new Color(0.6f, 0.02f, 0.02f));
        var eyeMat = Mat("MonsterEye", new Color(0.02f, 0.02f, 0.02f), new Color(1f, 0.85f, 0.2f) * 1.5f);
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(monster.transform, false);
            eye.transform.localPosition = new Vector3(i * 0.011f, territory.agentHeight * 0.78f, territory.agentRadius * 0.85f);
            eye.transform.localScale = Vector3.one * 0.011f;
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
        }
        var glowGo = new GameObject("Glow", typeof(Light));
        glowGo.transform.SetParent(monster.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, territory.agentHeight * 0.6f, 0f);
        var glow = glowGo.GetComponent<Light>();
        glow.type = LightType.Point; glow.range = 0.45f; glow.intensity = 0.04f; glow.color = new Color(1f, 0.1f, 0.05f);

        var ai = monster.AddComponent<MonsterAI>();
        ai.territory = territory;
        ai.bodyRenderer = body.GetComponent<Renderer>();
        ai.glow = glow;

        // Player health + HUD.
        var cc = Object.FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            if (cc.GetComponent<PlayerHealth>() == null) Undo.AddComponent<PlayerHealth>(cc.gameObject);
            if (cc.GetComponent<HealthBarUI>() == null) Undo.AddComponent<HealthBarUI>(cc.gameObject);
        }

        Selection.activeGameObject = monster;
        EditorSceneManager.MarkSceneDirty(monster.scene);
        Debug.Log($"Red pill monster built. Territory {gb.min:F2} to {gb.max:F2}.");
    }

    static Material Mat(string name, Color color, Color emission)
    {
        const string dir = "Assets/Monster/Materials";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Monster", "Materials");
        string path = $"{dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.5f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
