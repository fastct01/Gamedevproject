using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds its own screen-space UI at runtime: a thin white line along the bottom of the screen
/// that shrinks toward the centre as stamina drops. No Canvas or Image setup needed.
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    public StaminaSystem stamina;
    [Tooltip("Line thickness in pixels.")]
    public float thickness = 6f;
    [Tooltip("Fraction of the screen width the line spans when stamina is full.")]
    [Range(0.1f, 1f)] public float maxWidth = 0.4f;
    [Tooltip("Gap between the line and the bottom edge, in pixels.")]
    public float bottomMargin = 20f;
    public Color color = Color.white;
    [Tooltip("Opacity of the line while sprinting is locked out.")]
    [Range(0f, 1f)] public float exhaustedAlpha = 0.4f;
    [Tooltip("Hide the line while stamina is full.")]
    public bool hideWhenFull = true;

    RectTransform _fill;
    Image _image;
    CanvasGroup _group;

    void Awake()
    {
        if (stamina == null) stamina = GetComponent<StaminaSystem>();

        var canvasGo = new GameObject("StaminaCanvas", typeof(Canvas), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        var lineGo = new GameObject("StaminaLine", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(canvasGo.transform, false);
        _image = lineGo.GetComponent<Image>();
        _image.raycastTarget = false;
        _fill = lineGo.GetComponent<RectTransform>();
        _fill.pivot = new Vector2(0.5f, 0f);
        _fill.anchoredPosition = new Vector2(0f, bottomMargin);
        _fill.sizeDelta = new Vector2(0f, thickness);
    }

    void Update()
    {
        if (stamina == null) return;

        float half = maxWidth * 0.5f * stamina.Normalized;
        _fill.anchorMin = new Vector2(0.5f - half, 0f);
        _fill.anchorMax = new Vector2(0.5f + half, 0f);
        _fill.offsetMin = new Vector2(0f, bottomMargin);
        _fill.offsetMax = new Vector2(0f, bottomMargin + thickness);

        var c = color;
        if (stamina.IsExhausted) c.a *= exhaustedAlpha;
        _image.color = c;

        float target = hideWhenFull && stamina.Normalized >= 0.999f ? 0f : 1f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.deltaTime * 4f);
    }
}
