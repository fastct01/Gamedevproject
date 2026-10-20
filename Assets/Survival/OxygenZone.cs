using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A circular area that refills the player's oxygen. Drawn in the game as a blue outline
/// (plus an optional faint fill) that hugs the ground. Change Radius in the Inspector or drag
/// the object to move it; the circle updates live in the editor.
/// </summary>
[ExecuteAlways]
public class OxygenZone : MonoBehaviour
{
    public static readonly List<OxygenZone> Active = new List<OxygenZone>();

    [Header("Area")]
    [Min(0.01f)] public float radius = 1.5f;
    [Tooltip("Only the player's horizontal distance counts; this limits how far above/below they can be.")]
    public float heightRange = 2f;
    [Tooltip("Also heals the player while they stand inside (e.g. the ship).")]
    public bool restoresHealth;

    [Header("Look")]
    public Color outlineColor = new Color(0.2f, 0.6f, 1f, 1f);
    public float outlineWidth = 0.03f;
    [Tooltip("Faint fill inside the circle. Set alpha to 0 for outline only.")]
    public Color fillColor = new Color(0.2f, 0.6f, 1f, 0.12f);
    [Range(16, 256)] public int segments = 96;
    [Tooltip("Project the circle onto the ground below it so it follows uneven terrain.")]
    public bool followGround = true;
    public float groundOffset = 0.01f;
    [Tooltip("Ground higher/lower than this (relative to the zone's position) is ignored, so the circle runs through rocks instead of climbing them.")]
    public float maxGroundStep = 0.1f;
    public LayerMask groundMask = ~0;
    [Tooltip("Gently pulse the outline.")]
    public bool pulse = true;

    LineRenderer _line;
    MeshFilter _fill;
    Material _lineMat, _fillMat;
    bool _dirty = true;
    Vector3 _lastPos;
    float _lastRadius;

    public bool Contains(Vector3 worldPos)
    {
        Vector3 d = worldPos - transform.position;
        if (Mathf.Abs(d.y) > heightRange) return false;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        _dirty = true;
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (_line != null) DestroySafe(_line.gameObject);
        if (_fill != null) DestroySafe(_fill.gameObject);
        DestroySafe(_lineMat);
        DestroySafe(_fillMat);
    }

    void OnValidate() => _dirty = true;

    void Update()
    {
        if (transform.position != _lastPos || !Mathf.Approximately(radius, _lastRadius)) _dirty = true;
        if (_dirty) Rebuild();

        if (pulse && Application.isPlaying && _lineMat != null)
        {
            var c = outlineColor;
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            _lineMat.SetColor("_BaseColor", c);
        }
    }

    void Rebuild()
    {
        _dirty = false;
        _lastPos = transform.position;
        _lastRadius = radius;
        EnsureObjects();

        var pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            pts[i] = GroundPoint(transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }

        _line.positionCount = segments;
        _line.SetPositions(pts);
        _line.widthMultiplier = outlineWidth;
        _lineMat.SetColor("_BaseColor", outlineColor);

        // Fan mesh in world space (the fill object sits at the origin with no rotation/scale).
        bool showFill = fillColor.a > 0.001f;
        _fill.gameObject.SetActive(showFill);
        if (showFill)
        {
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            // Centre at the average ring height, so objects inside the circle (like a ship) don't lift it.
            float avgY = 0f;
            foreach (var p in pts) avgY += p.y;
            avgY /= segments;
            verts[0] = new Vector3(transform.position.x, avgY, transform.position.z) - Vector3.up * groundOffset * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                verts[i + 1] = pts[i] - Vector3.up * groundOffset * 0.5f;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = _fill.sharedMesh;
            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _fillMat.SetColor("_BaseColor", fillColor);
        }
    }

    Vector3 GroundPoint(Vector3 p)
    {
        if (followGround)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * heightRange, Vector3.down, heightRange * 2f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                // Skip the player and anything that moves; we want static ground.
                if (hit.collider.attachedRigidbody != null || hit.collider is CharacterController) continue;
                if (Mathf.Abs(hit.point.y - transform.position.y) > maxGroundStep) continue;
                p.y = hit.point.y;
                break;
            }
        }
        return p + Vector3.up * groundOffset;
    }

    void EnsureObjects()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

        if (_line == null)
        {
            var go = new GameObject("OxygenZone Outline") { hideFlags = HideFlags.HideAndDontSave };
            _line = go.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.numCornerVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.alignment = LineAlignment.View;
            _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            MakeTransparent(_lineMat);
            _line.sharedMaterial = _lineMat;
        }

        if (_fill == null)
        {
            var go = new GameObject("OxygenZone Fill") { hideFlags = HideFlags.HideAndDontSave };
            _fill = go.AddComponent<MeshFilter>();
            _fill.sharedMesh = new Mesh { name = "OxygenZoneFill", hideFlags = HideFlags.HideAndDontSave };
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _fillMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            MakeTransparent(_fillMat);
            _fillMat.SetFloat("_Cull", 0f); // visible from both sides
            mr.sharedMaterial = _fillMat;
        }
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = outlineColor;
        const int n = 64;
        Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
