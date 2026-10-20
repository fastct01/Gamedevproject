using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires a level light beam from its muzzle while a charged PowerCore sits in its cradle.
/// The beam bounces off BeamMirrors (splitters fork it), changes colour through BeamFilters,
/// and lights BeamReceivers. Drawn with LineRenderers.
/// </summary>
public class LightEmitter : MonoBehaviour
{
    public Transform muzzle;
    public SnapSocket cradle;
    [Tooltip("Seconds a full core powers the beam (0 = never runs out).")]
    public float drainSeconds = 90f;
    public float maxDistance = 12f;
    public int maxSegments = 16;
    public float beamWidth = 0.008f;
    public Material whiteBeam;
    public Material blueBeam;

    public bool Powered { get; private set; }

    readonly List<LineRenderer> _pool = new List<LineRenderer>();
    int _used;
    Collider[] _player;
    const float Eps = 0.002f;

    void Start()
    {
        var cc = FindFirstObjectByType<CharacterController>();
        _player = cc != null ? cc.GetComponentsInChildren<Collider>(true) : new Collider[0];
    }

    void Update()
    {
        _used = 0;
        PowerCore core = cradle != null && cradle.Current != null ? cradle.Current.GetComponent<PowerCore>() : null;
        Powered = core != null && core.charge > 0f;
        if (Powered && muzzle != null)
        {
            if (drainSeconds > 0f) core.Drain(Time.deltaTime / drainSeconds); // 0 = never runs out
            Vector3 dir = muzzle.forward; dir.y = 0f;
            Trace(muzzle.position, dir.normalized, BeamColor.White, 0);
        }
        for (int i = _used; i < _pool.Count; i++) _pool[i].enabled = false;
    }

    void Trace(Vector3 origin, Vector3 dir, BeamColor color, int depth)
    {
        if (depth >= maxSegments) return;

        RaycastHit hit = default;
        bool any = false;
        float best = maxDistance;
        foreach (var h in Physics.RaycastAll(origin, dir, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= best || System.Array.IndexOf(_player, h.collider) >= 0) continue;
            best = h.distance; hit = h; any = true;
        }

        if (!any) { Draw(origin, origin + dir * maxDistance, color); return; }
        Draw(origin, hit.point, color);

        var mirror = hit.collider.GetComponentInParent<BeamMirror>();
        if (mirror != null)
        {
            Vector3 n = mirror.transform.forward; n.y = 0f; n.Normalize();
            // Only the mirror face reflects (its normal is the mirror's forward). Prism backs / box edges block.
            if (Mathf.Abs(Vector3.Dot(hit.normal, n)) < 0.9f) return;
            Vector3 r = Vector3.Reflect(dir, n); r.y = 0f; r.Normalize();
            Trace(hit.point + r * Eps, r, color, depth + 1);
            if (mirror.splitter) Trace(hit.point + dir * Eps, dir, color, depth + 1);
            return;
        }

        var filter = hit.collider.GetComponentInParent<BeamFilter>();
        if (filter != null)
        {
            // Rays starting inside a collider ignore it, so the beam carries on through the filter.
            Trace(hit.point + dir * Eps, dir, filter.color, depth + 1);
            return;
        }

        var receiver = hit.collider.GetComponentInParent<BeamReceiver>();
        if (receiver != null) receiver.ReportHit(color);
    }

    void Draw(Vector3 a, Vector3 b, BeamColor color)
    {
        LineRenderer lr;
        if (_used < _pool.Count) lr = _pool[_used];
        else
        {
            var go = new GameObject("Beam Segment");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            _pool.Add(lr);
        }
        _used++;
        lr.enabled = true;
        lr.widthMultiplier = beamWidth;
        lr.sharedMaterial = color == BeamColor.Blue ? blueBeam : whiteBeam;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }
}
