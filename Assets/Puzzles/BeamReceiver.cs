using UnityEngine;

/// <summary>Lights up while it's being hit by a beam of the required colour.</summary>
public class BeamReceiver : MonoBehaviour
{
    public BeamColor required = BeamColor.White;
    public Renderer indicator;
    public Light glow;
    public Color litColor = Color.white;
    public Color idleColor = new Color(0.15f, 0.15f, 0.18f);

    float _lastHit = -10f;
    float _level;
    Material _mat;

    public bool IsLit => Time.time - _lastHit < 0.15f;

    void Awake()
    {
        if (indicator != null) { _mat = indicator.material; _mat.EnableKeyword("_EMISSION"); }
    }

    public void ReportHit(BeamColor color)
    {
        if (color == required) _lastHit = Time.time;
    }

    void Update()
    {
        _level = Mathf.MoveTowards(_level, IsLit ? 1f : 0f, Time.deltaTime * 4f);
        if (_mat != null)
        {
            _mat.SetColor("_BaseColor", Color.Lerp(idleColor, litColor, _level));
            _mat.SetColor("_EmissionColor", litColor * 2.5f * _level);
        }
        if (glow != null) { glow.enabled = _level > 0.01f; glow.intensity = 0.05f * _level; }
    }
}
