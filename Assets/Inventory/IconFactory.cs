using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws simple item icons at runtime (no art needed): blaster, gear, padlock.</summary>
public static class IconFactory
{
    const int Size = 64;
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite For(InventoryItem item) => item == null ? null : Get(item.iconStyle.ToString(), item.iconColor);
    public static Sprite Lock() => Get("Lock", new Color(0.7f, 0.72f, 0.78f));

    static Sprite Get(string style, Color color)
    {
        string key = style + ColorUtility.ToHtmlStringRGBA(color);
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float a = style == "Gear" ? Gear(x + 0.5f, y + 0.5f)
                        : style == "Blaster" ? Blaster(x + 0.5f, y + 0.5f)
                        : style == "Torch" ? Torch(x + 0.5f, y + 0.5f)
                        : style == "OxygenTank" ? OxygenTank(x + 0.5f, y + 0.5f)
                        : LockShape(x + 0.5f, y + 0.5f);
                Color c = color;
                // Simple shading: lighter towards the top.
                c = Color.Lerp(c * 0.75f, c, y / (float)Size);
                c.a = a;
                px[y * Size + x] = c;
            }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        Cache[key] = s;
        return s;
    }

    static float Box(float x, float y, float x0, float y0, float x1, float y1)
    {
        float dx = Mathf.Min(x - x0, x1 - x), dy = Mathf.Min(y - y0, y1 - y);
        return Mathf.Clamp01(Mathf.Min(dx, dy) + 0.5f);
    }

    static float Gear(float x, float y)
    {
        float cx = x - Size / 2f, cy = y - Size / 2f;
        float r = Mathf.Sqrt(cx * cx + cy * cy);
        float ang = Mathf.Atan2(cy, cx);
        const int teeth = 10;
        float t = Mathf.Repeat(ang / (Mathf.PI * 2f) * teeth, 1f);
        float outer = (t > 0.15f && t < 0.6f) ? 29f : 23f;
        float body = Mathf.Clamp01(outer - r + 0.5f);
        float hole = Mathf.Clamp01(r - 8f + 0.5f);
        return Mathf.Min(body, hole);
    }

    static float Blaster(float x, float y)
    {
        float a = 0f;
        a = Mathf.Max(a, Box(x, y, 8, 30, 44, 44));   // body
        a = Mathf.Max(a, Box(x, y, 44, 34, 58, 40));  // barrel
        a = Mathf.Max(a, Box(x, y, 14, 12, 26, 31));  // grip
        a = Mathf.Max(a, Box(x, y, 22, 44, 36, 49));  // top cell
        float trigger = Box(x, y, 28, 22, 31, 30);
        return Mathf.Max(a, trigger);
    }

    static float Torch(float x, float y)
    {
        float body = Box(x, y, 8, 26, 40, 38);    // handle
        float head = Box(x, y, 40, 21, 52, 43);   // head
        float lens = Box(x, y, 52, 23, 56, 41);   // lens
        float button = Box(x, y, 20, 38, 26, 41);
        return Mathf.Max(Mathf.Max(body, head), Mathf.Max(lens, button));
    }

    static float OxygenTank(float x, float y)
    {
        // Rounded canister with a valve on top.
        float body = Box(x, y, 20, 8, 44, 46);
        float cx = x - 32f, cy = y - 46f;
        float dome = cy >= 0f ? Mathf.Clamp01(12.5f - Mathf.Sqrt(cx * cx + cy * cy)) : 0f;
        float valve = Box(x, y, 28, 56, 36, 62);
        float band = 1f - Box(x, y, 20, 24, 44, 28) * 0.6f; // darker stripe
        return Mathf.Max(Mathf.Max(body * band, dome), valve);
    }

    static float LockShape(float x, float y)
    {
        float body = Box(x, y, 18, 10, 46, 34);
        float cx = x - 32f, cy = y - 34f;
        float r = Mathf.Sqrt(cx * cx + cy * cy);
        float arc = (cy >= 0f) ? Mathf.Clamp01(1.5f - Mathf.Abs(r - 10f) + 0.5f) : 0f;
        float legs = Mathf.Max(Box(x, y, 20, 30, 24, 36), Box(x, y, 40, 30, 44, 36));
        return Mathf.Max(body, Mathf.Max(arc, legs));
    }
}
