using UnityEngine;

/// <summary>
/// Portable power for the light emitter. Drains while powering the beam and recharges inside any
/// oxygen zone (e.g. carry it back to the ship). Glows brighter the more charge it has.
/// </summary>
public class PowerCore : MonoBehaviour
{
    [Range(0f, 1f)] public float charge = 1f;
    [Tooltip("Seconds to recharge from empty inside an oxygen zone.")]
    public float rechargeSeconds = 4f;
    public Renderer glowRenderer;
    public Light glowLight;
    public Color glowColor = new Color(1f, 0.55f, 0.15f);

    Material _mat;

    void Awake()
    {
        if (glowRenderer != null) { _mat = glowRenderer.material; _mat.EnableKeyword("_EMISSION"); }
    }

    public void Drain(float amount) => charge = Mathf.Max(0f, charge - amount);

    void Update()
    {
        foreach (var z in OxygenZone.Active)
            if (z != null && z.isActiveAndEnabled && z.Contains(transform.position))
            {
                charge = Mathf.Min(1f, charge + Time.deltaTime / Mathf.Max(0.1f, rechargeSeconds));
                break;
            }

        float pulse = charge > 0f && charge < 0.25f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 10f) : 1f;
        if (_mat != null)
        {
            _mat.SetColor("_BaseColor", Color.Lerp(new Color(0.2f, 0.2f, 0.2f), glowColor, charge));
            _mat.SetColor("_EmissionColor", glowColor * 2.5f * charge * pulse);
        }
        if (glowLight != null) glowLight.intensity = 0.02f * charge * pulse;
    }
}
