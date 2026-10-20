using UnityEngine;

/// <summary>Glow-in-the-dark marking: only visible while the player's flashlight is OFF.</summary>
public class DarkGlyph : MonoBehaviour
{
    public Color color = new Color(0.4f, 1f, 0.7f, 0.9f);
    public float fadeSpeed = 3f;

    Renderer[] _renderers;
    Material[] _mats;
    PlayerFlashlight _flashlight;
    float _a;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mats = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _mats[i] = _renderers[i].material;
    }

    void Start() => _flashlight = FindFirstObjectByType<PlayerFlashlight>();

    void Update()
    {
        float target = _flashlight != null && _flashlight.IsOn ? 0f : 1f;
        _a = Mathf.MoveTowards(_a, target, Time.deltaTime * fadeSpeed);
        bool show = _a > 0.01f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].enabled = show;
            if (show) _mats[i].SetColor("_BaseColor", new Color(color.r, color.g, color.b, color.a * _a));
        }
    }
}
