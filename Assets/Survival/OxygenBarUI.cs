using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the oxygen HUD at runtime: a vertical bar on the right edge of the screen with an "O2" label
/// and time remaining, plus a failure screen when oxygen runs out. No Canvas setup needed.
/// </summary>
public class OxygenBarUI : MonoBehaviour
{
    public OxygenSystem oxygen;

    [Header("Bar")]
    public float barWidth = 16f;
    [Range(0.1f, 0.9f)] public float barHeight = 0.4f;
    public float rightMargin = 28f;
    public Color fillColor = new Color(0.25f, 0.65f, 1f);
    public Color refillColor = new Color(0.45f, 0.95f, 1f);
    public Color lowColor = new Color(1f, 0.25f, 0.2f);
    public Color boostColor = new Color(0.4f, 1f, 0.8f);
    public Color frameColor = new Color(1f, 1f, 1f, 0.85f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);

    RectTransform _fill;
    Image _fillImage;
    Text _timeText, _label;
    GameObject _failScreen;

    void Awake()
    {
        if (oxygen == null) oxygen = GetComponent<OxygenSystem>();
        Build();
    }

    void Build()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("OxygenCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        // Bar container, right-middle.
        var bar = NewRect("OxygenBar", canvasGo.transform);
        bar.anchorMin = bar.anchorMax = new Vector2(1f, 0.5f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.anchoredPosition = new Vector2(-rightMargin, 0f);
        bar.sizeDelta = new Vector2(barWidth + 6f, 1080f * barHeight);

        // Frame (outer), background (inner), fill.
        AddImage(bar, frameColor);
        var bg = NewRect("Background", bar);
        Stretch(bg, 2f);
        AddImage(bg, backgroundColor);

        _fill = NewRect("Fill", bg);
        _fill.anchorMin = new Vector2(0f, 0f);
        _fill.anchorMax = new Vector2(1f, 1f);
        _fill.offsetMin = new Vector2(1f, 1f);
        _fill.offsetMax = new Vector2(-1f, -1f);
        _fillImage = AddImage(_fill, fillColor);

        // Tick marks every 25%.
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
        _label.text = "O₂";

        _timeText = NewText("Time", bar, font, 18, TextAnchor.UpperCenter);
        var tr = _timeText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -8f);
        tr.sizeDelta = new Vector2(90f, 28f);

        // Failure screen.
        var fail = NewRect("OutOfOxygen", canvasGo.transform);
        Stretch(fail, 0f);
        AddImage(fail, new Color(0f, 0f, 0f, 0.75f));
        var title = NewText("Title", fail, font, 64, TextAnchor.MiddleCenter);
        Stretch(title.rectTransform, 0f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        title.text = "OUT OF OXYGEN";
        title.color = lowColor;
        var hint = NewText("Hint", fail, font, 28, TextAnchor.MiddleCenter);
        Stretch(hint.rectTransform, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        hint.text = "Press R to restart";
        _failScreen = fail.gameObject;
        _failScreen.SetActive(false);
    }

    void Update()
    {
        if (oxygen == null) return;

        float n = oxygen.Normalized;
        _fill.anchorMax = new Vector2(1f, n);

        Color c = oxygen.IsRefilling ? refillColor : fillColor;
        if (oxygen.IsLow && !oxygen.IsRefilling)
            c = Color.Lerp(lowColor, fillColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f) * 0.5f);
        _fillImage.color = c;
        _label.color = oxygen.IsLow ? lowColor : Color.white;

        if (oxygen.BoostRemaining > 0f)
        {
            int b = Mathf.CeilToInt(oxygen.BoostRemaining);
            _timeText.text = $"BOOST {b / 60}:{b % 60:00}";
            _fillImage.color = Color.Lerp(boostColor, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 4f));
        }
        else if (oxygen.IsRefilling) _timeText.text = "REFILL";
        else
        {
            int s = Mathf.CeilToInt(oxygen.SecondsRemaining);
            _timeText.text = $"{s / 60}:{s % 60:00}";
        }
        _timeText.color = oxygen.IsLow ? lowColor : Color.white;

        if (oxygen.IsDepleted && !_failScreen.activeSelf)
        {
            _failScreen.SetActive(true);
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
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        return t;
    }
}
