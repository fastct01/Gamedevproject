using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A flashlight the player (pill) holds in its hand, shown first-person in the lower right of the screen.
/// The beam comes out of the lens and meets the centre of the screen a little way ahead. The light is steady,
/// with only a slight sway when turning. F toggles it; while it's off a small white dot appears in the centre.
/// Put it on the Player root. "Hold" is the pivot the model and lights are parented to (it's moved under the camera).
/// </summary>
[DefaultExecutionOrder(100)] // after the controller has rotated the camera
public class PlayerFlashlight : MonoBehaviour
{
    [Header("References")]
    public Transform hold;
    [Tooltip("The flashlight mesh. Its pose is applied automatically on Play, so stray edits can't move it out of the hand.")]
    public Transform model;
    public Light beam;
    [Tooltip("Optional small point light at the lens that lights up the hand and nearby ground.")]
    public Light spill;
    public Camera aimCamera;
    [Tooltip("Renderers whose emission glows while the light is on (bulb, lens).")]
    public Renderer[] glowRenderers;
    [Tooltip("Which material slots on those renderers glow (for the bundled model: 1 = lens, 3 = bulb).")]
    public int[] glowMaterialSlots = { 1, 3 };
    public Color glowColor = new Color(1f, 0.95f, 0.85f) * 1.5f;

    [Header("Hold (first-person, player-local units relative to the camera)")]
    [Tooltip("Where the flashlight sits in view: x = right, y = up, z = forward.")]
    public Vector3 viewOffset = new Vector3(0.2f, -0.13f, 0.7f);
    [Tooltip("Distance ahead where the beam crosses the centre of the screen.")]
    public float convergeDistance = 40f;
    [Tooltip("Max degrees the flashlight lags behind when you turn. 0 = locked to the camera.")]
    [Range(0f, 10f)] public float swayAmount = 1.5f;
    public float swaySmoothing = 12f;
    [Tooltip("The player is tiny, so the camera's near clip must be very small or the hand-held model is cut off.")]
    public bool fixCameraNearClip = true;
    public float nearClip = 0.005f;
    [Tooltip("Model position inside the hold (keep near 0 so the lens lines up with the beam).")]
    public Vector3 modelOffset = new Vector3(0f, 0f, 0.05f);
    [Tooltip("Extra angle on the model only (not the beam) so you see its side, like it's held in a hand.")]
    public Vector3 modelTilt = new Vector3(-5f, -6f, -10f);
    public float modelScale = 1.7f;

    [Header("Beam")]
    public bool startOn = true;
    public float intensity = 8f;
    [Range(1f, 179f)] public float outerAngle = 55f;
    [Range(0f, 179f)] public float innerAngle = 18f;
    public float range = 12f;
    [Tooltip("Kelvin. ~3000 = warm incandescent, ~5600 = white LED, ~6500 = cool LED.")]
    public float colorTemperature = 5600f;
    [Tooltip("Optional light pattern texture. Leave empty for a clean, plain beam.")]
    public Texture cookie;
    public bool castShadows = true;
    public float spillIntensity = 0.02f;
    public float spillRange = 0.12f;
    [Tooltip("Seconds to reach full brightness when switched on.")]
    public float switchOnTime = 0.07f;
    [Tooltip("Hide the flashlight model while the light is off (it lowers out of view, and comes back up when switched on).")]
    public bool hideWhenOff = true;
    [Tooltip("Seconds for the flashlight to lower out of / raise into view.")]
    public float hideTime = 0.15f;
    [Tooltip("How far down it drops when put away (player-local units).")]
    public float hideDrop = 0.25f;
    [Tooltip("Pull the flashlight back and down when you're right up against a wall or box, so it doesn't poke through.")]
    public bool retractNearWalls = true;

    [Header("Flicker")]
    [Tooltip("Constant subtle brightness wobble (0 = steady).")]
    [Range(0f, 0.2f)] public float flickerNoise = 0f;
    [Tooltip("Average seconds between short stutters (0 = never).")]
    public float stutterInterval = 0f;

    [Header("Input & audio")]
    public Key toggleKey = Key.F;
    [Tooltip("Inventory item id of the torch. While it isn't the equipped hotbar item, the torch is put away and off.")]
    public string inventoryItemId = "torch";
    public AudioSource audioSource;
    public AudioClip clickOn, clickOff;

    [Header("Centre dot (shown while the light is off)")]
    public float dotSize = 6f;
    public Color dotColor = Color.white;

    public bool IsOn { get; private set; }

    float _level, _stutterTime, _stutterValue = 1f, _noiseSeed, _shown = 1f;
    Renderer[] _modelRenderers;
    Collider[] _ownColliders;
    float _retract;
    Inventory _inv;
    bool _wasHolding = true;
    Quaternion _lagRot;
    Material[] _glowMats;
    Image _dot, _dotShadow;

    void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponentInChildren<Camera>();
        if (aimCamera == null) aimCamera = Camera.main;
        _noiseSeed = Random.value * 100f;
        _ownColliders = GetComponentsInChildren<Collider>(true);

        ApplySetup();
        if (aimCamera != null) _lagRot = aimCamera.transform.rotation;

        // The old toggle script would fight with this one over the same light.
        foreach (var t in GetComponentsInChildren<Torchlight1>(true)) t.enabled = false;

        if (glowRenderers != null)
        {
            var list = new System.Collections.Generic.List<Material>();
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                var mats = r.materials; // instances, so the assets aren't changed
                foreach (int slot in glowMaterialSlots)
                {
                    if (slot < 0 || slot >= mats.Length) continue;
                    mats[slot].EnableKeyword("_EMISSION");
                    list.Add(mats[slot]);
                }
            }
            _glowMats = list.ToArray();
        }

        IsOn = startOn;
        _level = IsOn ? 1f : 0f;
        _shown = IsOn || !hideWhenOff ? 1f : 0f;
        // Only renderers that start enabled, so a hidden renderer stays hidden.
        _modelRenderers = model != null
            ? System.Array.FindAll(model.GetComponentsInChildren<Renderer>(true), r => r.enabled)
            : new Renderer[0];
        SetModelVisible(_shown > 0.001f);
        BuildDot();
    }

    /// <summary>Puts the hold on the camera, the model in the hand and the lights at the lens, with the beam settings applied.</summary>
    [ContextMenu("Apply Setup Now")]
    public void ApplySetup()
    {
        if (aimCamera == null) aimCamera = GetComponentInChildren<Camera>();
        if (aimCamera != null)
        {
            if (fixCameraNearClip) aimCamera.nearClipPlane = nearClip;
            // Ride on the camera so the flashlight never lags or jitters relative to the view.
            if (hold != null)
            {
                hold.SetParent(aimCamera.transform, false);
                hold.localScale = Vector3.one;
                hold.localPosition = viewOffset;
                hold.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, convergeDistance) - viewOffset, Vector3.up);
            }
        }

        if (model != null && hold != null)
        {
            model.SetParent(hold, false);
            model.localPosition = modelOffset;
            model.localRotation = Quaternion.Euler(modelTilt) * Quaternion.Euler(90f, 0f, 0f); // mesh's lens end is +Y
            model.localScale = Vector3.one * modelScale;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Beam starts just in front of the lens (the bundled mesh's lens is 0.11 units from its centre).
        Vector3 lens = modelOffset + Vector3.forward * (0.11f * modelScale + 0.01f);
        if (beam != null && hold != null)
        {
            beam.transform.SetParent(hold, false);
            beam.transform.localPosition = lens;
            beam.transform.localRotation = Quaternion.identity;
        }
        if (spill != null && hold != null)
        {
            spill.transform.SetParent(hold, false);
            spill.transform.localPosition = lens + Vector3.forward * 0.3f;
        }

        ConfigureLights();
    }

    void ConfigureLights()
    {
        if (beam != null)
        {
            beam.type = LightType.Spot;
            beam.spotAngle = outerAngle;
            beam.innerSpotAngle = Mathf.Min(innerAngle, outerAngle);
            beam.range = range;
            beam.useColorTemperature = true;
            beam.colorTemperature = colorTemperature;
            beam.color = Color.white;
            beam.cookie = cookie;
            beam.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            beam.shadowNearPlane = 0.1f;
            beam.intensity = intensity;
        }
        if (spill != null)
        {
            spill.type = LightType.Point;
            spill.range = spillRange;
            spill.useColorTemperature = true;
            spill.colorTemperature = colorTemperature;
            spill.shadows = LightShadows.None;
            spill.intensity = spillIntensity;
        }
    }

    // True if the torch is in hand (or if there's no inventory / no torch item, so it works stand-alone).
    bool Holding()
    {
        if (_inv == null) _inv = GetComponent<Inventory>();
        if (_inv == null || !_inv.HasInHotbar(inventoryItemId)) return true;
        return _inv.Equipped != null && _inv.Equipped.id == inventoryItemId;
    }

    void Update()
    {
        bool holding = Holding();
        if (holding != _wasHolding)
        {
            SetOn(holding); // taking it out switches it on, putting it away switches it off
            _wasHolding = holding;
        }

        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            if (holding) Toggle();
            else if (_inv != null) _inv.EquipById(inventoryItemId); // F takes the torch out if it's put away
        }

        // Ramp brightness: quick fade-in like a real bulb, near-instant off.
        float target = IsOn ? 1f : 0f;
        float speed = IsOn ? 1f / Mathf.Max(0.001f, switchOnTime) : 50f;
        _level = Mathf.MoveTowards(_level, target, speed * Time.deltaTime);

        float flicker = 1f;
        if (IsOn)
        {
            flicker += (Mathf.PerlinNoise(_noiseSeed, Time.time * 6f) - 0.5f) * 2f * flickerNoise;
            if (stutterInterval > 0f && _stutterTime <= 0f && Random.value < Time.deltaTime / stutterInterval)
                _stutterTime = Random.Range(0.15f, 0.45f);
            if (_stutterTime > 0f)
            {
                _stutterTime -= Time.deltaTime;
                if (Random.value < 0.35f) _stutterValue = Random.Range(0.15f, 1f);
                flicker *= _stutterValue;
            }
        }

        float k = _level * flicker;
        if (beam != null)
        {
            beam.intensity = intensity * k;
            beam.enabled = k > 0.001f;
        }
        if (spill != null)
        {
            spill.intensity = spillIntensity * k;
            spill.enabled = k > 0.001f;
        }
        if (_glowMats != null)
            foreach (var m in _glowMats) m.SetColor("_EmissionColor", glowColor * k);

        // Centre dot fades in while the light is off.
        if (_dot != null)
        {
            float a = Mathf.MoveTowards(_dot.color.a, IsOn ? 0f : dotColor.a, Time.deltaTime * 8f);
            _dot.color = new Color(dotColor.r, dotColor.g, dotColor.b, a);
            _dotShadow.color = new Color(0f, 0f, 0f, a * 0.5f);
        }
    }

    void LateUpdate()
    {
        if (hold == null || aimCamera == null) return;

        // Fixed pose in view, angled so the beam crosses the screen centre at convergeDistance.
        Quaternion baseRot = Quaternion.LookRotation(new Vector3(0f, 0f, convergeDistance) - viewOffset, Vector3.up);

        // Gentle sway: the flashlight trails the camera by up to swayAmount degrees when turning.
        Quaternion camRot = aimCamera.transform.rotation;
        _lagRot = Quaternion.Slerp(_lagRot, camRot, 1f - Mathf.Exp(-swaySmoothing * Time.deltaTime));
        Quaternion sway = Quaternion.Inverse(camRot) * _lagRot;
        sway.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        sway = Mathf.Abs(angle) < 0.001f || float.IsNaN(axis.x) ? Quaternion.identity
            : Quaternion.AngleAxis(Mathf.Clamp(angle, -swayAmount, swayAmount), axis);

        // Lower the flashlight out of view while it's off, raise it back when it's on.
        float shownTarget = IsOn || !hideWhenOff ? 1f : 0f;
        _shown = Mathf.MoveTowards(_shown, shownTarget, Time.deltaTime / Mathf.Max(0.001f, hideTime));
        SetModelVisible(_shown > 0.001f);
        float ease = _shown * _shown * (3f - 2f * _shown); // smoothstep
        // Retract when something is closer than the end of the flashlight.
        float target = 0f;
        if (retractNearWalls)
        {
            var cam = aimCamera.transform;
            float scale = cam.lossyScale.z;
            float reach = (viewOffset.z + modelOffset.z + 0.11f * modelScale) * scale;
            float nearest = reach;
            foreach (var h in Physics.SphereCastAll(cam.position, 0.15f * scale, cam.forward, reach, ~0, QueryTriggerInteraction.Ignore))
                if (h.distance < nearest && System.Array.IndexOf(_ownColliders, h.collider) < 0) nearest = h.distance;
            target = 1f - nearest / reach;
        }
        _retract = Mathf.MoveTowards(_retract, target, Time.deltaTime * 6f);
        Vector3 retractOffset = new Vector3(0f, -0.12f, -viewOffset.z * 0.6f) * _retract;

        hold.localPosition = viewOffset + Vector3.down * (1f - ease) * hideDrop + retractOffset;
        hold.localRotation = Quaternion.Euler(25f * _retract, 0f, 0f) * sway * baseRot;
    }

    public void Toggle() => SetOn(!IsOn);

    void SetModelVisible(bool visible)
    {
        if (_modelRenderers == null) return;
        foreach (var r in _modelRenderers)
            if (r != null && r.enabled != visible) r.enabled = visible;
    }

    public void SetOn(bool on)
    {
        if (IsOn == on) return;
        IsOn = on;
        var clip = on ? clickOn : clickOff;
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void BuildDot()
    {
        var canvasGo = new GameObject("CrosshairCanvas", typeof(Canvas));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var sprite = MakeCircleSprite(64);
        _dotShadow = MakeDot(canvasGo.transform, "DotShadow", sprite, dotSize + 3f);
        _dot = MakeDot(canvasGo.transform, "Dot", sprite, dotSize);
        float a = IsOn ? 0f : dotColor.a;
        _dot.color = new Color(dotColor.r, dotColor.g, dotColor.b, a);
        _dotShadow.color = new Color(0f, 0f, 0f, a * 0.5f);
    }

    static Image MakeDot(Transform parent, string name, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // Anti-aliased white circle so the dot is round, not square.
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
