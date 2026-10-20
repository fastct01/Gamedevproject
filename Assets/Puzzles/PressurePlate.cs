using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lights up when the total mass of objects resting on it is within [minMass, maxMass].
/// Needs a trigger BoxCollider covering the space just above the plate. Held objects (no gravity) don't count.
/// Shows: dim = empty, cyan = correct weight, flickering red = wrong weight.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PressurePlate : MonoBehaviour
{
    public float minMass = 1f;
    public float maxMass = 5f;
    public Renderer indicator;
    public Color idleColor = new Color(0.25f, 0.27f, 0.3f);
    public Color okColor = new Color(0.2f, 0.9f, 1f);
    public Color wrongColor = new Color(1f, 0.2f, 0.15f);

    public float TotalMass { get; private set; }
    public bool IsSatisfied => TotalMass >= minMass && TotalMass <= maxMass;

    readonly Dictionary<Rigidbody, int> _bodies = new Dictionary<Rigidbody, int>();
    readonly List<Rigidbody> _stale = new List<Rigidbody>();
    Material _mat;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (indicator != null)
        {
            _mat = indicator.material;
            _mat.EnableKeyword("_EMISSION");
        }
    }

    void OnTriggerEnter(Collider c)
    {
        var rb = c.attachedRigidbody;
        if (rb == null) return;
        _bodies.TryGetValue(rb, out int n);
        _bodies[rb] = n + 1;
    }

    void OnTriggerExit(Collider c)
    {
        var rb = c.attachedRigidbody;
        if (rb == null || !_bodies.TryGetValue(rb, out int n)) return;
        if (n <= 1) _bodies.Remove(rb); else _bodies[rb] = n - 1;
    }

    void Update()
    {
        float total = 0f;
        _stale.Clear();
        foreach (var kv in _bodies)
        {
            var rb = kv.Key;
            if (rb == null || !rb.gameObject.activeInHierarchy) { _stale.Add(rb); continue; }
            if (rb.isKinematic || !rb.useGravity) continue; // being held / carried
            total += rb.mass;
        }
        foreach (var rb in _stale) _bodies.Remove(rb);
        TotalMass = total;

        if (_mat == null) return;
        Color c;
        if (IsSatisfied) c = okColor;
        else if (total > 0.001f) c = Color.Lerp(idleColor, wrongColor, 0.6f + 0.4f * Mathf.Sin(Time.time * 14f));
        else c = idleColor;
        _mat.SetColor("_BaseColor", c);
        _mat.SetColor("_EmissionColor", IsSatisfied ? okColor * 2f : (total > 0.001f ? wrongColor * 0.8f : Color.black));
    }
}
