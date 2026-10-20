using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-way dusk-to-deep-night progression. Starts from however the scene is lit when Play begins
/// (your current night) and slowly gets darker, foggier and more claustrophobic. It never brightens
/// and never cycles back to day; once it reaches full darkness it holds there.
/// Leaves the flashlight and other local lights alone - only the sun, sky, ambient, fog and post-processing change.
/// </summary>
public class NightProgression : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds from the starting night to full darkness.")]
    public float duration = 600f;
    [Tooltip("Shape of the progression over time (0..1 -> 0..1). Ease-in makes the end feel sudden.")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Speed multiplier for testing (e.g. 20 to see the whole night in 30 s).")]
    public float debugTimeScale = 1f;

    [Header("Sun / moonlight")]
    public Light sun;
    [Range(0f, 1f)] public float endSunIntensity = 0.25f;
    public Color endSunTint = new Color(0.55f, 0.65f, 1f);
    [Tooltip("Degrees the sun sinks further below the horizon, fading the last glow from the sky.")]
    public float sunSinkDegrees = 25f;

    [Header("Sky & ambient (multipliers of the starting values)")]
    [Range(0f, 1f)] public float endSkyExposure = 0.3f;
    [Range(0f, 1f)] public float endAmbient = 0.3f;
    [Range(0f, 1f)] public float endReflections = 0.3f;

    [Header("Fog (visibility)")]
    public bool useFog = true;
    public Color fogColor = new Color(0.03f, 0.04f, 0.08f);
    public float startFogDensity = 0.02f;
    [Tooltip("Higher = shorter view distance at the end. Roughly 1 / density units of visibility.")]
    public float endFogDensity = 0.3f;

    [Header("Post-processing")]
    public Volume volume;
    public float endVignette = 0.5f;
    [Tooltip("Exposure change at full darkness (negative = darker).")]
    public float endPostExposure = -0.75f;
    [Range(0f, 1f)] public float endSaturation = 0.6f;

    [Header("Events")]
    public UnityEvent onFullDarkness;

    /// <summary>0 at the start, 1 at full darkness.</summary>
    public float Progress { get; private set; }
    public float Elapsed { get; private set; }

    // Starting values captured at Start.
    float _sunIntensity, _ambient, _reflections, _skyExposure, _vignette, _postExposure, _saturation;
    Color _sunColor;
    Quaternion _sunRotation;
    Material _sky;
    Vignette _vig;
    ColorAdjustments _color;
    bool _fired;
    float _envTimer;

    void Start()
    {
        if (sun == null) sun = RenderSettings.sun;
        if (volume == null) volume = FindFirstObjectByType<Volume>();

        if (sun != null)
        {
            _sunIntensity = sun.intensity;
            _sunColor = sun.color;
            _sunRotation = sun.transform.rotation;
        }
        _ambient = RenderSettings.ambientIntensity;
        _reflections = RenderSettings.reflectionIntensity;

        // Work on a copy of the skybox so the material asset isn't modified.
        if (RenderSettings.skybox != null)
        {
            _sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = _sky;
            if (_sky.HasProperty("_Exposure")) _skyExposure = _sky.GetFloat("_Exposure");
        }

        if (useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = startFogDensity;
        }

        if (volume != null)
        {
            var profile = volume.profile; // runtime instance; the asset stays untouched
            if (!profile.TryGet(out _vig)) _vig = profile.Add<Vignette>();
            if (!profile.TryGet(out _color)) _color = profile.Add<ColorAdjustments>();
            _vig.active = _color.active = true;
            _vignette = _vig.intensity.value;
            _postExposure = _color.postExposure.value;
            _saturation = _color.saturation.value;
            _vig.intensity.overrideState = true;
            _color.postExposure.overrideState = true;
            _color.saturation.overrideState = true;
        }

        Apply(0f);
    }

    void Update()
    {
        if (Progress >= 1f) return; // hold at full darkness

        Elapsed += Time.deltaTime * debugTimeScale;
        float linear = duration > 0f ? Mathf.Clamp01(Elapsed / duration) : 1f;
        // Never allow the curve to make things brighter again.
        Progress = Mathf.Max(Progress, Mathf.Clamp01(curve.Evaluate(linear)));
        if (linear >= 1f) Progress = 1f;

        Apply(Progress);

        if (Progress >= 1f && !_fired)
        {
            _fired = true;
            onFullDarkness?.Invoke();
        }
    }

    void Apply(float t)
    {
        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(_sunIntensity, _sunIntensity * endSunIntensity, t);
            sun.color = Color.Lerp(_sunColor, _sunColor * endSunTint, t);
            // Pitch the light further upward (= sun further below the horizon) around its own right axis.
            sun.transform.rotation = Quaternion.AngleAxis(-sunSinkDegrees * t, _sunRotation * Vector3.right) * _sunRotation;
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(_ambient, _ambient * endAmbient, t);
        RenderSettings.reflectionIntensity = Mathf.Lerp(_reflections, _reflections * endReflections, t);

        if (_sky != null && _sky.HasProperty("_Exposure"))
            _sky.SetFloat("_Exposure", Mathf.Lerp(_skyExposure, _skyExposure * endSkyExposure, t));

        if (useFog) RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, endFogDensity, t * t);

        if (_vig != null) _vig.intensity.value = Mathf.Lerp(_vignette, Mathf.Max(_vignette, endVignette), t);
        if (_color != null)
        {
            _color.postExposure.value = Mathf.Lerp(_postExposure, _postExposure + Mathf.Min(0f, endPostExposure), t);
            // Saturation is -100..100 in URP; 0 = unchanged.
            _color.saturation.value = Mathf.Lerp(_saturation, Mathf.Min(_saturation, (endSaturation - 1f) * 100f), t);
        }

        // Skybox-based ambient lighting has to be recalculated to pick up sky changes; once a second is plenty.
        _envTimer -= Time.deltaTime;
        if (_envTimer <= 0f)
        {
            _envTimer = 1f;
            DynamicGI.UpdateEnvironment();
        }
    }

    /// <summary>Jump to a point in the night (0..1), e.g. from a story trigger. Can't go backwards.</summary>
    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= Progress) return;
        Progress = t;
        Elapsed = Mathf.Max(Elapsed, t * duration);
        Apply(Progress);
    }
}
