using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the health HUD at runtime: a red vertical bar on the right of the screen (next to the oxygen bar),
/// a red screen flash when hurt, and a death screen. No Canvas setup needed.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    public PlayerHealth health;

    [Header("Bar")]
    public float barWidth = 16f;
    [Range(0.1f, 0.9f)] public float barHeight = 0.4f;
    [Tooltip("Distance from the right edge. The oxygen bar sits at 28, so 64 puts this just left of it.")]
    public float rightMargin = 64f;
    public Color fillColor = new Color(0.9f, 0.15f, 0.15f);
    public Color lowColor = new Color(1f, 0.45f, 0.1f);
    [Tooltip("Tint while healing in the ship zone.")]
    public Color healColor = new Color(0.4f, 1f, 0.5f);
    public Color frameColor = new Color(1f, 1f, 1f, 0.85f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);

    RectTransform _fill;
    Image _fillImage, _flash;
    Text _value, _label;
    GameObject _deathScreen;
    float _shown = 1f;

    void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        Build();
    }

    void Build()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("HealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 111;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        // Red damage flash (full screen, behind the bar).
        var flash = NewRect("DamageFlash", canvasGo.transform);
        Stretch(flash, 0f);
        _flash = AddImage(flash, new Color(0.8f, 0f, 0f, 0f));

        var bar = NewRect("HealthBar", canvasGo.transform);
        bar.anchorMin = bar.anchorMax = new Vector2(1f, 0.5f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.anchoredPosition = new Vector2(-rightMargin, 0f);
        bar.sizeDelta = new Vector2(barWidth + 6f, 1080f * barHeight);
        AddImage(bar, frameColor);
        var bg = NewRect("Background", bar);
        Stretch(bg, 2f);
        AddImage(bg, backgroundColor);
        _fill = NewRect("Fill", bg);
        _fill.anchorMin = Vector2.zero;
        _fill.anchorMax = Vector2.one;
        _fill.offsetMin = new Vector2(1f, 1f);
        _fill.offsetMax = new Vector2(-1f, -1f);
        _fillImage = AddImage(_fill, fillColor);
        for (int i = 1; i < 4; i++)
        {
            var tick = NewRect("Tick", bg);
            tick.anchorMin = new Vector2(0f, i * 0.25f);
            tick.anchorMax = new Vector2(1f, i * 0.25f);
            tick.sizeDelta = new Vector2(0f, 1f);
            AddImage(tick, new Color(1f, 1f, 1f, 0.35f));
        }

        _label = NewText("Label", bar, font, 22, TextAnchor.LowerCenter);
        var lr = _label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.anchoredPosition = new Vector2(0f, 8f);
        lr.sizeDelta = new Vector2(80f, 30f);
        _label.text = "HP";

        _value = NewText("Value", bar, font, 18, TextAnchor.UpperCenter);
        var vr = _value.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.anchoredPosition = new Vector2(0f, -8f);
        vr.sizeDelta = new Vector2(90f, 28f);

        var death = NewRect("YouDied", canvasGo.transform);
        Stretch(death, 0f);
        AddImage(death, new Color(0.15f, 0f, 0f, 0.8f));
        var title = NewText("Title", death, font, 64, TextAnchor.MiddleCenter);
        Stretch(title.rectTransform, 0f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        title.text = "YOU DIED";
        title.color = fillColor;
        var hint = NewText("Hint", death, font, 28, TextAnchor.MiddleCenter);
        Stretch(hint.rectTransform, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        hint.text = "Press R to restart";
        _deathScreen = death.gameObject;
        _deathScreen.SetActive(false);
    }

    void Update()
    {
        if (health == null) return;
        // Bar eases down so hits are readable.
        _shown = Mathf.MoveTowards(_shown, health.Normalized, Time.deltaTime * 1.5f);
        _fill.anchorMax = new Vector2(1f, _shown);
        bool low = health.Normalized <= 0.3f;
        Color c = low ? Color.Lerp(fillColor, lowColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : fillColor;
        if (health.IsHealing) c = Color.Lerp(c, healColor, 0.5f + 0.3f * Mathf.Sin(Time.time * 6f));
        _fillImage.color = c;
        _value.text = health.IsHealing ? "+" + Mathf.CeilToInt(health.Current) : Mathf.CeilToInt(health.Current).ToString();
        _label.color = low ? lowColor : Color.white;

        float since = Time.time - health.LastHitTime;
        _flash.color = new Color(0.8f, 0f, 0f, Mathf.Clamp01(0.45f - since * 0.9f));

        if (health.IsDead && !_deathScreen.activeSelf)
        {
            _deathScreen.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        rt.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        return t;
    }
}
